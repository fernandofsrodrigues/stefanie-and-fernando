using System;
using UnityEngine;

namespace StefanieAndFernando
{
    // Class-course chapter data. Chapter 0 (Curitiba) is a literal copy of the graded scene; its live build still comes from
    // SFGame.BuildLevel/SpawnEnemies, and SFChapterCheck (F1) proves the two agree. Chapters 1+ are built from this data only.
    [Serializable]public struct SFChapterHazard{public float x,height,half;public string kind,live,idle;public SFChapterHazard(float x,float height,string kind,string live,string idle,float half=.75f){this.x=x;this.height=height;this.half=half;this.kind=kind;this.live=live;this.idle=idle;}}
    [Serializable]public struct SFChapterPickup{public float x,height;public string kind;public SFChapterPickup(string kind,float x,float height){this.x=x;this.height=height;this.kind=kind;}}
    [Serializable]public struct SFChapterProp{public string sheet;public int frame;public float x,lane,width;public SFChapterProp(string sheet,int frame,float x,float lane,float width){this.sheet=sheet;this.frame=frame;this.x=x;this.lane=lane;this.width=width;}}
    [Serializable]public struct SFChapterEnemy{public string id;public float x,lane,height,hp;public bool boss,final,hold;public SFChapterEnemy(string id,float x,float lane,float height,float hp=0,bool boss=false,bool final=false,bool hold=false){this.id=id;this.x=x;this.lane=lane;this.height=height;this.hp=hp;this.boss=boss;this.final=final;this.hold=hold;}}
    [Serializable]public struct SFChapterCheckpoint{public float trigger,x,height;public string label;public SFChapterCheckpoint(float trigger,float x,float height,string label){this.trigger=trigger;this.x=x;this.height=height;this.label=label;}}
    [Serializable]public struct SFChapterBeat{public float x;public int actor;public string speaker,line;public SFChapterBeat(float x,int actor,string speaker,string line){this.x=x;this.actor=actor;this.speaker=speaker;this.line=line;}}
    // hazardTint is the strip, wash and glow colour (today's Teal in Curitiba); the diagonal hazard markers stay Gold.
    // hp 0 on a cast entry means "use the identity's own health" (SFActor.Configure, or SFEnemyProfiles for new ids).
    // bossShort names the boss in the extraction hint ("EXTRACT  -  STOP ..."), which must fit one line of the 360-380 px hint box.
    [Serializable]public sealed class SFCourseChapter{public int index,next=-1,musicRoute,platformStyle;public bool driveAfter,verticalCatchUp;public string key,city,title,subtitle,briefing,startLabel,platePrefix,plateFallback,plateName,introSpeaker,intro,bossName,bossShort,bossBlock,bossDownNotice,midBossDownNotice,extractionLabel,goalRow,touchJumpRow,winTitle,winLine,wonNotice,nextLabel,hazardSpan;public float levelEnd=132,cameraLift,startX=3,startHeight,extractionX=130,extractionHeight;public Color hazardTint=Color.white;public SFPlatform[] platforms;public SFChapterHazard[] hazards;public SFChapterPickup[] pickups;public SFChapterProp[] props;public SFChapterEnemy[] cast;public SFChapterCheckpoint[] checkpoints;public SFChapterBeat[] beats;}

    public static class SFCourseChapters
    {
        public static readonly SFCourseChapter[] All={Curitiba(),Rio(),Anbar()};
        public static SFCourseChapter Get(int i)=>All[Mathf.Clamp(i,0,All.Length-1)];

