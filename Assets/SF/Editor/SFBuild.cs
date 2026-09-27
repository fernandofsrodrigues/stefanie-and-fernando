using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

namespace StefanieAndFernando.Editor
{
    public static class SFBuild
    {
        const string Root = "Assets/SF/";
        static string ReleaseTag
        {
            get
            {
                string revision=Environment.GetEnvironmentVariable("SF_RELEASE_REVISION");
                if(string.IsNullOrEmpty(revision))return "0.8.47";
                if(!System.Text.RegularExpressions.Regex.IsMatch(revision,"^[a-z][a-z0-9-]*$"))throw new Exception("Invalid release revision");
                return "0.8.47-"+revision;
            }
        }
        static string evidenceRoot;
        // Each editor process owns fresh evidence, including importer diagnostics.
        public static string EvidenceRoot
        {
            get
            {
                if(evidenceRoot!=null)return evidenceRoot;
                string path=Environment.GetEnvironmentVariable("SF_BUILD_EVIDENCE");
                if(string.IsNullOrEmpty(path))path="../QA/Build-v0.8.47-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+Guid.NewGuid().ToString("N");
                path=Path.GetFullPath(path);
                if(Directory.Exists(path)||File.Exists(path))throw new Exception("Preserve existing build evidence: "+path);
                Directory.CreateDirectory(path);
                return evidenceRoot=path;
            }
        }
        [MenuItem("SF/1. Import selected art (preserves sources)")]
        public static void ImportArt(){SFArtImporter.ImportAll();SFDirectionalWalkArt.Compose();SFExpansionArt.Compose();}

