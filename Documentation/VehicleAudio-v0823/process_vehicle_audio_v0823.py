from pathlib import Path
import sys, math, hashlib, json, uuid, shutil
base=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(base/'work/sf/audio-tools'))
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

r=base/'outputs/StefanieAndFernando'; provenance=r/'AudioLicenses/Vehicle-v0823'
dest=r/'UnityProject/Assets/SF/Resources/Audio'; qa=r/'QA/Vehicle-v0823'
qa.mkdir(exist_ok=True);(provenance/'runtime').mkdir(exist_ok=True)
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
items=[
 ('vehicle_engine','engine/engine-loop/engine-loop-1.wav',True,.64,'Car engine loop (96kHz, 4s)','Iwan (qubodup) Gabovitch','CC BY 3.0','https://opengameart.org/content/car-engine-loop-96khz-4s'),
 ('vehicle_tires','tires_squal_loop.wav',True,.50,'Car Tire Squeal Skid Loop','audible-edge (Tom Haigh); loop edit by qubodup','CC BY 3.0','https://opengameart.org/content/car-tire-squeal-skid-loop'),
 ('vehicle_door_open','door_opening.wav',False,.64,'CarDoorSfx (opening)','looneybits','CC0 1.0','https://opengameart.org/content/cardoorsfx'),
 ('vehicle_door_close','door_closing.wav',False,.64,'CarDoorSfx (closing)','looneybits','CC0 1.0','https://opengameart.org/content/cardoorsfx'),
 ('vehicle_ignition','engine_start_up_01.wav',False,.64,'Car Engine Start Up 02','looneybits','CC0 1.0','https://opengameart.org/content/car-engine-start-up-02')]
