using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    // Course-chapter enemy identities. Each new id borrows an existing layout's behaviour (and its art when the id's own sheet is missing).
    // Get() returns null for the seven existing ids (latch, keel, vesper, silk, ratchet, cantilever, foreman), so their code paths stay byte-identical.
    // tint and scale dress only a fallback (an id drawn with its layout's art, SFActor.Configure/Render); an id's own sheets draw untinted at scale 1.
    public sealed class SFEnemyProfile{public string layout,title;public float hp,speed,scale=1;public Color tint=Color.white;}
    public static class SFEnemyProfiles
    {
        static readonly Dictionary<string,SFEnemyProfile> map=new Dictionary<string,SFEnemyProfile>{
            {"smuggler",new SFEnemyProfile{layout="latch",title="SMUGGLER",hp=60,speed=3.4f,tint=new Color(1,.86f,.72f)}},
            {"smuggler_shotgun",new SFEnemyProfile{layout="keel",title="SMUGGLER",hp=52,speed=3f,tint=new Color(.82f,.95f,.78f)}},
            {"raider",new SFEnemyProfile{layout="silk",title="RAIDER",hp=58,speed=3.3f,tint=new Color(1,.88f,.66f)}},
            {"merc_boss",new SFEnemyProfile{layout="cantilever",title="THE BROKER",hp=260,speed=2.7f,scale=1.06f,tint=new Color(.8f,.88f,1)}},
            {"armored_boss",new SFEnemyProfile{layout="foreman",title="THE ARCHITECT",hp=320,speed=2.2f,scale=1.1f,tint=new Color(.74f,.78f,.84f)}}};
        public static SFEnemyProfile Get(string id)=>id!=null&&map.TryGetValue(id,out var p)?p:null;
        public static string Title(string id)=>Get(id)?.title??(id=="foreman"?"THE FOREMAN":id.ToUpperInvariant());
        // The person an identity depicts, for enemy variety (SFFeel.cs): Codex's smuggler_shotgun sheets draw the smuggler himself with a shotgun.
        // Every other id is its own person, so chapter 0's latch/keel/vesper count exactly as before.
        public static string Person(string id)=>id=="smuggler_shotgun"?"smuggler":id;
        // A sheet is usable only with at least min frames and no missing frame; otherwise the actor falls back to its layout's art as one set.
        public static bool Usable(SFArt a,int min)=>a!=null&&a.frames!=null&&a.frames.Length>=min&&System.Array.TrueForAll(a.frames,f=>f!=null);
    }
}
