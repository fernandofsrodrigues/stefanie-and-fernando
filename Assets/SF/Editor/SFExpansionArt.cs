using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace StefanieAndFernando.Editor
{
    // This is an additive art preparation pass, independent of chapter gameplay and builds.
    // Aliases share original Sprite/Texture objects; no duplicate bitmap or per-pose scaling.
    public static class SFExpansionArt
    {
        const string Folder="Assets/SF/Resources/SF/";
        static string Report { get { string path=Path.Combine(SFBuild.EvidenceRoot,"ExpansionArt");Directory.CreateDirectory(path);return path+Path.DirectorySeparatorChar; } }
        static readonly string[] Ids={"smuggler","smuggler_shotgun","raider","merc_boss","armored_boss"};
        [Serializable] sealed class Mapping {public string resource,source;public int[] frames;}
        [Serializable] sealed class Evidence {public string status="Art preparation only; chapter integration and gameplay pending";public Mapping[] mappings;public string[] checks;}

        public static void Prepare()
        {
            Directory.CreateDirectory(Report);
            SFArtImporter.ImportOnly(Ids.Select(id=>"exp_"+id+"_sheet").Concat(new[]{"plate_rio_redeemer","plate_desert_outskirts"}).ToArray(),Report+"import-report.txt");
            Compose();
            RenderReview();
            ValidateRejectedLayout();
            SFBuild.Validate();
            File.Copy(Path.Combine(SFBuild.EvidenceRoot,"editmode-checks.txt"),Report+"legacy-editmode-checks.txt");
        }

        public static void Compose()
        {
            var mappings=new List<Mapping>();var checks=new List<string>();
            var cloth=Enumerable.Repeat(new Color32(247,247,248,255),3600).ToArray();
            for(int y=10;y<50;y++)for(int x=20;x<40;x++)cloth[y*60+x]=new Color32(253,248,239,255);
            SFArtImporter.CleanCell(cloth,60,new RectInt(0,0,60,60),false,new SFSheetLayout{mask="white-strict"},out var maskBounds,out var maskPivot);
            Require(cloth[0].a==0,"Neutral off-white background is removed",checks);
            Require(cloth[30*60+39].a==255,"Warm white trouser edge remains opaque",checks);
            foreach(string id in Ids)
            {
                string source="exp_"+id+"_sheet";var art=Load(source);
                Require(art.frames.Length==24,source+" has all 24 authored key poses",checks);
                Require(art.frames.All(s=>s!=null&&s.pixelsPerUnit>0),source+" has valid frames and calibrated pixel scale",checks);
                bool gun=id=="smuggler_shotgun";
                // Claude's final identity contract: latch, keel, silk, cantilever, foreman.
                // Different actor layouts read different base slots for contact and defeat.
                int[] basis=id=="raider"?new[]{0,22,12,0,15,1,12,0}:
                    id=="merc_boss"?new[]{1,13,15,1,20,12,22,1}:
                    id=="armored_boss"?new[]{0,12,13,13,1,20,22,1}:
                    gun?new[]{0,4,5,12,1,2,20,22}:new[]{4,5,6,7,1,13,14,22};
                Alias(id,source,basis,mappings);
                Alias(id+"_walk",source,new[]{4,5,6,7},mappings);
                Alias(id+"_idle",source,Enumerable.Repeat(gun?0:1,8).ToArray(),mappings);
                // Current enemy contact clocks (.48/.85 and .7/1.25) both enter slot 4.
                // Hold anticipation before the authored contact pose; don't imply eight unique drawings.
                Alias(id+"_strike",source,gun?new[]{0,0,1,1,1,1,0,0}:new[]{1,12,12,12,13,13,12,1},mappings);
                Alias(id+"_reaction",source,new[]{20,20,21,22,21,23,20,22},mappings);
                if(gun)Alias(id+"_reload",source,new[]{0,3,3,0},mappings);
            }
            Require(Ids.Select(id=>Load("exp_"+id+"_sheet").Frame(0).texture).Distinct().Count()==Ids.Length,"Each enemy source has its own texture",checks);
            // Existing campaign sources already contain four requested Rio plates. Reuse
            // their original sprites; the extra hillside courtyard gives six distinct scenes.
            string[] rio={"plate_2_0","plate_2_1","plate_2_5","plate_2_2","plate_2_3","plate_rio_redeemer"};
            for(int i=0;i<rio.Length;i++)Alias("rio_course_"+i,rio[i],new[]{0},mappings);
            for(int i=0;i<6;i++)Alias("anbar_course_"+i,"plate_5_"+i,new[]{0},mappings);
            string[] desert={"plate_desert_outskirts","plate_5_2","plate_5_3"};
            for(int i=0;i<desert.Length;i++)Alias("desert_drive_"+i,desert[i],new[]{0},mappings);
            Alias("plate_desert_convoy","plate_desert_outskirts",new[]{0},mappings);
            Alias("plate_desert_arrival","plate_5_3",new[]{0},mappings);
            AssetDatabase.SaveAssets();
            foreach(var map in mappings)
            {
                var source=Load(map.source);var result=Load(map.resource);
                Require(result.frames.Length==map.frames.Length&&result.frames.Select((s,i)=>s==source.frames[map.frames[i]]).All(b=>b),map.resource+" shares the exact selected source sprites",checks);
                Require(AssetDatabase.LoadAllAssetsAtPath(Folder+map.resource+".asset").Length==1,map.resource+" adds no duplicate texture",checks);
            }
            File.WriteAllText(Report+"mapping.json",JsonUtility.ToJson(new Evidence{mappings=mappings.ToArray(),checks=checks.ToArray()},true));
            Debug.Log("SF Expansion art mappings: "+checks.Count+" checks passed; "+mappings.Count+" additive aliases. No gameplay verification claimed.");
        }

        static SFArt Load(string name)
        {
            var art=AssetDatabase.LoadAssetAtPath<SFArt>(Folder+name+".asset");
            if(art==null||art.frames==null||art.frames.Length==0)throw new InvalidOperationException("Missing expansion art: "+name);
            return art;
        }
        static void ValidateRejectedLayout()
        {
            const string id="exp_smuggler_sheet";
            string layoutPath="Assets/SF/ArtSource/"+id+".json";
            byte[] original=File.ReadAllBytes(layoutPath);var before=Load(id);
            string priorHash=before.sourceHash;var priorFrames=before.frames.ToArray();
            bool rejected=false;
            try
            {
                string invalid=System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(layoutPath),"\"rows\"\\s*:\\s*6","\"rows\": 128");
                File.WriteAllText(layoutPath,invalid);
                try {SFArtImporter.ImportOnly(new[]{id},Report+"invalid-layout-import.txt");}
                catch(Exception error) when(error.Message.Contains("whole-figure assignment produced an empty cell")){rejected=true;}
            }
            finally {File.WriteAllBytes(layoutPath,original);}
            if(!rejected||before.sourceHash!=priorHash||!before.frames.SequenceEqual(priorFrames)||before.frames.Any(s=>s==null||s.texture==null))
                throw new InvalidOperationException("A rejected layout changed the previous valid import");
            File.WriteAllText(Report+"rejected-layout-regression.txt","PASS: Deliberately invalid 128-row layout rejected.\nPASS: Previous source hash and all 24 sprite/texture references retained.\nPASS: Original layout bytes restored.\n");
        }
        static void Require(bool condition,string message,List<string> checks)
        {if(!condition)throw new InvalidOperationException(message);checks.Add("PASS: "+message);}
        static void Alias(string name,string source,int[] frames,List<Mapping> mappings)
        {
            var parent=Load(source);string path=Folder+name+".asset";
            if(frames.Any(i=>i<0||i>=parent.frames.Length))throw new InvalidOperationException(name+": invalid frame selection");
            var result=AssetDatabase.LoadAssetAtPath<SFArt>(path);
            if(result==null){result=ScriptableObject.CreateInstance<SFArt>();AssetDatabase.CreateAsset(result,path);}
            result.source=parent.source;result.sourceHash=parent.sourceHash+"|expansion-alias-v1:"+string.Join(",",frames);
            result.frames=frames.Select(i=>parent.frames[i]).ToArray();EditorUtility.SetDirty(result);
            mappings.Add(new Mapping{resource=name,source=source,frames=frames});
        }

        public static void RenderReview()
        {
            Directory.CreateDirectory(Report);
            var scene=EditorSceneManager.NewPreviewScene();
            var cameraObject=new GameObject("Expansion art review camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5.625f;
            camera.scene=scene;
            camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.17f);
            var target=new RenderTexture(3840,2160,24);camera.targetTexture=target;
            var sprites=new List<SpriteRenderer>();var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(Folder+"Unlit.mat"));
            try
            {
                foreach(string phase in new[]{"idle","walk","walk1","walk2","walk3","strike","reaction","reload","base-idle","base-windup","base-hit","base-down"})
                {
                    foreach(var previous in sprites)UnityEngine.Object.DestroyImmediate(previous.gameObject);sprites.Clear();
                    for(int i=0;i<Ids.Length;i++)
                    {
                        string kind=phase.StartsWith("walk")?"walk":phase;
                        string resource=phase.StartsWith("base-")?Ids[i]:Ids[i]+"_"+(kind=="reload"&&Ids[i]!="smuggler_shotgun"?"idle":kind);
                        var art=Load(resource);int frame=phase=="strike"?4:phase=="reaction"?3:phase=="reload"?1:phase.StartsWith("walk")&&phase.Length==5?phase[4]-'0':0;
                        if(phase.StartsWith("base-"))
                        {
                            int[] slots=phase=="base-idle"?new[]{4,0,0,0,0}:
                                phase=="base-windup"?new[]{4,4,2,0,1}:
                                phase=="base-hit"?new[]{5,5,4,2,2}:new[]{7,7,1,6,6};
                            frame=slots[i];
                        }
                        var go=new GameObject(resource);SceneManager.MoveGameObjectToScene(go,scene);
                        var sr=go.AddComponent<SpriteRenderer>();sr.sharedMaterial=material;sr.sprite=art.Frame(frame);
                        // No player loop runs between editor preview renders; explicitly bind
                        // the sprite texture so the last import cannot remain on the material.
                        var properties=new MaterialPropertyBlock();properties.SetTexture("_MainTex",sr.sprite.texture);sr.SetPropertyBlock(properties);
                        sr.transform.position=new Vector3(-7.5f+i*3.75f,-2,0);sprites.Add(sr);
                    }
                    camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
                    var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();
                    File.WriteAllBytes(Report+"unity-scale-"+phase+"-4k.png",image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;
                }
            }
            finally
            {
                camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(material);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
