using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StefanieAndFernando.Editor
{
    // Pair sources retain their own calibrated registration. Composite assets reference
    // those sprites directly, without resampling or merging their original bitmaps.
    public static class SFDirectionalWalkArt
    {
        public static void Compose()
        {
            const string folder="Assets/SF/Resources/SF/";
            foreach(string hero in new[]{"fernando","stefanie"})
            foreach(string direction in new[]{"north","south"})
            {
                string stem=hero+"_field_"+direction+"_walk_";
                var pairs=new[]{"contact_a","pass_a","contact_b","pass_b"}
                    .Select(phase=>AssetDatabase.LoadAssetAtPath<SFArt>(folder+stem+phase+".asset")).ToArray();
                if(pairs.Any(a=>a==null||a.frames==null||a.frames.Length!=2||a.frames.Any(s=>s==null)))
                    throw new InvalidOperationException("Incomplete directional walk sources: "+stem);
                string path=folder+hero+"_field_walk_"+direction+".asset";
                var result=AssetDatabase.LoadAssetAtPath<SFArt>(path);
                string hash=string.Join("|",pairs.Select(a=>a.sourceHash));
                if(result!=null&&result.sourceHash==hash&&result.frames!=null&&result.frames.Length==8&&result.frames.All(s=>s!=null))continue;
                if(result==null){result=ScriptableObject.CreateInstance<SFArt>();AssetDatabase.CreateAsset(result,path);}
                result.source=string.Join(";",pairs.Select(a=>a.source));result.sourceHash=hash;
                result.frames=new Sprite[8];
                for(int head=0;head<2;head++)for(int phase=0;phase<4;phase++)result.frames[head*4+phase]=pairs[phase].frames[head];
                EditorUtility.SetDirty(result);
            }
            foreach(string hero in new[]{"fernando","stefanie"})ComposeCivilPistol(folder,hero,"south");
            ComposeCivilPistol(folder,"fernando","north");
            AssetDatabase.SaveAssets();
        }
        static void ComposeCivilPistol(string folder,string hero,string direction)
        {
            string stem=hero+"_civil_pistol_"+direction;
            var pairs=new[]{"contact_a","pass_a","contact_b","pass_b"}
                .Select(phase=>AssetDatabase.LoadAssetAtPath<SFArt>(folder+stem+"_"+phase+".asset")).ToArray();
            if(pairs.Any(a=>a==null||a.frames==null||a.frames.Length!=2||a.frames.Any(s=>s==null)))
                throw new InvalidOperationException("Incomplete civilian pistol travel sources: "+stem);
            string path=folder+stem+".asset",hash=string.Join("|",pairs.Select(a=>a.sourceHash));
            var result=AssetDatabase.LoadAssetAtPath<SFArt>(path);
            if(result!=null&&result.sourceHash==hash&&result.frames!=null&&result.frames.Length==8&&result.frames.All(s=>s!=null))return;
            if(result==null){result=ScriptableObject.CreateInstance<SFArt>();AssetDatabase.CreateAsset(result,path);}
            result.source=string.Join(";",pairs.Select(a=>a.source));result.sourceHash=hash;result.frames=new Sprite[8];
            // Each source is walk on the left, run on the right. No headgear or mirrored pose variants.
            for(int gait=0;gait<2;gait++)for(int phase=0;phase<4;phase++)result.frames[gait*4+phase]=pairs[phase].frames[gait];
            EditorUtility.SetDirty(result);
        }
    }
}