meta=(dest/'bodyhit0.wav.meta').read_text();report=[];preview=[]
for name,path,loop,peak,title,author,license,url in items:
 source=provenance/'originals'/path; x,sr=sf.read(source,always_2d=True,dtype='float64');channels=x.shape[1];duration=len(x)/sr
 x=x.mean(axis=1);x-=x.mean();common=math.gcd(sr,48000);x=resample_poly(x,48000//common,sr//common);sr=48000
 transformations=['arithmetic channel mean to mono','DC removal','scipy resample_poly to 48000 Hz']
 if loop:
  n=round(.06*sr);w=np.linspace(0,1,n);x=np.concatenate([x[n:-n],x[-n:]*(1-w)+x[:n]*w]);transformations.append('60 ms linear overlap crossfade with seam rotation; output duration reduced by 60 ms')
 else:
  active=np.flatnonzero(np.abs(x)>max(np.max(np.abs(x))*.002,1e-6));start=max(0,int(active[0]) - round(.015*sr));end=min(len(x),int(active[-1])+round(.04*sr)+1);x=x[start:end]
  n=min(round(.004*sr),len(x)//8);x[:n]*=np.linspace(0,1,n);x[-n:]*=np.linspace(1,0,n)
  transformations.append('trim silence at 0.2% of peak retaining 15 ms lead and 40 ms tail; 4 ms linear edge ramps')
 x*=peak/np.max(np.abs(x));transformations.append(f'constant peak gain to {peak}; PCM signed 16-bit')
 out=dest/(name+'.wav');sf.write(out,x,sr,subtype='PCM_16')
 final,_=sf.read(out);assert np.isfinite(final).all() and np.max(np.abs(final))<.66 and len(final)>100
 delta=np.abs(np.diff(final));boundary=abs(final[-1]-final[0]);assert not loop or boundary<=float(np.quantile(delta,.999))+1e-4
 assert loop or final[0]==final[-1]==0
 m=out.with_suffix('.wav.meta')
 if not m.exists(): m.write_text(meta.replace('a2e398a8970b453e86f3180ce7149daa',uuid.uuid4().hex).replace('compressionFormat: 1','compressionFormat: 0').replace('sampleRateOverride: 44100','sampleRateOverride: 48000'),encoding='utf-8')
 shutil.copy2(out,provenance/'runtime'/out.name)
 report.append(dict(runtime=out.name,source=source.relative_to(provenance).as_posix(),sourceSHA256=sha(source),runtimeSHA256=sha(out),title=title,author=author,license=license,licenseURL='https://creativecommons.org/licenses/by/3.0/' if license=='CC BY 3.0' else 'https://creativecommons.org/publicdomain/zero/1.0/',url=url,changes=transformations,originalChannels=channels,originalSeconds=duration,seconds=len(final)/sr,sampleRate=sr,channels=1,peak=float(np.max(np.abs(final))),rms=float(np.sqrt(np.mean(final**2))),boundaryDelta=float(boundary),intraSignalDelta99_9=float(np.quantile(delta,.999)),loop=loop))
 preview.extend([final,np.zeros(sr//2)])
 print(name,round(len(final)/sr,3),'seconds',flush=True)
sf.write(qa/'vehicle-audio-preview.wav',np.concatenate(preview),48000,subtype='PCM_16')
record={'version':'0.8.23','toolVersions':{'numpy':np.__version__,'soundfile':sf.__version__},'files':report,'notes':'Engine author explicitly describes a car recording. Door/starter and tire sources are licensed vehicle foley; not all are documented field recordings. Generic sound design, not authentic pickup model audio. Human listening review remains outstanding.'}
(provenance/'runtime-sources.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
(qa/'audio-signal-checks.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
shutil.copy2(__file__,provenance/'process_vehicle_audio_v0823.py')
for name in ['SFVehicleAudio','SFVehicleAudioState']:
 p=r/'UnityProject/Assets/SF/Runtime'/(name+'.cs.meta')
 if not p.exists():p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
credits='''VEHICLE SOUND EFFECTS

Car engine loop (96kHz, 4s) — Iwan "qubodup" Gabovitch / CC BY 3.0
https://opengameart.org/content/car-engine-loop-96khz-4s

Car Tire Squeal Skid Loop — audible-edge (Tom Haigh); loop edit by qubodup / CC BY 3.0
https://opengameart.org/content/car-tire-squeal-skid-loop
https://creativecommons.org/licenses/by/3.0/

CarDoorSfx (opening/closing) and Car Engine Start Up 02 — looneybits / CC0 1.0
https://opengameart.org/content/cardoorsfx
https://opengameart.org/content/car-engine-start-up-02
https://creativecommons.org/publicdomain/zero/1.0/

Changes: mono mix, DC removal, 48 kHz resampling, loop overlap crossfades or silence trim/edge fades, peak control, runtime pitch and volume. Generic vehicle foley; not exact vehicle-model recordings. Creators do not endorse this game. CC BY sounds retain their license freedoms; standalone original and edited WAVs accompany the portable build in AudioLicenses/Vehicle-v0823.

'''
p=r/'UnityProject/Assets/SF/Resources/AudioCredits.txt';s=p.read_text();assert 'VEHICLE SOUND EFFECTS' not in s;p.write_text(s.replace('GAME\n',credits+'GAME\n'),encoding='utf-8')
doc='# Vehicle audio — 0.8.23\n\n'+credits+'\nEngine source is documented as a car recording. The other source descriptions do not establish the recording method; they are credited as vehicle effects. None are presented as recordings of the depicted pickup. No firearm sounds changed.\n\nThe runtime source table includes file hashes and exact transformations. Originals, license texts and source-page snapshots are preserved beside it. Preview order: engine, tires, door opening, door closing, ignition. Objective signal checks pass; listening on user speakers/headphones is still needed.\n'
for p in [r/'Audio-Credits-v0.8.23.md',r/'UnityProject/Documentation/Audio-Credits-v0.8.23.md',provenance/'README.md']:p.write_text(doc,encoding='utf-8')