        [MenuItem("SF/2. Create demo scene")]
        public static void Prepare()
        {
            ImportArt();
            // The SF scene is a saved copy of the supplied checkpoint, with the original Player motor reused at runtime.
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Single);
            new GameObject("SF Demo").AddComponent<SFGame>();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"Scenes/Curitiba.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"Scenes/Curitiba.unity",true)};
            PlayerSettings.companyName="Stefanie and Fernando";PlayerSettings.productName="Stefanie & Fernando";
            PlayerSettings.defaultScreenWidth=3840;PlayerSettings.defaultScreenHeight=2160;
            PlayerSettings.bundleVersion="0.8.47";
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;PlayerSettings.runInBackground=true;
            PlayerSettings.colorSpace=ColorSpace.Gamma;
            var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if(shader==null)shader=Shader.Find("Sprites/Default");
            if(AssetDatabase.LoadAssetAtPath<Material>(Root+"Resources/SF/Unlit.mat")==null)AssetDatabase.CreateAsset(new Material(shader),Root+"Resources/SF/Unlit.mat");
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback=true;
            PlayerSettings.WebGL.dataCaching=true;
            PlayerSettings.WebGL.template="APPLICATION:Default";
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            QualitySettings.vSyncCount=1; QualitySettings.antiAliasing=0;
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("SF/3. Validate gameplay rules and art")]
        public static void Validate()
        {
            var results=new List<string>();
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("FAILED: "+name);results.Add("PASS: "+name);};
            check(SFMath.WithinStrike(Vector2.zero,0,1,new Vector2(1,.2f),0,1.5f),"strike reaches aligned target");
            check(!SFMath.WithinStrike(Vector2.zero,0,1,new Vector2(-1,0),0,1.5f),"strike excludes target behind actor");
            check(!SFMath.WithinStrike(Vector2.zero,0,1,new Vector2(1,1.5f),0,1.5f),"strike respects depth lane");
            check(!SFMath.WithinStrike(Vector2.zero,0,1,Vector2.right,3,1.5f),"strike respects airborne height");
            foreach(string id in new[]{"fernando_move","stefanie_move","fernando_punch","stefanie_punch","fernando_kick","stefanie_kick","latch","keel","vesper"})
            {var art=Resources.Load<SFArt>("SF/"+id);check(art!=null&&art.frames.Length==8&&art.frames.All(s=>s!=null),id+" has eight imported frames");}
            foreach(string id in new[]{"fernando_run","stefanie_run","fernando_crouch","stefanie_crouch","fernando_strike","stefanie_strike","fernando_recoil","stefanie_recoil","fernando_rifle","stefanie_rifle","stefanie_sidearm"})
            {var art=Resources.Load<SFArt>("SF/"+id);check(art!=null&&art.frames.Length==8&&art.frames.All(s=>s!=null),id+" has eight new frames");}
            foreach(string id in new[]{"pickups_food","pickups_drinks","pickups_medical"})check(Resources.Load<SFArt>("SF/"+id).frames.Length==9,id+" uses nine-item layout");
            foreach(string id in new[]{"field_inner_leg_parts","civil_inner_leg_parts"})
            {
                var art=Resources.Load<SFArt>("SF/"+id);
                check(art!=null&&art.frames.Length==2&&art.frames.All(s=>s!=null),id+" has two dedicated inner-leg textures");
                var source=new Texture2D(2,2);source.LoadImage(File.ReadAllBytes(Root+"ArtSource/"+id+".png"));
                check(source.width==1536&&source.height==1024,id+" preserves calibrated 1536 by 1024 source canvas");
                UnityEngine.Object.DestroyImmediate(source);
            }
            // Regression fixture: detached boot/prop, enclosed white garment, cyan background.
            var pixels=Enumerable.Repeat(new Color32(210,243,245,255),10000).ToArray();
            for(int y=20;y<80;y++)for(int x=30;x<65;x++)pixels[y*100+x]=new Color32(50,40,35,255);
            for(int y=35;y<60;y++)for(int x=40;x<55;x++)pixels[y*100+x]=new Color32(250,250,250,255);
            for(int y=10;y<16;y++)for(int x=70;x<80;x++)pixels[y*100+x]=new Color32(45,40,40,255);
            for(int y=63;y<69;y++)for(int x=43;x<49;x++)pixels[y*100+x]=new Color32(210,243,245,255);
            SFArtImporter.CleanCell(pixels,100,new RectInt(0,0,100,100),false,new SFSheetLayout{mask="cyan"},out var bounds,out var pivot);
            check(pixels[0].a==0&&pixels[45*100+45].a==255,"cyan cleanup retains white garment pixels");
            check(pixels[12*100+75].a==255,"cleanup retains detached boot or prop component");
            check(pixels[65*100+45].a==0,"explicit cyan cleanup removes enclosed backdrop gaps");
            var preserved=Enumerable.Repeat(new Color32(0,0,0,0),10000).ToArray();
            for(int y=20;y<80;y++)for(int x=30;x<65;x++)preserved[y*100+x]=new Color32(255,255,255,255);
            SFArtImporter.CleanCell(preserved,100,new RectInt(0,0,100,100),true,new SFSheetLayout(),out bounds,out pivot);
            check(preserved[40*100+40].a==255,"existing transparency is not color-keyed again");
            File.WriteAllLines(Path.Combine(EvidenceRoot,"editmode-checks.txt"),results);
            Debug.Log("SF: "+results.Count+" validation checks passed.");
        }

        [MenuItem("SF/Build Windows")]
        public static void WindowsParty(){string root="../Windows-"+ReleaseTag;RequireFresh(root);Prepare();Build(BuildTarget.StandaloneWindows64,root+"/StefanieAndFernando.exe");}
        public static void Windows(){Prepare();Build(BuildTarget.StandaloneWindows64,"../Windows/StefanieAndFernando.exe");}
        [MenuItem("SF/Build Web")]
        public static void Web()
        {
            Prepare();
            // This local preview uses a Development build to bypass a reproducible LLVM -O3 crash.
            // The generic IL2CPP compiler setting does not override Web's optimization preset.
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL,UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            Build(BuildTarget.WebGL,"../Web");
        }
        public static void WebRelease()
        {
#if UNITY_WEBGL
            string output="../Candidate-"+ReleaseTag+"/Web";
            RequireFresh(output);
            Prepare();
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL,UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            UnityEditor.WebGL.UserBuildSettings.codeOptimization=UnityEditor.WebGL.WasmCodeOptimization.DiskSize;
            Build(BuildTarget.WebGL,output,true);
#else
            throw new Exception("Select the WebGL build target before creating a release Web build.");
#endif
        }
        static void RequireFresh(string path)
        {
            if(Directory.Exists(path))throw new Exception("Preserve existing build; stage a new version: "+path);
        }
        static void Build(BuildTarget target,string path,bool webRelease=false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"Scenes/Curitiba.unity"},locationPathName=path,target=target,options=target==BuildTarget.WebGL&&!webRelease?BuildOptions.Development:BuildOptions.None});
            File.WriteAllText(Path.Combine(EvidenceRoot,target+"-build.txt"),result.summary.result+"\nBytes: "+result.summary.totalSize+"\nErrors: "+result.summary.totalErrors+"\nWarnings: "+result.summary.totalWarnings);
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("SF build failed: "+result.summary.result);
            if(target==BuildTarget.WebGL)InstallWebShell(path);
        }
        public static void RefreshWebShell()=>InstallWebShell("../Web");
        static void InstallWebShell(string path)
        {
            var files=Directory.GetFiles(Path.Combine(path,"Build")).Select(Path.GetFileName).ToArray();
            var html=File.ReadAllText(Root+"Editor/SFWebShell.html");
            foreach(var pair in new[]{("LOADER","Web.loader.js"),("DATA","Web.data"),("FRAMEWORK","Web.framework.js"),("CODE","Web.wasm")})
                html=html.Replace("{{"+pair.Item1+"}}","Build/"+files.Single(f=>f.StartsWith(pair.Item2)&&!f.Contains("symbols")));
            File.WriteAllText(Path.Combine(path,"index.html"),html);
        }
    }
}
