using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace StefanieAndFernando.Editor
{
    [Serializable] public class SFSheetLayout
    {
        public int columns=4,rows=2,top=0,bottom=0,captionBand=0;
        public float worldHeight=2.8f,groundFraction=0;
        public float pixelsPerUnit=0;
        // Optional plate-only runtime cap; archival source and calibrated character sheets stay intact.
        public int maxDimension=0;
        public string anchor="feet",mask="auto";
        // Optional normalized row boundaries, top to bottom. Keeps irregular atlases explicit.
        public float[] rowCuts;
        // Explicit printed-label rectangles in source pixels, measured from the top left.
        public RectInt[] excludeTopLeft;
        // Authored horizontal pelvis anchors, in source pixels; prevents stance changes sliding the torso.
        public float[] rootX;
    }

    public static class SFArtImporter
    {
        const string Root="Assets/SF/";
        static string Cutouts=>Path.Combine(SFBuild.EvidenceRoot,"Cutouts");
        public static void ImportAll()=>ImportSources(null,Path.Combine(SFBuild.EvidenceRoot,"import-report.txt"));
        // Additive review batches must not reprocess unrelated, already verified artwork.
        public static void ImportOnly(string[] ids,string reportPath)=>ImportSources(new HashSet<string>(ids),reportPath);
        static void ImportSources(HashSet<string> selected,string reportPath)
        {
            Directory.CreateDirectory(Cutouts);
            var report=new List<string>{"SF importer — whole-figure packing; rectangular depth cells preserve visible pixels and world registration. Source images are never modified."};
            foreach(var path in Directory.GetFiles(Root+"ArtSource","*.png"))
            {
                string source=path.Replace('\\','/'),id=Path.GetFileNameWithoutExtension(path);
                if(selected!=null&&!selected.Contains(id))continue;
                bool plate=id.StartsWith("curitiba_")||id.StartsWith("plate_");
                string layoutPath=Path.ChangeExtension(path,"json");
                bool explicitLayout=File.Exists(layoutPath);
                var layout=explicitLayout?JsonUtility.FromJson<SFSheetLayout>(File.ReadAllText(layoutPath)):new SFSheetLayout();
                if(!explicitLayout&&(id=="latch"||id=="keel"||id=="vesper")){layout.mask="gray";layout.captionBand=70;}
                if(id=="vesper")layout.worldHeight=2.9f;
                string hash;
                string layoutHash=JsonUtility.ToJson(layout);
                if(layout.maxDimension==0)layoutHash=layoutHash.Replace(",\"maxDimension\":0","");
                using(var sha=System.Security.Cryptography.SHA256.Create())hash="mask-v3.2:"+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))+layoutHash;
                bool compactDepth=IsDepthSheet(id);
                if(compactDepth)hash+=":rectangular-depth-v1";
                string output=Root+"Resources/SF/"+id+".asset";
                var art=AssetDatabase.LoadAssetAtPath<SFArt>(output);
                if(art!=null&&art.sourceHash==hash&&art.frames!=null&&art.frames.Length>0&&art.frames.All(s=>s!=null&&s.texture!=null))
                {report.Add(id+": unchanged, "+art.frames.Length+" validated cached frames");continue;}
                // Decode the full PNG directly; Unity texture-size limits must not shift authored crop coordinates.
                var input=new Texture2D(2,2,TextureFormat.RGBA32,false);
                if(!input.LoadImage(File.ReadAllBytes(path)))throw new Exception("Cannot decode "+source);
                if(plate&&layout.maxDimension>0&&Mathf.Max(input.width,input.height)>layout.maxDimension)
                {
                    float scale=layout.maxDimension/(float)Mathf.Max(input.width,input.height);
                    int rw=Mathf.RoundToInt(input.width*scale),rh=Mathf.RoundToInt(input.height*scale);
                    var resized=new Texture2D(rw,rh,TextureFormat.RGBA32,false);var colors=new Color[rw*rh];
                    for(int y=0;y<rh;y++)for(int x=0;x<rw;x++)colors[y*rw+x]=input.GetPixelBilinear((x+.5f)/rw,(y+.5f)/rh);
                    resized.SetPixels(colors);resized.Apply();UnityEngine.Object.DestroyImmediate(input);input=resized;
                }
                int w=input.width,h=input.height;
                var pixels=input.GetPixels32();
                input.name=id+"_clean";input.wrapMode=TextureWrapMode.Clamp;input.filterMode=FilterMode.Bilinear;
                var rectangles=new List<RectInt>();var pivots=new List<Vector2>();
                bool alpha=pixels.Count(p=>p.a<16)>pixels.Length/100;
                if(plate){rectangles.Add(new RectInt(0,0,w,h));pivots.Add(new Vector2(.5f,.5f));}
                else if(explicitLayout)
                {
                    var packed=PackWholeFigures(pixels,w,h,alpha,layout,id,compactDepth,rectangles,pivots,report);
                    UnityEngine.Object.DestroyImmediate(input);input=packed;
                }
                else for(int row=0;row<layout.rows;row++)for(int col=0;col<layout.columns;col++)
                {
                    var cell=new RectInt(col*w/layout.columns,(layout.rows-1-row)*h/layout.rows,(col+1)*w/layout.columns-col*w/layout.columns,h/layout.rows);
                    CleanCell(pixels,w,cell,alpha,layout,out var bounds,out var pivot);rectangles.Add(bounds);pivots.Add(pivot);
                }
                if(!explicitLayout||plate){input.SetPixels32(pixels);input.Apply();}
                if(explicitLayout)File.WriteAllBytes(Path.Combine(Cutouts,id+".png"),input.EncodeToPNG());
                // A rejected mask/layout must leave the previous imported asset intact.
                // Replace children only after the complete source has packed successfully.
                if(art!=null){foreach(var child in AssetDatabase.LoadAllAssetsAtPath(output))if(child!=art)UnityEngine.Object.DestroyImmediate(child,true);}
                else {art=ScriptableObject.CreateInstance<SFArt>();AssetDatabase.CreateAsset(art,output);}
                AssetDatabase.AddObjectToAsset(input,art);
                art.frames=new Sprite[rectangles.Count];
                float ppu=plate?100:layout.pixelsPerUnit>0?layout.pixelsPerUnit:(h-layout.top-layout.bottom)/(float)layout.rows*.94f/layout.worldHeight;
                for(int i=0;i<art.frames.Length;i++)
                {
                    var b=rectangles[i];art.frames[i]=Sprite.Create(input,new Rect(b.x,b.y,b.width,b.height),pivots[i],ppu,0,SpriteMeshType.FullRect);
                    art.frames[i].name=id+"_"+i;AssetDatabase.AddObjectToAsset(art.frames[i],art);
                }
                art.source=source;art.sourceHash=hash;
                input.Apply(false,true);EditorUtility.SetDirty(art);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllLines(reportPath,report);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }

        sealed class Figure
        {
            public List<int> pixels=new List<int>();public int minX=int.MaxValue,minY=int.MaxValue,maxX,maxY;public float rootX,rootY;
            public void Add(int index,int w){pixels.Add(index);int x=index%w,y=index/w;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
        }
        [Serializable] sealed class FrameRecord {public RectInt sourceBounds,atlasBounds;public Vector2 sourceRoot,pivot;public int pixelCount;}
        // Depth sheets are rendered as whole sprites. Keep side rigs, props and their calibrated bounds unchanged.
        static bool IsDepthSheet(string id)=>(id.StartsWith("fernando_")||id.StartsWith("stefanie_"))&&(id.Contains("_north")||id.Contains("_south"));
        [Serializable] sealed class AtlasRecord {public int cellSize,cellWidth,cellHeight,columns,rows;public string packing;public FrameRecord[] frames;}
        static Texture2D PackWholeFigures(Color32[] p,int w,int h,bool alpha,SFSheetLayout layout,string id,bool compactDepth,List<RectInt> rects,List<Vector2> pivots,List<string> report)
        {
            // Mask before assigning figures. A rough grid is only a label for whole components;
            // it never cuts through a boot, hair strand or the next row's head.
            for(int y=h-layout.top;y<h;y++)for(int x=0;x<w;x++)p[y*w+x]=new Color32(0,0,0,0);
            if(layout.excludeTopLeft!=null)foreach(var excluded in layout.excludeTopLeft)
                for(int y=Math.Max(0,excluded.yMin);y<Math.Min(h,excluded.yMax);y++)
                    for(int x=Math.Max(0,excluded.xMin);x<Math.Min(w,excluded.xMax);x++)p[(h-1-y)*w+x]=new Color32(0,0,0,0);
            var mask=new SFSheetLayout{mask=layout.mask};
            CleanCell(p,w,new RectInt(0,0,w,h),alpha,mask,out var ignoredBounds,out var ignoredPivot);
            var figures=Enumerable.Range(0,layout.columns*layout.rows).Select(_=>new Figure()).ToArray();
            var seen=new bool[p.Length];var queue=new Queue<int>();
            for(int start=0;start<p.Length;start++)
            {
                if(seen[start]||p[start].a<12)continue;
                var component=new List<int>();long sx=0,sy=0;seen[start]=true;queue.Enqueue(start);
                while(queue.Count>0)
                {
                    int i=queue.Dequeue(),x=i%w,y=i/w;component.Add(i);sx+=x;sy+=y;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {int nx=x+dx,ny=y+dy;if(nx<0||nx>=w||ny<0||ny>=h)continue;int j=ny*w+nx;if(!seen[j]&&p[j].a>=12){seen[j]=true;queue.Enqueue(j);}}
                }
                float cx=sx/(float)component.Count,cy=sy/(float)component.Count,topY=h-1-cy;
                int col=Mathf.Clamp((int)(cx/w*layout.columns),0,layout.columns-1);
                int row=Mathf.Clamp((int)((topY-layout.top)/(h-layout.top-layout.bottom)*layout.rows),0,layout.rows-1);
                if(layout.rowCuts!=null)for(int r=0;r<layout.rows;r++)if(topY>=layout.rowCuts[r]*h&&topY<layout.rowCuts[r+1]*h){row=r;break;}
                float rowTop=layout.rowCuts!=null?layout.rowCuts[row]*h:layout.top+row*(h-layout.top-layout.bottom)/(float)layout.rows;
                bool caption=layout.captionBand>0&&topY-rowTop<layout.captionBand&&cx-col*w/(float)layout.columns<w/(float)layout.columns*.16f;
                if(caption){foreach(int i in component)p[i]=new Color32(0,0,0,0);continue;}
                foreach(int i in component)figures[row*layout.columns+col].Add(i,w);
            }
            foreach(var f in figures)
            {
                if(f.pixels.Count<100)throw new Exception(id+": whole-figure assignment produced an empty cell.");
                int band=Math.Max(4,(f.maxY-f.minY)/18);
                var feet=f.pixels.Where(i=>i/w<=f.minY+band).ToArray();
                f.rootX=layout.anchor=="bounds"?(f.minX+f.maxX)*.5f:feet.Average(i=>(float)(i%w));f.rootY=f.minY;
            }
            if(layout.anchor=="center")for(int row=0;row<layout.rows;row++)
            {
                float ground=figures.Skip(row*layout.columns).Take(layout.columns).Min(f=>f.minY);
                for(int col=0;col<layout.columns;col++){var f=figures[row*layout.columns+col];f.rootX=(col+.5f)*w/layout.columns;f.rootY=ground;}
            }
            if(layout.rootX!=null&&layout.rootX.Length==figures.Length)
                for(int i=0;i<figures.Length;i++)figures[i].rootX=layout.rootX[i];
            int left=Mathf.CeilToInt(figures.Max(f=>f.rootX-f.minX))+10,right=Mathf.CeilToInt(figures.Max(f=>f.maxX-f.rootX))+10;
            int below=Mathf.CeilToInt(figures.Max(f=>f.rootY-f.minY))+10,above=Mathf.CeilToInt(figures.Max(f=>f.maxY-f.rootY))+10;
            int cell=Mathf.CeilToInt(Mathf.Max(left+right,below+above)/16f)*16;
            int cellWidth=compactDepth?Mathf.CeilToInt((left+right)/16f)*16:cell;
            int cellHeight=compactDepth?Mathf.CeilToInt((below+above)/16f)*16:cell;
            int aw=cellWidth*layout.columns,ah=cellHeight*layout.rows;
            // cellSize is the legacy maximum span; consumers must use cellWidth/Height for rectangular atlases.
            var atlas=new Color32[aw*ah];var record=new AtlasRecord{cellSize=cell,cellWidth=cellWidth,cellHeight=cellHeight,packing=compactDepth?"rectangular-depth-v1":"square-v3.2",columns=layout.columns,rows=layout.rows,frames=new FrameRecord[figures.Length]};
            for(int n=0;n<figures.Length;n++)
            {
                var f=figures[n];int ox=n%layout.columns*cellWidth,oy=(layout.rows-1-n/layout.columns)*cellHeight;
                // Round the translation once: rounding each half-integer pixel separately can merge adjacent pixels.
                int rootX=Mathf.RoundToInt(f.rootX),rootY=Mathf.RoundToInt(f.rootY);
                foreach(int i in f.pixels)
                {
                    int x=left+i%w-rootX,y=below+i/w-rootY;
                    if(x<0||x>=cellWidth||y<0||y>=cellHeight)throw new Exception(id+": repack would clip a figure.");
                    atlas[(oy+y)*aw+ox+x]=p[i];
                }
                var rect=new RectInt(ox,oy,cellWidth,cellHeight);var pivot=new Vector2(left/(float)cellWidth,below/(float)cellHeight);
                if(layout.anchor=="bounds"){rect=new RectInt(ox+left+f.minX-rootX,oy+below+f.minY-rootY,f.maxX-f.minX+1,f.maxY-f.minY+1);pivot=new Vector2(.5f,0);}
                rects.Add(rect);pivots.Add(pivot);
                record.frames[n]=new FrameRecord{sourceBounds=new RectInt(f.minX,f.minY,f.maxX-f.minX+1,f.maxY-f.minY+1),atlasBounds=rect,sourceRoot=new Vector2(f.rootX,f.rootY),pivot=pivot,pixelCount=f.pixels.Count};
                report.Add(id+" ["+n+"] "+f.pixels.Count+" pixels, packed="+rect+" pivot="+pivot);
            }
            Directory.CreateDirectory(Path.Combine(Cutouts,"SourceSpace"));
            var cleaned=new Texture2D(w,h,TextureFormat.RGBA32,false);cleaned.SetPixels32(p);cleaned.Apply();File.WriteAllBytes(Path.Combine(Cutouts,"SourceSpace",id+".png"),cleaned.EncodeToPNG());UnityEngine.Object.DestroyImmediate(cleaned);
            File.WriteAllText(Path.Combine(Cutouts,id+".json"),JsonUtility.ToJson(record,true));
            var result=new Texture2D(aw,ah,TextureFormat.RGBA32,false);result.name=id+"_packed";result.wrapMode=TextureWrapMode.Clamp;result.filterMode=FilterMode.Bilinear;result.SetPixels32(atlas);result.Apply();return result;
        }

        public static void CleanCell(Color32[] pixels,int stride,RectInt cell,bool alpha,SFSheetLayout layout,out RectInt bounds,out Vector2 pivot)
        {
            int w=cell.width,h=cell.height,n=w*h;
            var removed=new bool[n];var queue=new Queue<int>();
            Func<int,int> at=i=>(cell.y+i/w)*stride+cell.x+i%w;
            Func<int,bool> background=i=>{
                var c=pixels[at(i)];if(c.a<12)return true;
                // Trust authored alpha. Re-keying white garments would damage an already cleaned source.
                if(alpha&&layout.mask=="auto")return false;
                int min=Math.Min(c.r,Math.Min(c.g,c.b)),max=Math.Max(c.r,Math.Max(c.g,c.b));
                bool cyan=c.g>170&&c.b>170&&c.g-c.r>8&&c.b-c.r>8&&Math.Abs(c.g-c.b)<35;
                // Reviewed white-clothing sheets need a tighter backdrop threshold than
                // general automatic cleanup; the broad threshold eats pale waist/leg fabric.
                if(layout.mask=="white-strict")return min>235&&max-min<6;
                return layout.mask=="cyan"?cyan:layout.mask=="gray"?min>160&&max-min<24:cyan||(min>235&&max-min<22);
            };
            Action<int> seed=i=>{if(!removed[i]&&background(i)){removed[i]=true;queue.Enqueue(i);}};
            for(int x=0;x<w;x++){seed(x);seed((h-1)*w+x);}for(int y=0;y<h;y++){seed(y*w);seed(y*w+w-1);}
            // Explicit cyan profiles can remove enclosed backdrop gaps without keying white clothing.
            if(layout.mask=="cyan")for(int i=0;i<n;i++)seed(i);
            while(queue.Count>0){int i=queue.Dequeue(),x=i%w,y=i/w;if(x>0)seed(i-1);if(x+1<w)seed(i+1);if(y>0)seed(i-w);if(y+1<h)seed(i+w);}
            var seen=new bool[n];var components=new List<List<int>>();
            for(int start=0;start<n;start++)
            {
                if(removed[start]||seen[start])continue;
                var component=new List<int>();queue.Enqueue(start);seen[start]=true;
                while(queue.Count>0)
                {
                    int i=queue.Dequeue(),x=i%w,y=i/w;component.Add(i);
                    // Eight-way connectivity preserves antialiased diagonal fingers and equipment straps.
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {int nx=x+dx,ny=y+dy;if(nx<0||nx>=w||ny<0||ny>=h)continue;int j=ny*w+nx;if(!seen[j]&&!removed[j]){seen[j]=true;queue.Enqueue(j);}}
                }
                components.Add(component);
            }
            if(components.Count==0)throw new Exception("Empty cell "+cell);
            int largest=components.Max(c=>c.Count),minx=w,miny=h,maxx=0,maxy=0;
            var keep=new bool[n];
            foreach(var component in components)
            {
                if(component.Count<Math.Max(12,largest/1800))continue;
                // Printed corner numbers are excluded by a narrow authored caption region, not by deleting every detached object.
                if(layout.captionBand>0&&component.All(i=>i/w>h-layout.captionBand&&i%w<w*.16f))continue;
                foreach(int i in component){keep[i]=true;minx=Math.Min(minx,i%w);maxx=Math.Max(maxx,i%w);miny=Math.Min(miny,i/w);maxy=Math.Max(maxy,i/w);}
            }
            if(maxx<minx||maxy<miny)throw new Exception("No retained pixels in "+cell);
            float footSum=0,footCount=0;int band=Math.Max(4,(maxy-miny)/18);
            for(int i=0;i<n;i++)
            {
                if(!keep[i]){var c=pixels[at(i)];c.a=0;pixels[at(i)]=c;}
                else if(i/w<=miny+band){footSum+=i%w;footCount++;}
            }
            float px=layout.anchor=="center"?w*.5f:layout.anchor=="bounds"?(minx+maxx)*.5f:footSum/Math.Max(1,footCount);
            float py=layout.groundFraction>0?h*layout.groundFraction:miny;
            // Full cell bounds allow pivots below an airborne foot; no artificial per-frame rescaling.
            if(layout.anchor=="bounds")
            {
                bounds=new RectInt(cell.x+minx,cell.y+miny,maxx-minx+1,maxy-miny+1);pivot=new Vector2(.5f,0);
            }
            else {bounds=cell;pivot=new Vector2(px/w,py/h);}
        }
    }
}
