"""Read-only pixel/registration comparison. This script never writes or edits image pixels."""
from pathlib import Path
import hashlib,json
import numpy as np
from PIL import Image
import sys
root=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parents[2]
repo=root/'UnityProject';qa=root/'QA';old=qa/'AtlasBaseline-v0815'
baseline=json.loads((old/'baseline.json').read_text());results=[];checks=[]
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def frame(atlas,record):
    b=record['atlasBounds'];a=atlas[atlas.shape[0]-b['y']-b['height']:atlas.shape[0]-b['y'],b['x']:b['x']+b['width']][::-1]
    y,x=np.nonzero(a[:,:,3]);rgba=a[y,x]
    coords=np.column_stack((x-record['pivot']['x']*b['width'],y-record['pivot']['y']*b['height']))
    return coords,rgba
for entry in baseline['entries']:
    name=entry['name'];source=repo/'Assets/SF/ArtSource'/(name+'.png');layout=source.with_suffix('.json')
    assert sha(source)==entry['sourceSHA256'] and sha(layout)==entry['layoutSHA256'],name+' source or layout changed'
    assert sha(old/(name+'.png'))==entry['atlasSHA256'] and sha(old/(name+'.json'))==entry['metadataSHA256'],name+' baseline changed'
    checks.append('PASS: '+name+' original source, layout and immutable baseline hashes preserved')
    before=json.loads((old/(name+'.json')).read_text());after=json.loads((qa/'Cutouts'/(name+'.json')).read_text())
    with Image.open(old/(name+'.png')) as im:prior=np.array(im.convert('RGBA'))
    with Image.open(qa/'Cutouts'/(name+'.png')) as im:current=np.array(im.convert('RGBA'))
    assert len(before['frames'])==len(after['frames'])==entry['frames']
    assert after['packing']=='rectangular-depth-v1' and current.shape[1]==after['columns']*after['cellWidth'] and current.shape[0]==after['rows']*after['cellHeight']
    assert current.nbytes<prior.nbytes,name+' did not save texture bytes'
    checks.append('PASS: '+name+' rectangular atlas strictly smaller with unchanged frame count')
    errors=[];visible=0
    for i,(a,b) in enumerate(zip(before['frames'],after['frames'])):
        assert a['sourceBounds']==b['sourceBounds'] and a['sourceRoot']==b['sourceRoot'] and a['pixelCount']==b['pixelCount'],(name,i,'source registration changed')
        ca,pa=frame(prior,a);cb,pb=frame(current,b)
        assert pa.shape==pb.shape and np.array_equal(pa,pb),(name,i,'visible RGBA changed')
        assert ca.shape==cb.shape
        err=float(np.max(np.abs(ca-cb)));assert err<.0001,(name,i,'pixel-space pivot changed',err)
        assert len(pb)==b['pixelCount'],(name,i,'alpha pixel count')
        errors.append(err);visible+=len(pb);checks.append(f'PASS: {name} frame {i} visible RGBA, source registration and pixel-to-pivot positions preserved')
    results.append({'name':name,'frames':len(after['frames']),'oldDimensions':[prior.shape[1],prior.shape[0]],'newDimensions':[current.shape[1],current.shape[0]],'oldRGBA32Bytes':prior.nbytes,'newRGBA32Bytes':current.nbytes,'visiblePixels':visible,'maxPivotDifferencePixels':max(errors),'visibleRGBAIdentical':True,'sourceSHA256':sha(source),'layoutSHA256':sha(layout),'packedAtlasSHA256':sha(qa/'Cutouts'/(name+'.png'))})
assert len(results)==35 and sum(x['frames'] for x in results)==336 and len(checks)==406
folder=qa/'Atlas-v0816';folder.mkdir(exist_ok=True)
prior=sum(x['oldRGBA32Bytes'] for x in results);current=sum(x['newRGBA32Bytes'] for x in results)
report={'scope':'35 north/south depth atlases only; 336 source frames. No new or edited source artwork, resolution reduction, texture compression or timing changes.','sourceVersion':'0.8.15','version':'0.8.16','checksPassed':len(checks),'atlasCount':35,'framesCompared':336,'oldRGBA32Bytes':prior,'newRGBA32Bytes':current,'savedRGBA32Bytes':prior-current,'savedPercent':100*(prior-current)/prior,'allVisibleRGBAIdentical':True,'maxPivotDifferencePixels':max(x['maxPivotDifferencePixels'] for x in results),'limits':'RGBA32 texture-pixel storage only, not measured whole-process memory, frame rate, load time or physical-tablet performance. Transparent padding and some sprite rectangle bounds change; visible pixel positions do not. Side rigs and props excluded.','atlases':results}
(folder/'atlas-comparison.json').write_text(json.dumps(report,indent=2)+'\n');(folder/'atlas-checks.txt').write_text('\n'.join(checks)+'\n')
for name in ['baseline.json']:(folder/name).write_bytes((old/name).read_bytes())
print(json.dumps({k:v for k,v in report.items() if k!='atlases'},indent=2))