        // Literal copy of SFGame.cs BuildLevel/SpawnEnemies/StartGame/Update/TryExtract and the SFHUD course text (INTEL wording).
        static SFCourseChapter Curitiba()=>new SFCourseChapter{
            index=0,next=1,musicRoute=0,platformStyle=0,driveAfter=false,verticalCatchUp=false,
            key="curitiba",city="CURITIBA",title="AFTER THE RAIN",subtitle="CLASS PLATFORMER / PARTNER ADVENTURE",
            briefing="Recover three pieces of intel, clear the route, and extract together.\nJump over electrical hazards and onto platforms.\nA downed hero needs a partner revive. Both down means defeat.",
            startLabel="START PLATFORMER  >",platePrefix="curitiba_",plateFallback=null,plateName="Curitiba plate ",
            introSpeaker="STEFANIE",intro="Three pieces of intel. One way out. Together.",
            bossName="VESPER",bossShort="VESPER",bossBlock="Vesper is blocking extraction.",bossDownNotice="BOSS DISARMED  /  REACH EXTRACTION",midBossDownNotice=null,
            extractionLabel="EXTRACTION",goalRow="GOAL   3 intel, stop Vesper, extract together",touchJumpRow="JUMP   Clear platforms and live current",
            winTitle="BOTH HOME.",winLine="Three pieces of intel recovered. A promise kept.",wonNotice="Both home. Mission complete.",nextLabel="NEXT CHAPTER  /  RIO  >",
            hazardSpan="Electrical current span",hazardTint=new Color(.37f,.86f,.83f),
            levelEnd=132,cameraLift=0,startX=3,startHeight=0,extractionX=130,extractionHeight=0,
            platforms=new[]{new SFPlatform(14,5,1.1f),new SFPlatform(34,4,1),new SFPlatform(53,4,1.1f),new SFPlatform(57,4,2.4f),new SFPlatform(91,5,1.5f)},
            hazards=new[]{new SFChapterHazard(38,0,"current","LIVE CURRENT / JUMP","FAULT CYCLING"),new SFChapterHazard(75,0,"current","LIVE CURRENT / JUMP","FAULT CYCLING"),new SFChapterHazard(103,0,"current","LIVE CURRENT / JUMP","FAULT CYCLING")},
            pickups=new[]{new SFChapterPickup("data",17,1.85f),new SFChapterPickup("data",59,3.15f),new SFChapterPickup("data",94,2.25f),
                new SFChapterPickup("medical",31,.8f),new SFChapterPickup("ammo",65,.8f),new SFChapterPickup("medical",108,.8f),
                new SFChapterPickup("food",11,.8f),new SFChapterPickup("drink",42,.8f),new SFChapterPickup("food",80,.8f)},
            props=new[]{new SFChapterProp("props_solid",0,8,1.1f,2.1f),new SFChapterProp("props_occluder",0,8,1.8f,1.5f),
                new SFChapterProp("props_solid",5,22,.85f,2.3f),new SFChapterProp("props_occluder",1,32,1.1f,1.9f),
                new SFChapterProp("props_solid",3,50,1.1f,2.2f),new SFChapterProp("props_solid",4,72,1.1f,1.8f),
                new SFChapterProp("props_solid",2,100,1.1f,2.3f)},
            cast=new[]{new SFChapterEnemy("latch",24,.1f,0),new SFChapterEnemy("latch",28,-.25f,0),new SFChapterEnemy("keel",45,.2f,0),new SFChapterEnemy("latch",48,-.1f,0),
                new SFChapterEnemy("latch",69,-.2f,0),new SFChapterEnemy("keel",85,.3f,0),new SFChapterEnemy("latch",88,-.25f,0),new SFChapterEnemy("vesper",119,0,0,0,true,true)},
            checkpoints=new[]{new SFChapterCheckpoint(61,62,0,"CHECKPOINT  /  CONSERVATORY APPROACH")},
            beats=new[]{new SFChapterBeat(109,7,"VESPER","This road ends here.")}};

