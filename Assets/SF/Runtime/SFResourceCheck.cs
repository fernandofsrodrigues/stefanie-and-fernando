using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace StefanieAndFernando
{
    // Local-only cold A/B probe. Object counts are measured residency; RGBA bytes are
    // an estimate from resident texture dimensions, not a browser/GPU memory reading.
    public sealed class SFResourceCheck:MonoBehaviour
    {
        static bool NativeProbe=>Application.platform!=RuntimePlatform.WebGLPlayer&&Environment.GetCommandLineArgs().Contains("-sfResourceProbe");
        public static bool Requested=>NativeProbe||SFGame.LocalQAAllowed&&Application.absoluteURL.Contains("resources=");
        static bool Eager=>Requested&&(Environment.GetCommandLineArgs().Contains("-sfResourceEager")||Application.absoluteURL.Contains("resources=eager"));
        public static bool DeferFieldArt=>(Application.platform==RuntimePlatform.WebGLPlayer||NativeProbe)&&!Eager;
        [Serializable] sealed class Sample
        {
            public string phase,mode;public int textures,artObjects,fieldArtObjects;
            public long residentRgbaBytesEstimate,unityAllocatedBytes,unityReservedBytes,managedBytes;
            public string[] fieldSources;
        }
        void Record(string phase)
        {
            var textures=Resources.FindObjectsOfTypeAll<Texture2D>();
            var arts=Resources.FindObjectsOfTypeAll<SFArt>();
            var fields=arts.Where(a=>a.name.Contains("_field_")).OrderBy(a=>a.name).ToArray();
            Debug.Log("SF_RESOURCE "+JsonUtility.ToJson(new Sample{
                phase=phase,mode=DeferFieldArt?"deferred":"eager",textures=textures.Length,artObjects=arts.Length,fieldArtObjects=fields.Length,
                residentRgbaBytesEstimate=textures.Where(t=>t.format==TextureFormat.RGBA32||t.format==TextureFormat.ARGB32).Sum(t=>(long)t.width*t.height*4),
                unityAllocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),unityReservedBytes=Profiler.GetTotalReservedMemoryLong(),managedBytes=GC.GetTotalMemory(false),
                fieldSources=fields.Select(a=>a.name+":"+a.sourceHash).ToArray()}));
        }
        IEnumerator Start()
        {
            var g=GetComponent<SFGame>();yield return new WaitForSeconds(.3f);g.enabled=false;
            Record("cold-civil-menu");g.StartGame();yield return null;g.enabled=false;Record("civil-start");
            var clock=System.Diagnostics.Stopwatch.StartNew();
            foreach(var kit in g.Loadouts){kit.outfit=1;kit.startRifle=true;}
            g.ApplyLoadouts();clock.Stop();
            yield return null;Record("field-selected");
            bool rendered=g.Fernando.CurrentSheet.StartsWith("field_")&&g.Stefanie.CurrentSheet.StartsWith("field_");
            Debug.Log((rendered?"PASS: ":"FAIL: ")+"both deferred field rifles render after outfit selection; load ms="+clock.ElapsedMilliseconds);
            foreach(var kit in g.Loadouts){kit.outfit=0;kit.startRifle=false;}
            g.ApplyLoadouts();yield return null;Record("civil-return");
            bool civilian=!g.Fernando.CurrentSheet.StartsWith("field_")&&!g.Stefanie.CurrentSheet.StartsWith("field_");
            Debug.Log((civilian?"PASS: ":"FAIL: ")+"civilian outfit returns after deferred field selection");
            Debug.Log("SF_RESOURCE_COMPLETE");
            if(Application.platform!=RuntimePlatform.WebGLPlayer)Application.Quit(rendered&&civilian?0:1);
        }
    }
}