        // CLIMB TO THE REDEEMER: Rocinha rooftops at tiers 1.5 / 3.0 / 4.5 up to the Redeemer terrace.
        // driveAfter: the Won card's DESERT DRIVE opens the JLTV interlude (an in-country convoy after a travel cut to Iraq), then Al Anbar.
        // If the road cannot open, AdvanceCourse falls back to the direct cut.
        static SFCourseChapter Rio()=>new SFCourseChapter{
            index=1,next=2,musicRoute=2,platformStyle=1,driveAfter=true,verticalCatchUp=true,
            key="rio",city="RIO",title="CLIMB TO THE REDEEMER",subtitle="CHAPTER 2  /  RIO DE JANEIRO",
            briefing="Recover three pieces of intel on the climb to the Redeemer terrace.\nJump the live wiring; roofs stack three high, and a fall only costs a climb.\nA downed hero needs a partner revive. Both down means defeat.",
            startLabel="START CHAPTER 2  >",platePrefix="rio_course_",plateFallback="plate_2_",plateName="Rio plate ",
            introSpeaker="FERNANDO",intro="Three pieces of intel between here and the Redeemer. Up we go.",
            bossName="THE BROKER",bossShort="BROKER",bossBlock="The Broker is blocking extraction.",bossDownNotice="BROKER DOWN  /  REACH THE REDEEMER TERRACE",midBossDownNotice=null,
            extractionLabel="REDEEMER TERRACE",goalRow="GOAL   3 intel, stop the Broker, reach the terrace",touchJumpRow="JUMP   Climb rooftops, clear live wiring",
            winTitle="THE REDEEMER.",winLine="Three pieces of intel recovered. The trail leads to Al Anbar.",wonNotice="Chapter clear. Next: Al Anbar.",nextLabel="DESERT DRIVE  /  AL ANBAR  >",
            hazardSpan="Live wire span",hazardTint=new Color(.37f,.86f,.83f),
            levelEnd=132,cameraLift=.25f,startX=3,startHeight=0,extractionX=130,extractionHeight=4.5f,
            // R0..R14. Tiers 0 / 1.5 / 3.0 / 4.5 only (R0 at 1.0 is a first step); every deck above 1.75 has a nested support 1.5 below.
            // R6 (64-85) runs under all of R7, and R9 (93-107) under R11's right end, so a partner or enemy climbing from the street always
            // has a 1.5 deck beneath its step or follow spot (no hop loop). R9 stops 1.0 short of R12 (108): wider than the .52 actor capsule,
            // so a walker drops through as at R1/R2, and the deck lookups (+-.3) never overlap there.
            platforms=new[]{new SFPlatform(10,4,1),new SFPlatform(15,7,1.5f),new SFPlatform(23,12,1.5f),new SFPlatform(29,6,3),new SFPlatform(43,15,1.5f),new SFPlatform(46,12,3),new SFPlatform(64,21,1.5f),new SFPlatform(68,17,3),new SFPlatform(72.5f,6,4.5f),new SFPlatform(93,14,1.5f),new SFPlatform(96,11,3),new SFPlatform(101,6,4.5f),new SFPlatform(108,24,1.5f),new SFPlatform(108.5f,23.5f,3),new SFPlatform(109,23,4.5f)},
            hazards=new[]{new SFChapterHazard(32,1.5f,"wire","LIVE WIRE  /  JUMP","WIRE CYCLING"),new SFChapterHazard(40,0,"cable","SPLICED CABLE  /  JUMP","CABLE CYCLING"),
                new SFChapterHazard(52,3,"wire","LIVE WIRE  /  JUMP","WIRE CYCLING"),new SFChapterHazard(89.4f,0,"transformer","TRANSFORMER ARC  /  JUMP","ARC CYCLING")},
            pickups=new[]{new SFChapterPickup("data",32,3.75f),new SFChapterPickup("data",77,5.25f),new SFChapterPickup("data",104,5.25f),
                new SFChapterPickup("food",8,.8f),new SFChapterPickup("drink",36,.8f),new SFChapterPickup("ammo",62,.8f),new SFChapterPickup("drink",86,.8f),
                new SFChapterPickup("medical",57,3.8f),new SFChapterPickup("food",84,3.8f),new SFChapterPickup("medical",113,5.3f)},
            props=new[]{new SFChapterProp("props_urban",0,6,1.1f,2.1f),new SFChapterProp("props_coastal",1,20,.85f,2.2f),new SFChapterProp("props_urban",3,37,1.1f,1.9f),
                new SFChapterProp("props_urban",5,60,1.1f,2.2f),new SFChapterProp("props_coastal",2,87,1.1f,2),new SFChapterProp("props_urban",1,100,.85f,2.1f)},
            cast=new[]{new SFChapterEnemy("smuggler",18,.15f,0),new SFChapterEnemy("silk",30,0,1.5f),new SFChapterEnemy("smuggler_shotgun",38,-.2f,0),
                new SFChapterEnemy("smuggler",48,0,3),new SFChapterEnemy("silk",56,0,3),new SFChapterEnemy("smuggler",70,0,3),new SFChapterEnemy("silk",82,0,3),
                new SFChapterEnemy("smuggler_shotgun",98,0,3),new SFChapterEnemy("merc_boss",124,0,4.5f,0,true,true,true)},
            checkpoints=new[]{new SFChapterCheckpoint(61,62,0,"CHECKPOINT  /  HALFWAY UP THE HILLSIDE"),new SFChapterCheckpoint(105,111,1.5f,"CHECKPOINT  /  REDEEMER STAIRS")},
            beats=new[]{new SFChapterBeat(110,8,"THE BROKER","Nice view up here. Last one you get.")}};

        // THE UNFINISHED TOWER: a construction site held by a fictional mercenary network (no real group, flag or symbol).
        static SFCourseChapter Anbar()=>new SFCourseChapter{
            index=2,next=-1,musicRoute=5,platformStyle=2,driveAfter=false,verticalCatchUp=true,
            key="anbar",city="AL ANBAR",title="THE UNFINISHED TOWER",subtitle="CHAPTER 3  /  AL ANBAR PROVINCE",
            briefing="Climb to the Iron Meridian's roof HQ and recover three pieces of intel.\nCross the girders; jump welding arcs, steam and live cable.\nA downed hero needs a partner revive. Both down means defeat.",
            startLabel="START CHAPTER 3  >",platePrefix="anbar_course_",plateFallback="plate_5_",plateName="Anbar plate ",
            introSpeaker="STEFANIE",intro="Three pieces of intel inside that tower. The roof is our way out.",
            bossName="THE ARCHITECT",bossShort="ARCHITECT",bossBlock="The Architect is blocking extraction.",bossDownNotice="ARCHITECT DOWN  /  REACH ROOF EXTRACTION",midBossDownNotice="CANTILEVER DOWN  /  KEEP CLIMBING",
            extractionLabel="ROOF EXTRACTION",goalRow="GOAL   3 intel, stop the Architect, extract from the roof",touchJumpRow="JUMP   Cross girders, clear arcs and steam",
            winTitle="BOTH HOME.",winLine="The tower is dark. Three chapters, one promise kept.",wonNotice="Both home. Mission complete.",nextLabel="PLAY AGAIN FROM CURITIBA  >",
            hazardSpan="Welding spark span",hazardTint=new Color(1,.62f,.25f),
            levelEnd=132,cameraLift=.25f,startX=3,startHeight=0,extractionX=130,extractionHeight=4.5f,
            // A0..A16. The crane girders A10-A12 and the A13 step-down have no support below by design (partner uses the vertical catch-up).
            // A4 reaches 34.5, so the partner's spot cleared of the A2 welding strip (32.25+1.8) still lies under it.
            platforms=new[]{new SFPlatform(9,8.5f,1.5f),new SFPlatform(12.5f,5,3),new SFPlatform(25,21,1.5f),new SFPlatform(26,3,3),new SFPlatform(30.5f,4,3),new SFPlatform(35.5f,4,3),new SFPlatform(47,25.5f,1.5f),new SFPlatform(50,12,3),new SFPlatform(66,6.5f,3),new SFPlatform(69.5f,3,4.5f),new SFPlatform(74,3,4.5f),new SFPlatform(78.5f,3,4.5f),new SFPlatform(83,4,4.5f),new SFPlatform(87.5f,4,3),new SFPlatform(95,37,1.5f),new SFPlatform(99,33,3),new SFPlatform(103,29,4.5f)},
            hazards=new[]{new SFChapterHazard(21.6f,0,"cable","LIVE CABLE  /  JUMP","CABLE CYCLING"),new SFChapterHazard(32.25f,1.5f,"welding","WELDING ARC  /  JUMP","WELDER COOLING"),
                new SFChapterHazard(55,1.5f,"steam","STEAM VENT  /  JUMP","PRESSURE BUILDING"),new SFChapterHazard(112,4.5f,"generator","GENERATOR ARC  /  JUMP","GENERATOR CYCLING")},
            pickups=new[]{new SFChapterPickup("data",16,3.75f),new SFChapterPickup("data",60.5f,3.75f),new SFChapterPickup("data",85,5.25f),
                new SFChapterPickup("food",6,.8f),new SFChapterPickup("drink",27,2.3f),new SFChapterPickup("medical",43,2.3f),new SFChapterPickup("ammo",48.5f,2.3f),
                new SFChapterPickup("food",68,3.8f),new SFChapterPickup("drink",89.5f,3.8f),new SFChapterPickup("medical",106,5.3f)},
            props=new[]{new SFChapterProp("props_desert",0,7,1.1f,2.2f),new SFChapterProp("props_sideon",1,18.5f,.85f,2),new SFChapterProp("props_desert",2,45,1.1f,2.1f),
                new SFChapterProp("props_sideon",3,58,.85f,2.2f),new SFChapterProp("props_desert",4,92,1.1f,2),new SFChapterProp("props_sideon",0,100,.85f,2.1f)},
            cast=new[]{new SFChapterEnemy("raider",27,.1f,0),new SFChapterEnemy("ratchet",27.5f,0,3,0,false,false,true),new SFChapterEnemy("foreman",37.5f,0,1.5f,90),
                new SFChapterEnemy("raider",49,0,1.5f),new SFChapterEnemy("cantilever",57,0,3,180,true,false,true),new SFChapterEnemy("ratchet",65,0,1.5f),
                new SFChapterEnemy("raider",86.2f,0,4.5f,0,false,false,true),new SFChapterEnemy("foreman",93,.1f,0,90),new SFChapterEnemy("armored_boss",124,0,4.5f,0,true,true,true)},
            checkpoints=new[]{new SFChapterCheckpoint(43,44.5f,1.5f,"CHECKPOINT  /  FLOOR ONE"),new SFChapterCheckpoint(94,97.5f,1.5f,"CHECKPOINT  /  HQ STAIRWELL")},
            beats=new[]{new SFChapterBeat(49,4,"CANTILEVER","Mind the gap. It's a long way down."),new SFChapterBeat(106,8,"THE ARCHITECT","Every floor I built leads here. None lead out.")}};
    }
}
