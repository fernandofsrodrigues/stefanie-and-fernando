using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // Chapter QA, data mode (-sfCourse -sfChapterCheck on chapter 0): F1 proves SFCourseChapters.All[0] equals the live Curitiba
    // scene, F3 validates the Rio and Al Anbar layout data without building them, and F5 sweeps the enemy identity layer
    // (existing ids unchanged; new ids spawn on a deck and draw every state from one art source), plus F8's chapter 0 rule (Curitiba never
    // runs the vertical catch-up). Ends with 'CHAPTER DATA QA COMPLETE: PASS|FAIL'.
    // Chapter mode (-sfCourse -sfChapterCheck -sfChapter N): F3 again, then the built chapter. F4 plates (Codex art or fallback), the level
    // build, F5 spawns and identities, checkpoints/beats/retry, F9 elevated hazards and partner waiting, F6 climbs, F7 no second takeoff,
    // F8 partner climbs and vertical catch-up, F11 deck guards and no hop loop, F10 extraction and bosses, and the camera lift.
    // Ends with 'CHAPTER CHECK n COMPLETE: PASS|FAIL' and QA/chapter-check-n.txt, plus chapter-n-*-world.png captures.
    public sealed partial class SFChapterCheck:MonoBehaviour
    {
        SFGame g;readonly List<string> log=new List<string>();
        void Check(bool ok,string name){string line=(ok?"PASS: ":"FAIL: ")+name;log.Add(line);Debug.Log(line);}
        void Info(string text){string line="INFO: "+text;log.Add(line);Debug.Log(line);}
        static bool Near(float a,float b,float tolerance=.01f)=>Mathf.Abs(a-b)<=tolerance;
        static readonly string[] OldIds={"latch","keel","vesper","silk","ratchet","cantilever","foreman"};

        IEnumerator Start()
        {
            g=GetComponent<SFGame>();
            yield return new WaitForSeconds(.2f);
            Debug.Log("Chapter QA: chapter "+g.CourseChapter+", chapters enabled "+SFGame.ChaptersEnabled);
            if(System.Environment.GetCommandLineArgs().Contains("-sfReviewR2"))
            {
                if(g.InChapter)yield return ReviewR2();else Check(false,"R2 requires chapter 1 or 2");
                Finish();yield break;
            }
            Tables();
            foreach(string line in SFChapterLayoutRules.ValidateAll()){log.Add(line);Debug.Log(line);}
            if(g.InChapter)yield return ChapterFixtures();
            else{ExistingIdentities();yield return NoDrift();yield return IdentitySweep();yield return CuritibaNoCatchUp();}
            Finish();
        }

        // Places a hero or enemy with no residual input. The game loop is disabled around these fixtures, so only physics moves bodies.
        static void Ready(SFActor a,float x,float y)
        {
            // SetCommand(0,false) never clears a held or buffered jump (F7 ends airborne with a .15 buffer), and a body teleported while it
            // overlaps its old one-way deck keeps that contact. Freeze(true) clears both; restoring the prior simulated state keeps frozen
            // foes frozen for Stage/Home (port of Codex 03b7604 and 142aced).
            a.ReleaseGrip();a.Revive();bool simulated=a.motor.Body.simulated;a.motor.Freeze(true);
            a.health=a.maxHealth;a.invulnerable=a.cooldown=0;a.lane=0;
            a.Teleport(new Vector2(x,y));a.action=SFAction.Idle;a.motor.SetCommand(0,false);a.motor.Freeze(!simulated);
        }
        SFActor[] Foes=>g.Actors.Where(a=>!a.Friendly).ToArray();
        void FreezeFoes(){foreach(var a in Foes)a.motor.Freeze(true);}
        IEnumerator Shot(string name,float x,float y)
        {
            if(Application.platform==RuntimePlatform.WebGLPlayer)yield break;
            foreach(var a in g.Actors)a.Render(0);
            g.Camera.transform.position=new Vector3(x,y,-10);
            yield return new WaitForEndOfFrame();
            g.Capture("chapter-"+g.CourseChapter+"-"+name);
            yield return null;
        }

        IEnumerator ChapterFixtures()
        {
            var ch=g.Chapter;
            Plates(ch);Build(ch);Cast(ch);
            bool shake=g.ShakeEnabled;g.ShakeEnabled=false;
            g.StartGame();
            Check(g.State==SFState.Playing&&g.Speaker==ch.introSpeaker&&g.Speech==ch.intro,"F5 "+ch.key+" intro line is the chapter's ("+ch.introSpeaker+")");
            Check(Near(g.Player.X,ch.startX,.05f)&&Near(g.Partner.X,ch.startX-1.5f,.05f),"F5 heroes start at x"+ch.startX+" and x"+(ch.startX-1.5f));
            yield return new WaitForSeconds(1);
            var foes=Foes;var off=new List<string>();
            for(int i=0;i<foes.Length&&i<ch.cast.Length;i++)if(!foes[i].motor.IsGrounded||Mathf.Abs(foes[i].Height-ch.cast[i].height)>.15f)off.Add(foes[i].identity+"@"+foes[i].X.ToString("0.0")+" h"+foes[i].Height.ToString("0.00")+" want "+ch.cast[i].height);
            Check(foes.Length==ch.cast.Length&&off.Count==0,"F5 after 1 s of play every enemy stands grounded within .15 of its declared deck"+(off.Count>0?": "+string.Join("; ",off):""));
            Check(g.Player.motor.IsGrounded&&g.Player.Height<.2f&&Near(g.Camera.transform.position.y,1,.001f)&&g.CameraLift<.001f,"camera y stays 1 (no lift) while the lead is on the street");
            yield return Shot("start",g.Camera.transform.position.x,g.Camera.transform.position.y);
            g.enabled=false;FreezeFoes();
            yield return Triggers(ch);
            yield return Hazards(ch);
            yield return Movement(ch);
            yield return Extraction(ch);
            g.ShakeEnabled=shake;
        }

        // F4: six single-frame plates on the Curitiba grid, logged per slot as Codex art or the fallback. A missing prefix never throws.
        void Plates(SFCourseChapter ch)
        {
            var renderers=g.World.GetComponentsInChildren<SpriteRenderer>(true);int resolved=0;
            for(int i=0;i<6;i++)
            {
                var art=SFGame.LoadPlate(ch,i,out string id);if(art!=null)resolved++;
                var r=renderers.Where(s=>s.name==ch.plateName+i).ToArray();
                Info("F4 "+ch.key+" plate slot "+i+": "+(id==null?"missing":id==ch.plateFallback+i?"fallback "+id:"Codex art "+id));
                Check(art==null?r.Length==0:r.Length==1&&r[0].sprite==art.frames[0]&&Near(r[0].transform.position.x,i*23+11.5f)&&Near(r[0].transform.position.y,1)&&Near(r[0].bounds.size.x,24,.05f),"F4 plate slot "+i+" draws "+(id??"nothing")+" as one 24-wide frame centred at x"+(i*23+11.5f));
            }
            Check(resolved==6,"F4 all six plate slots resolve to Codex art or the "+ch.plateFallback+"* fallback");
            Check(renderers.All(s=>!s.name.StartsWith("Curitiba plate")),"F4 no Curitiba plate is built in "+ch.key);
            var probe=new SFCourseChapter{platePrefix="sf_missing_course_",plateFallback=ch.plateFallback};bool threw=false;SFArt found=null;string foundId=null;
            try{found=SFGame.LoadPlate(probe,0,out foundId);}catch(System.Exception e){threw=true;Debug.LogException(e);}
            Check(!threw&&found!=null&&foundId==ch.plateFallback+"0","F4 a missing plate prefix falls back to "+ch.plateFallback+"0 without throwing");
            probe.plateFallback="sf_missing_fallback_";threw=false;
            try{found=SFGame.LoadPlate(probe,0,out foundId);}catch(System.Exception e){threw=true;Debug.LogException(e);}
            Check(!threw&&found==null&&foundId==null,"F4 a slot with neither art nor fallback returns nothing without throwing");
        }

        // The chapter build: decks, deck art style, hazards (street strips below the contact shadows, deck strips above the deck lip), pickups,
        // props, the extraction beacon, the start checkpoint and the chapter's music route and HUD city.
        void Build(SFCourseChapter ch)
        {
            Check(g.Platforms.Length==ch.platforms.Length&&g.Platforms.Select((p,i)=>Near(p.x,ch.platforms[i].x)&&Near(p.width,ch.platforms[i].width)&&Near(p.height,ch.platforms[i].height)).All(ok=>ok),"build loads the "+ch.platforms.Length+" "+ch.key+" decks in order");
            Check(g.World.GetComponentsInChildren<PlatformEffector2D>(true).Length==ch.platforms.Length,"every deck has one one-way collider");
            var renderers=g.World.GetComponentsInChildren<SpriteRenderer>(true);
            string lip=ch.platformStyle==1?"Laje lip":ch.platformStyle==2?"Girder lip":"Platform lip";
            Check(renderers.Count(r=>r.name==lip)==ch.platforms.Length,"every deck draws the style "+ch.platformStyle+" '"+lip+"'");
            Check(g.Hazards.Length==ch.hazards.Length&&g.HazardHeights.Length==ch.hazards.Length&&g.HazardHalf.Length==ch.hazards.Length&&ch.hazards.Select((z,i)=>Near(g.Hazards[i],z.x)&&Near(g.HzH(i),z.height)&&Near(g.HzW(i),z.half>0?z.half:.75f)).All(ok=>ok),"hazard x, height and half-width load from the chapter");
            var spans=renderers.Where(r=>r.name==ch.hazardSpan).ToArray();
            Check(spans.Length==ch.hazards.Length,"exactly "+ch.hazards.Length+" '"+ch.hazardSpan+"' objects");
            Check(renderers.Count(r=>r.name=="Electrical current span")==(ch.hazardSpan=="Electrical current span"?ch.hazards.Length:0),"no Curitiba current span is built in "+ch.key);
            bool placed=spans.Length==ch.hazards.Length&&ch.hazards.All(z=>spans.Any(r=>Near(r.transform.position.x,z.x)&&(z.height<=0?Near(r.transform.position.y,-3.02f)&&r.sortingOrder<50:Near(r.transform.position.y,-3+z.height+.05f)&&r.sortingOrder>61&&r.sortingOrder<100)));
            Check(placed,"street strips sit at y -3.02 below the contact shadows; deck strips sit on their deck above the lip");
            var triggers=g.World.GetComponentsInChildren<SFInteraction>(true).Where(t=>t.hazard).ToArray();
            Check(triggers.Length==ch.hazards.Length&&ch.hazards.All(z=>triggers.Any(t=>Near(t.transform.position.x,z.x)&&Near(t.transform.position.y,z.height+.3f))),"hazard markers sit at (x, height+.3)");
            Check(g.Pickups.Count==ch.pickups.Length&&g.Pickups.Select((p,i)=>p.kind==ch.pickups[i].kind&&Near(p.x,ch.pickups[i].x)&&Near(p.height,ch.pickups[i].height)).All(ok=>ok),"pickups match the chapter data in order");
            Check(renderers.Count(r=>r.name=="Street prop")==ch.props.Count(p=>Resources.Load<SFArt>("SF/"+p.sheet)!=null),"props from the chapter data (missing sheets skipped)");
            var beacon=renderers.FirstOrDefault(r=>r.name=="Extraction beacon");
            Check(beacon!=null&&Near(beacon.transform.position.x,ch.extractionX)&&Near(beacon.transform.position.y,-2.7f+ch.extractionHeight),"extraction beacon at ("+ch.extractionX+", "+(-2.7f+ch.extractionHeight)+")");
            Check(Near(g.Checkpoint,ch.startX)&&Near(g.CheckpointHeight,ch.startHeight),"checkpoint starts at the chapter start");
            Check(g.RouteIndex==ch.musicRoute&&g.RouteName==ch.city,"music route "+ch.musicRoute+" and HUD city "+ch.city);
        }

        // F5 in the menu, before anything moves: ids, x, lanes, flags and health in data order, plus the final boss and its boss-bar title.
        void Cast(SFCourseChapter ch)
        {
            var foes=Foes;
            Check(foes.Length==ch.cast.Length&&foes.Select((a,i)=>a.identity==ch.cast[i].id&&Near(a.X,ch.cast[i].x,.05f)&&Near(a.lane,ch.cast[i].lane)&&a.boss==ch.cast[i].boss&&a.HoldTier==ch.cast[i].hold).All(ok=>ok),"F5 "+ch.key+" cast ids, x, lanes, boss and hold flags match the chapter data");
            Check(g.CourseCast.Count==ch.cast.Length&&g.CourseCast.Select((a,i)=>i<foes.Length&&a==foes[i]).All(ok=>ok),"F5 the chapter cast list follows data order (beat actor indices)");
            int final=System.Array.FindIndex(ch.cast,e=>e.final);
            Check(final>=0&&final<foes.Length&&g.FinalBoss==foes[final]&&g.FinalBoss.boss&&SFEnemyProfiles.Title(g.FinalBoss.identity)==ch.bossName,"F5 the final boss is "+(final>=0?ch.cast[final].id:"missing")+" with boss-bar title "+ch.bossName);
            var bad=new List<string>();
            for(int i=0;i<foes.Length&&i<ch.cast.Length;i++)
            {
                var e=ch.cast[i];var p=SFEnemyProfiles.Get(e.id);var a=foes[i];
                float want=e.hp>0?e.hp:p!=null?p.hp:e.boss?240:(a.LayoutId=="keel"||a.LayoutId=="ratchet")?52:64;
                if(!Near(a.maxHealth,want)||a.health!=a.maxHealth)bad.Add(e.id+" "+a.maxHealth+" want "+want);
                if(e.boss)Info("F5 "+ch.key+" boss "+e.id+(e.final?" (final)":" (mid)")+" HP "+a.maxHealth+", bar title "+SFEnemyProfiles.Title(e.id));
            }
            Check(bad.Count==0,"F5 health follows the data override, then the profile, then the identity default"+(bad.Count>0?": "+string.Join("; ",bad):""));
            foreach(var id in ch.cast.Select(e=>e.id).Distinct())
            {
                var a=foes.FirstOrDefault(f=>f.identity==id);if(a==null)continue;var p=SFEnemyProfiles.Get(id);
                Info("F5 "+id+" LayoutId "+a.LayoutId+", ArtId "+a.ArtId+(p==null?" (existing id)":a.ArtId==id?" (its own art)":" (layout fallback art)"));
                Check(p==null?a.LayoutId==id&&a.ArtId==id:a.LayoutId==p.layout&&(a.ArtId==id||a.ArtId==p.layout),"F5 "+id+" resolves its layout and one art source");
                // Own sheets are authored at their intended size; only a fallback to the layout's art takes the profile scale.
                float size=p!=null&&a.ArtId!=id?p.scale:1;
                Check(foes.Where(f=>f.identity==id).All(f=>f.visual.transform.localScale==Vector3.one*size),"F5 "+id+" draws at scale "+size+(p!=null&&a.ArtId!=id?" (profile scale on fallback art)":" (its own art is not rescaled)"));
            }
        }

        // Checkpoints advance by index (any height) and retry lands both heroes on the checkpoint deck. Beats fire once with their line.
        // Probes run in ascending x: the beats left of each checkpoint trigger are checked before its probes, so no checkpoint probe spends
        // a beat that has not been checked yet (Al Anbar's x49 beat sits between its x43 and x94 triggers).
        // Chapter 0's literal x61 checkpoint and x109 Vesper line never fire inside a chapter, even with the real Update loop running.
        IEnumerator Triggers(SFCourseChapter ch)
        {
            var lead=g.Player;var partner=g.Partner;
            var order=Enumerable.Range(0,ch.beats.Length).OrderBy(i=>ch.beats[i].x).ToArray();int nb=0;
            var xs=ch.checkpoints.Select(c=>c.trigger).Concat(ch.beats.Select(b=>b.x)).OrderBy(x=>x).ToArray();
            Check(ch.checkpoints.Select((c,i)=>i==0||c.trigger>ch.checkpoints[i-1].trigger).All(ok=>ok)&&xs.Zip(xs.Skip(1),(a,b)=>b-a>=.6f).All(ok=>ok),"checkpoint triggers ascend and no two trigger or beat x lie within .6, so the +-.3 probes stay in order");
            void Beat()
            {
                int k=order[nb++];var b=ch.beats[k];g.Say("","");
                Ready(lead,b.x+.3f,.05f);g.TickChapterTriggers();
                Check(g.Speaker==b.speaker&&g.Speech==b.line,"beat "+k+" fires past x"+b.x+": "+b.speaker);
                g.Say("","");g.TickChapterTriggers();
                Check(g.Speaker=="","beat "+k+" fires only once");
            }
            for(int k=0;k<ch.checkpoints.Length;k++)
            {
                var c=ch.checkpoints[k];
                while(nb<order.Length&&ch.beats[order[nb]].x<c.trigger)Beat();
                float before=g.Checkpoint;
                Ready(lead,c.trigger-.3f,.05f);g.TickChapterTriggers();
                Check(Near(g.Checkpoint,before),"checkpoint "+k+" holds before x"+c.trigger);
                Ready(lead,c.trigger+.3f,.05f);g.TickChapterTriggers();
                Check(Near(g.Checkpoint,c.x)&&Near(g.CheckpointHeight,c.height)&&g.Notice==c.label,"checkpoint "+k+" moves to ("+c.x+", "+c.height+") with '"+c.label+"' past x"+c.trigger);
                g.RetryCheckpoint();FreezeFoes();
                yield return new WaitForSeconds(.45f);
                var f=g.Fernando;var s=g.Stefanie;
                Check(f.motor.IsGrounded&&s.motor.IsGrounded&&Near(f.X,c.x,.05f)&&Near(s.X,c.x-1.5f,.05f)&&Near(f.Height,c.height,.12f)&&Near(s.Height,c.height,.12f),"retry from checkpoint "+k+" lands both heroes on its deck: F "+f.X.ToString("0.00")+"/"+f.Height.ToString("0.00")+", S "+s.X.ToString("0.00")+"/"+s.Height.ToString("0.00"));
                if(k==ch.checkpoints.Length-1)yield return Shot("checkpoint",c.x,1+g.CameraLift);
            }
            while(nb<order.Length)Beat();
            float last=ch.checkpoints.Length>0?ch.checkpoints[ch.checkpoints.Length-1].x:ch.startX;
            g.Say("","");g.Notice="";g.NoticeTime=0;
            foreach(float x in new[]{61.5f,109.5f})
            {
                Ready(lead,x,.05f);Ready(partner,x-1.5f,.05f);g.enabled=true;
                for(int n=0;n<3;n++)yield return null;
                g.enabled=false;FreezeFoes();
            }
            Check(g.Notice!="CHECKPOINT  /  CONSERVATORY APPROACH"&&g.Speaker!="VESPER"&&Near(g.Checkpoint,last),"chapter 0's x61 checkpoint and x109 Vesper line do not fire in "+ch.key+" under the live Update loop");
        }

        // F9: the first deck hazard damages only on its deck, only when live and only inside its half-width; the street below is safe and its
        // sparks rise to its deck. The partner waits at a live strip only on that strip's level. Street strips keep chapter 0's rule.
        IEnumerator Hazards(SFCourseChapter ch)
        {
            var lead=g.Player;var partner=g.Partner;
            int e=System.Array.FindIndex(ch.hazards,z=>z.height>0),s=System.Array.FindIndex(ch.hazards,z=>z.height<=0);
            Check(e>=0&&s>=0,"F9 "+ch.key+" has both a deck hazard and a street hazard");
            if(e>=0)
            {
                var z=ch.hazards[e];float half=g.HzW(e);
                Ready(partner,ch.startX-1.5f,.05f);Ready(lead,z.x,z.height+.05f);yield return new WaitForSeconds(.25f);
                Check(lead.motor.IsGrounded&&Near(lead.Height,z.height,.12f),"F9 the lead stands on the "+z.kind+" deck at x"+z.x);
                g.Elapsed=1;g.TickHazards();Check(lead.health==lead.maxHealth,"F9 the idle "+z.kind+" does no damage");
                g.Elapsed=2;g.TickHazards();Check(lead.health==lead.maxHealth-13,"F9 the live "+z.kind+" deals 13 on its deck");
                yield return Shot("deck-hazard",z.x,1);
                Ready(lead,z.x+half+.05f,z.height+.05f);yield return new WaitForSeconds(.2f);
                g.TickHazards();Check(lead.health==lead.maxHealth,"F9 x"+(z.x+half+.05f)+" just outside the half-width is safe");
                Ready(lead,z.x,.05f);yield return new WaitForSeconds(.25f);
                g.TickHazards();Check(lead.Height<.2f&&lead.health==lead.maxHealth,"F9 the street under the live "+z.kind+" is safe");
                Check(!g.HzLevel(0,e)&&g.HzLevel(z.height,e)&&g.HzDeck(z.height,e,.3f)&&!g.HzDeck(0,e,.3f),"F9 level helpers place the "+z.kind+" on its deck only");
                Ready(lead,ch.startX,.05f);
                for(int n=0;n<60;n++)g.TickHazards();
                var sparks=FindObjectsByType<SpriteRenderer>().Where(r=>r.name=="Impact"&&Mathf.Abs(r.transform.position.x-z.x)<=1.01f&&Near(r.transform.position.y,-2.9f+z.height,.02f)).Count();
                Check(sparks>0,"F9 "+z.kind+" sparks appear at visual y "+(-2.9f+z.height));
                var foes=Foes;var health=foes.Select(a=>a.health).ToArray();foreach(var a in foes)a.health=0;
                g.Elapsed=2;
                Ready(lead,z.x+5,.05f);Ready(partner,z.x-1.6f,.05f);yield return new WaitForSeconds(.25f);
                var c=g.CompanionCommand();
                Check(partner.Height<.2f&&c.move.x>0,"F9 a street partner walks under the live "+z.kind+" (move "+c.move.x+")");
                Ready(lead,z.x+2.5f,z.height+.05f);Ready(partner,z.x-1.6f,z.height+.05f);yield return new WaitForSeconds(.25f);
                c=g.CompanionCommand();
                Check(Near(partner.Height,z.height,.12f)&&c.move.x==0,"F9 a partner on the "+z.kind+" deck waits at its edge while it is live (move "+c.move.x+")");
                for(int i=0;i<foes.Length;i++)foes[i].health=health[i];
            }
            if(s>=0)
            {
                var z=ch.hazards[s];
                Ready(partner,ch.startX-1.5f,.05f);Ready(lead,z.x,.05f);yield return new WaitForSeconds(.25f);
                g.Elapsed=1;g.TickHazards();Check(lead.health==lead.maxHealth,"F9 street "+z.kind+" idle does no damage");
                g.Elapsed=2;g.TickHazards();Check(lead.health==lead.maxHealth-13,"F9 street "+z.kind+" live deals 13, like chapter 0's strips");
                Ready(lead,z.x+.8f,.05f);yield return new WaitForSeconds(.2f);g.TickHazards();Check(lead.health==lead.maxHealth,"F9 street "+z.kind+" is safe .8 from its centre");
                Check(g.HzLevel(0,s)&&!g.HzLevel(1.5f,s),"F9 a street strip counts only on the street in chapters");
            }
            g.Elapsed=1;g.TickHazards();
        }

        // Movement fixtures on the built chapter, game loop off: real bodies with normal commands (F6, F7), the real CompanionCommand (F8) and
        // the real EnemyCommand (F11). Foes stay frozen at health 0 through F6-F8 so nothing clinches the lead or distracts the partner.
        string deckTag="R";
        string D(int i)=>deckTag+i;
        static bool Settled(SFActor a,float h,float x0,float x1)=>a.motor.IsGrounded&&Mathf.Abs(a.motor.Body.linearVelocity.y)<.1f&&Near(a.Height,h,.12f)&&a.X>=x0-.05f&&a.X<=x1+.05f;
        // On the street or on a deck top (from .05 below to .25 above it). A fresh takeoff from anywhere else is a mid-air boost.
        bool Standing(SFActor a)=>Mathf.Abs(a.Height)<.25f||g.Platforms.Any(p=>a.X>p.x-.3f&&a.X<p.x+p.width+.3f&&a.Height>p.height-.05f&&a.Height<p.height+.25f);
        int DeckAt(float x,float h)=>System.Array.FindIndex(g.Platforms,p=>Mathf.Abs(p.height-h)<.01f&&x>=p.x-.01f&&x<=p.x+p.width+.01f);
        static SFCommand Up(int n)=>new SFCommand{jump=n==0};

        IEnumerator Movement(SFCourseChapter ch)
        {
            var lead=g.Player;var P=g.Platforms;bool rio=ch.key=="rio";deckTag=rio?"R":"A";
            var foes=Foes;var health=foes.Select(a=>a.health).ToArray();foreach(var a in foes){a.health=0;a.motor.Freeze(true);}
            Ready(g.Partner,ch.startX-1.5f,.05f);
            // Jog hops take off within .7 of a listed deck's right edge; walk-offs keep walking right until the lead has dropped below 'above'.
            bool Hop(float x,int[] decks)=>decks.Any(i=>x>=P[i].x+P[i].width-.7f&&x<P[i].x+P[i].width);
            SFCommand Jog(float stop,float h,int[] decks)=>new SFCommand{move=lead.X<stop?Vector2.right:Vector2.zero,jump=lead.motor.IsGrounded&&lead.motor.Body.linearVelocity.y<=.1f&&Near(lead.Height,h,.15f)&&Hop(lead.X,decks)};
            SFCommand WalkOff(float above)=>new SFCommand{move=lead.Height>above?Vector2.right:Vector2.zero,walk=true};
            // F6: normal commands only. A single climb, hop or drop must stand settled on its target within 1.2 s.
            if(rio)
            {
                yield return Climb("street x30 jump",30,0,Up,2);
                yield return Climb("R2 x30.5 jump",30.5f,1.5f,Up,3);
                yield return Climb("R6 x70 jump",70,1.5f,Up,7);
                yield return Climb("R7 x74 jump",74,3,Up,8);
                yield return Climb("R11 run-jump from x106.3",105.8f,4.5f,n=>new SFCommand{move=Vector2.right,run=true,jump=lead.X>=106.3f&&lead.X<107&&lead.Height>4.3f&&lead.motor.IsGrounded&&lead.motor.Body.linearVelocity.y<=.1f},14,109);
                yield return Climb("walk-off R8 at x78.5",77.6f,4.5f,n=>WalkOff(3.5f),7,78.5f);
            }
            else
            {
                yield return Climb("A2 x28 jump",28,1.5f,Up,3);
                yield return Climb("jog hops A3 > A4 > A5",26.8f,3,n=>Jog(37,3,new[]{3,4}),5,-1,4,new[]{4});
                yield return Climb("jog hops A9 > A10 > A11 > A12",70.3f,4.5f,n=>Jog(85,4.5f,new[]{9,10,11}),12,-1,5,new[]{10,11});
                yield return Climb("walk-off A7 at x62",61.2f,3,n=>WalkOff(2.2f),6,62);
                yield return Climb("walk-off A12 at x87",86.2f,4.5f,n=>WalkOff(3.7f),13);
            }
            // F7: jump held for 60 fixed steps on the deck under each tier. Climbing onto that tier is legitimate; a second takeoff is not.
            foreach(int i in rio?new[]{3,8,11,14}:new[]{1,7,9,16})yield return JumpSpam(i);
            // F8: the real CompanionCommand. Nested stacks need no catch-up; the crane, a partner parked above the lead and one hopping in place in a street gap do.
            if(rio)
            {
                yield return Catch("street x70 climbs to the lead on R8",new Vector2(76,4.5f),new Vector2(70,0),8,0,false);
                yield return Catch("street x118 climbs to the lead on R14",new Vector2(124,4.5f),new Vector2(118,0),8,0,false);
                yield return Catch("R13 above is brought down to the lead on R12",new Vector2(115,1.5f),new Vector2(118,3),4,1,true);
                yield return HopCatch("street x107.5 (the R9/R12 gap) below the lead on R11",new Vector2(104,4.5f),new Vector2(107.5f,0),5);
            }
            else
            {
                yield return Catch("A9 x71 reaches the lead on A12 (x-gap catch-up)",new Vector2(85,4.5f),new Vector2(71,4.5f),4,0,true);
                yield return Catch("the street under the crane reaches the lead on A12",new Vector2(85,4.5f),new Vector2(84,0),4,1,true);
                yield return Catch("A4 above is brought down to the lead on A2",new Vector2(35.9f,1.5f),new Vector2(34.2f,3),4,1,true);
                yield return HopCatch("street x46.5 (the A2/A6 gap) below the lead on A7",new Vector2(51,3),new Vector2(46.5f,0),5);
            }
            Info("F8 "+ch.key+" vertical catch-ups so far: "+g.VerticalCatchUps);
            for(int i=0;i<foes.Length;i++)foes[i].health=health[i];
            yield return Guards(ch);
        }

        // F6 helper: stands the lead on (x, h), then ticks it with command(step) on real physics until it stands settled on deck (at x >= minX),
        // and when via is given, it must also have stood on each of those decks on the way.
        IEnumerator Climb(string name,float x,float h,System.Func<int,SFCommand> command,int deck,float minX=-1,float seconds=1.2f,int[] via=null)
        {
            var a=g.Player;var p=g.Platforms[deck];float x0=Mathf.Max(p.x,minX),x1=p.x+p.width;
            Ready(a,x,h+.05f);yield return new WaitForSeconds(.2f);
            bool placed=a.motor.IsGrounded&&Near(a.Height,h,.12f),arrived=false;var touched=new HashSet<int>();float took=0;
            int budget=Mathf.CeilToInt(seconds/Time.fixedDeltaTime);
            for(int n=0;n<budget&&!arrived;n++)
            {
                a.Tick(Time.fixedDeltaTime,command(n));yield return new WaitForFixedUpdate();
                if(via!=null)foreach(int v in via){var q=g.Platforms[v];if(Settled(a,q.height,q.x,q.x+q.width))touched.Add(v);}
                if(Settled(a,p.height,x0,x1)){arrived=true;took=(n+1)*Time.fixedDeltaTime;}
            }
            bool path=via==null||via.All(touched.Contains);
            Check(placed&&arrived&&path,"F6 "+name+" lands on "+D(deck)+(minX>p.x?" past x"+minX:"")+(via!=null?" via "+string.Join(", ",via.Select(D)):"")+" within "+seconds+" s ("+(arrived?took.ToString("0.00")+" s at x"+a.X.ToString("0.00"):"ended at "+a.X.ToString("0.00")+"/"+a.Height.ToString("0.00"))+")");
            a.motor.SetCommand(0,false);
        }

        // F7 helper: the lead stands on the highest deck under the middle of deck i (or the street) and holds jump for 60 fixed steps.
        // Fail on a fresh takeoff (vy>8 after <1) from anywhere but a surface top, or a peak 2.2+ above the last surface it stood on.
        IEnumerator JumpSpam(int i)
        {
            var a=g.Player;var top=g.Platforms[i];float x=top.x+top.width*.5f,h=0;
            foreach(var p in g.Platforms)if(p.height<top.height-.01f&&p.height>h&&x>p.x+.3f&&x<p.x+p.width-.3f)h=p.height;
            Ready(a,x,h+.05f);yield return new WaitForSeconds(.2f);
            bool placed=a.motor.IsGrounded&&Near(a.Height,h,.12f),boost=false,wasStanding=Standing(a);float takeoff=a.Height,peak=0,previousV=0;
            Info("F7 start "+D(i)+": placed "+placed+", h "+a.Height.ToString("0.000")+", grounded "+a.motor.IsGrounded+", vy "+a.motor.Body.linearVelocity.y.ToString("0.000"));
            for(int n=0;n<60;n++)
            {
                float beforeH=a.Height,beforeV=a.motor.Body.linearVelocity.y;bool beforeGround=a.motor.IsGrounded;
                a.Tick(Time.fixedDeltaTime,new SFCommand{jump=true});yield return new WaitForFixedUpdate();
                float vy=a.motor.Body.linearVelocity.y;bool standing=Standing(a);
                if(vy>8&&previousV<1&&!wasStanding){boost=true;Info("F7 flagged "+D(i)+" step "+n+": before h "+beforeH.ToString("0.000")+" vy "+beforeV.ToString("0.000")+" grounded "+beforeGround+"; after h "+a.Height.ToString("0.000")+" vy "+vy.ToString("0.000")+" grounded "+a.motor.IsGrounded+"; previous sampled vy "+previousV.ToString("0.000"));}
                if(standing&&vy<=.1f)takeoff=a.Height;
                peak=Mathf.Max(peak,a.Height-takeoff);previousV=vy;wasStanding=standing;
            }
            Check(placed&&!boost&&peak<2.2f,"F7 jump held under "+D(i)+" from "+(h>0?"the "+h+" deck":"the street")+" at x"+x+": no mid-air takeoff (peak +"+peak.ToString("0.00")+" above the last surface)");
            a.motor.SetCommand(0,false);
        }

        // F8 helper: the lead stands still on its surface; only the partner is ticked with the real CompanionCommand while game time advances.
        // Arrived means the partner stands settled at the lead's height within 6 of it. A teleport shows up as one step longer than .5.
        IEnumerator Catch(string name,Vector2 at,Vector2 from,float seconds,int catchUps,bool teleport)
        {
            var lead=g.Player;var partner=g.Partner;
            Ready(lead,at.x,at.y+.05f);Ready(partner,from.x,from.y+.05f);yield return new WaitForSeconds(.25f);
            bool placed=Near(lead.Height,at.y,.12f)&&Near(partner.Height,from.y,.12f),arrived=false;
            Info("F8 start "+name+": placed "+placed+", lead "+lead.X.ToString("0.00")+"/"+lead.Height.ToString("0.000")+", partner "+partner.X.ToString("0.00")+"/"+partner.Height.ToString("0.000"));
            int before=g.VerticalCatchUps,budget=Mathf.CeilToInt(seconds/Time.fixedDeltaTime);float step=0,took=0;var last=partner.motor.Body.position;
            for(int n=0;n<budget&&!arrived;n++)
            {
                partner.Tick(Time.fixedDeltaTime,g.CompanionCommand());yield return new WaitForFixedUpdate();g.Elapsed+=Time.fixedDeltaTime;
                if(catchUps>0&&n%25==0)Info("F8 sample "+name+" step "+n+": lead "+lead.Height.ToString("0.000")+" grounded "+lead.motor.IsGrounded+"; partner "+partner.X.ToString("0.00")+"/"+partner.Height.ToString("0.000")+" v "+partner.motor.Body.linearVelocity.ToString("F2")+" grounded "+partner.motor.IsGrounded+"; catches "+(g.VerticalCatchUps-before));
                step=Mathf.Max(step,Vector2.Distance(partner.motor.Body.position,last));last=partner.motor.Body.position;
                if(partner.motor.IsGrounded&&Mathf.Abs(partner.motor.Body.linearVelocity.y)<.1f&&Near(partner.Height,lead.Height,.15f)&&Mathf.Abs(partner.X-lead.X)<6){arrived=true;took=(n+1)*Time.fixedDeltaTime;}
            }
            int fired=g.VerticalCatchUps-before;
            Info("F8 "+name+": "+(arrived?"arrived in "+took.ToString("0.00")+" s":"not arrived, partner at "+partner.X.ToString("0.00")+"/"+partner.Height.ToString("0.00"))+", vertical catch-ups "+fired+", largest step "+step.ToString("0.00"));
            Check(placed&&arrived&&fired==catchUps&&(teleport?step>.5f:step<.5f),"F8 "+name+" within "+seconds+" s ("+(catchUps>0?catchUps+" vertical catch-up":teleport?"by teleport":"no catch-up")+")");
            partner.motor.SetCommand(0,false);
        }

        // F8 helper: the partner stands in a street gap no deck can catch it in, far below the settled lead, and hops in place: its real
        // CompanionCommand with the move cleared and a jump on every grounded, not rising frame (the partner's own jump rule) until a
        // catch-up fires. The hops must not end the stranded run, so exactly one vertical catch-up lands it on the lead's surface.
        IEnumerator HopCatch(string name,Vector2 at,Vector2 from,float seconds)
        {
            var lead=g.Player;var partner=g.Partner;
            Ready(lead,at.x,at.y+.05f);Ready(partner,from.x,from.y+.05f);yield return new WaitForSeconds(.25f);
            bool placed=Near(lead.Height,at.y,.12f)&&Near(partner.Height,from.y,.12f),arrived=false;
            int before=g.VerticalCatchUps,budget=Mathf.CeilToInt(seconds/Time.fixedDeltaTime),jumps=0;float lastV=0;
            for(int n=0;n<budget&&!arrived;n++)
            {
                var c=g.CompanionCommand();
                if(g.VerticalCatchUps==before){c.move=Vector2.zero;c.jump=partner.motor.IsGrounded&&partner.motor.Body.linearVelocity.y<=.1f;}
                partner.Tick(Time.fixedDeltaTime,c);yield return new WaitForFixedUpdate();g.Elapsed+=Time.fixedDeltaTime;
                float vy=partner.motor.Body.linearVelocity.y;if(lastV<1&&vy>8)jumps++;lastV=vy;
                if(g.VerticalCatchUps>before&&partner.motor.IsGrounded&&Mathf.Abs(vy)<.1f&&Near(partner.Height,lead.Height,.15f))arrived=true;
            }
            int fired=g.VerticalCatchUps-before;
            Check(placed&&jumps>=3&&fired==1&&arrived,"F8 "+name+": hops in place keep the stranded run, one vertical catch-up within "+seconds+" s (hops "+jumps+", catch-ups "+fired+", partner at "+partner.X.ToString("0.00")+"/"+partner.Height.ToString("0.00")+")");
            partner.motor.SetCommand(0,false);
        }

        float minX,maxX;int hops,leadJumps;bool offDeck;
        // F11 helper: ticks one enemy with the real EnemyCommand; records its x range, fresh takeoffs (vy>8 after <1) and any drift off height h.
        // With jumpingLead the lead also holds jump (so it re-jumps on every landing) until the last .2 s, and its takeoffs are counted.
        IEnumerator Chase(SFActor e,float h,float seconds,bool jumpingLead=false)
        {
            minX=maxX=e.X;hops=leadJumps=0;offDeck=false;float lastV=0,leadV=0;int budget=Mathf.CeilToInt(seconds/Time.fixedDeltaTime);
            for(int n=0;n<budget;n++)
            {
                if(jumpingLead)g.Player.Tick(Time.fixedDeltaTime,new SFCommand{jump=n<budget-10});
                e.Tick(Time.fixedDeltaTime,g.EnemyCommand(e));yield return new WaitForFixedUpdate();
                float vy=e.motor.Body.linearVelocity.y;if(lastV<1&&vy>8)hops++;lastV=vy;
                float ly=g.Player.motor.Body.linearVelocity.y;if(leadV<1&&ly>8)leadJumps++;leadV=ly;
                minX=Mathf.Min(minX,e.X);maxX=Mathf.Max(maxX,e.X);offDeck|=!Near(e.Height,h,.12f);
            }
        }
        // Parks both heroes (settled, then invulnerable) and stands enemy e on (x, h) with its physics on.
        IEnumerator Stage(SFActor e,float x,float h,Vector2 lead,Vector2 partner)
        {
            Ready(g.Player,lead.x,lead.y+.05f);Ready(g.Partner,partner.x,partner.y+.05f);
            Ready(e,x,h+.05f);e.motor.Freeze(false);yield return new WaitForSeconds(.25f);
            g.Player.invulnerable=g.Partner.invulnerable=100;
        }
        // Returns enemy e to its data spawn and freezes it again once settled.
        IEnumerator Home(SFActor e,SFChapterEnemy d)
        {
            Ready(e,d.x,d.height+.05f);e.lane=d.lane;e.motor.Freeze(false);yield return new WaitForSeconds(.25f);e.motor.Freeze(true);
        }

        // F11: deck guards (HoldTier) keep their decks and never step off (above heroes under the deck they keep over them, not at an edge),
        // the right-edge guard sends a non-hold regular on a deck that runs to x132 toward its left edge, and a non-hold regular chasing a hero
        // under a higher deck, or one jumping beside it, never hops. Only the tested enemy (and, for the jumping case, the lead) is ticked;
        // each enemy returns to its spawn.
        IEnumerator Guards(SFCourseChapter ch)
        {
            var foes=Foes;var P=g.Platforms;bool rio=ch.key=="rio";
            int f=System.Array.FindIndex(ch.cast,e=>e.final);
            if(f<0||f>=foes.Length){Check(false,"F11 "+ch.key+" has a final boss to test");yield break;}
            int r=System.Array.FindIndex(ch.cast,e=>!e.hold&&!e.boss&&(e.id=="smuggler"||e.id=="raider"));int low=rio?4:6,high=rio?5:7;
            if(r<0||r>=foes.Length){Check(false,"F11 "+ch.key+" has a non-hold smuggler or raider to test");yield break;}
            var chaser=foes[r];var rd=ch.cast[r];
            var boss=foes[f];var fd=ch.cast[f];int bd=DeckAt(fd.x,fd.height);var deck=bd>=0?P[bd]:default(SFPlatform);string who=SFEnemyProfiles.Title(fd.id);
            var street=new Vector2(120,0);var beside=new Vector2(121.5f,0);
            // The right-edge guard is for regulars (a deck guard never steps off): the non-hold chaser stood at the boss spawn, heroes under that deck.
            yield return Stage(chaser,fd.x,fd.height,street,beside);
            var first=g.EnemyCommand(chaser);
            Check(bd>=0&&first.move.x<0&&!first.jump,"F11 right-edge guard: a non-hold "+rd.id+" at x"+fd.x+" on "+D(bd)+" above a street hero heads for its left edge (its right edge lies past x132)");
            yield return Home(chaser,rd);
            yield return Stage(boss,fd.x,fd.height,street,beside);
            yield return Chase(boss,fd.height,3);
            Check(bd>=0&&!offDeck&&hops==0&&minX>=deck.x+.25f&&maxX<=deck.x+deck.width-.25f,"F11 "+who+" on "+D(bd)+" with a hero on the street at x120 holds "+fd.height+" for 3 s (x "+minX.ToString("0.00")+".."+maxX.ToString("0.00")+", hops "+hops+")");
            // The heroes stand under the deck 6 and 7.5 right of the guard (inside the 11-unit activation range in both chapters). Its left edge is
            // the nearest one, but a deck guard never steps off, so it walks toward them along its deck instead of marching to that edge.
            yield return Stage(boss,deck.x+2,fd.height,new Vector2(deck.x+8,0),new Vector2(deck.x+9.5f,0));
            yield return Chase(boss,fd.height,2);
            Check(bd>=0&&!offDeck&&hops==0&&minX>=deck.x+1.8f&&maxX>=deck.x+3.5f,"F11 "+who+" above heroes under "+D(bd)+" walks toward them, not to its nearest edge (x "+minX.ToString("0.00")+".."+maxX.ToString("0.00")+", start "+(deck.x+2)+")");
            // The heroes on the street 6 and 4.5 left of the deck: the guard walks to its left edge, reaches its .5 stop line and stops short of it.
            yield return Stage(boss,deck.x+2,fd.height,new Vector2(deck.x-6,0),new Vector2(deck.x-4.5f,0));
            yield return Chase(boss,fd.height,2);
            Check(bd>=0&&!offDeck&&hops==0&&minX>=deck.x+.25f&&minX<=deck.x+.8f,"F11 "+who+" walking to "+D(bd)+"'s left edge reaches its .5 stop line and stops short of the edge (min x "+minX.ToString("0.00")+", edge "+deck.x+")");
            yield return Home(boss,fd);
            // A regular deck guard on the crane end, with the hero on the next girder to its left.
            int k=System.Array.FindIndex(ch.cast,e=>e.hold&&!e.boss&&e.height>=4.4f);
            if(k>=0&&k<foes.Length)
            {
                var guard=foes[k];var kd=ch.cast[k];int gi=DeckAt(kd.x,kd.height);int ni=-1;
                if(gi>=0)for(int i=0;i<P.Length;i++)if(i!=gi&&Mathf.Abs(P[i].height-P[gi].height)<.01f&&P[i].x+P[i].width<=P[gi].x&&(ni<0||P[i].x>P[ni].x))ni=i;
                if(gi<0||ni<0)Check(false,"F11 the "+kd.id+" guard at x"+kd.x+" has its deck and a neighbouring girder");
                else
                {
                    var gp=P[gi];
                    yield return Stage(guard,kd.x,kd.height,new Vector2(P[ni].x+P[ni].width*.5f,kd.height),new Vector2(gp.x-26,0));
                    yield return Chase(guard,kd.height,3);
                    Check(!offDeck&&hops==0&&minX>=gp.x+.25f&&maxX<=gp.x+gp.width-.25f,"F11 the "+kd.id+" guard on "+D(gi)+" with the hero on "+D(ni)+" never leaves its deck (x "+minX.ToString("0.00")+".."+maxX.ToString("0.00")+", hops "+hops+")");
                }
                yield return Home(guard,kd);
            }
            // No hop loop: a non-hold regular on the lower deck chases a hero standing under the higher deck (Rio R4/R5, Al Anbar A6/A7).
            yield return Stage(chaser,P[low].x+1,P[low].height,new Vector2(P[high].x+6,P[low].height),new Vector2(P[low].x-20,0));
            yield return Chase(chaser,P[low].height,6);
            Check(hops==0&&!offDeck,"F11 a non-hold "+rd.id+" on "+D(low)+" chasing a hero under "+D(high)+" for 6 s makes no hop (hops "+hops+", x "+minX.ToString("0.00")+".."+maxX.ToString("0.00")+")");
            // The same chaser starting under the higher deck while the hero re-jumps on every landing 1.5 left of that deck (so it never lands on it):
            // a step-up aimed at a hero in the air would hop the chaser onto the higher deck, then walk it off again once the hero lands.
            yield return Stage(chaser,P[high].x+1,P[low].height,new Vector2(P[high].x-1.5f,P[low].height),new Vector2(P[low].x-20,0));
            yield return Chase(chaser,P[low].height,6,true);
            Check(leadJumps>=3&&hops==0&&!offDeck,"F11 a non-hold "+rd.id+" under "+D(high)+" chasing a hero who re-jumps on "+D(low)+" for 6 s makes no hop (hero jumps "+leadJumps+", hops "+hops+", x "+minX.ToString("0.00")+".."+maxX.ToString("0.00")+")");
            yield return Home(chaser,rd);
        }

        // F8, chapter 0: Curitiba never runs the vertical catch-up. With the lead on the 2.4 deck and the partner below it for 5 s of real
        // CompanionCommand ticks, the count stays unchanged and the partner never jumps position (it may still climb the 1.1 step itself).
        IEnumerator CuritibaNoCatchUp()
        {
            g.SetState(SFState.Playing);g.enabled=false;
            var foes=Foes;var health=foes.Select(a=>a.health).ToArray();foreach(var a in foes){a.health=0;a.motor.Freeze(true);}
            var lead=g.Player;var partner=g.Partner;var high=g.Platforms[3];int before=g.VerticalCatchUps;
            Ready(lead,high.x+2.5f,high.height+.05f);Ready(partner,high.x+3,.05f);yield return new WaitForSeconds(.25f);
            bool placed=Near(lead.Height,high.height,.12f)&&partner.Height<.2f;float step=0;var last=partner.motor.Body.position;
            for(int n=0;n<250;n++)
            {
                partner.Tick(Time.fixedDeltaTime,g.CompanionCommand());yield return new WaitForFixedUpdate();g.Elapsed+=Time.fixedDeltaTime;
                step=Mathf.Max(step,Vector2.Distance(partner.motor.Body.position,last));last=partner.motor.Body.position;
            }
            Check(placed&&!g.InChapter&&!g.Chapter.verticalCatchUp&&g.VerticalCatchUps==before&&step<.5f,"F8 chapter 0: 5 s with the lead on the 2.4 deck and the partner below fire no catch-up (largest step "+step.ToString("0.00")+")");
            for(int i=0;i<foes.Length;i++)foes[i].health=health[i];
        }

        // F10: extraction needs 3 intel, the final boss down, the lead on the extraction deck and the partner within 6 and on the same level.
        // A mid-boss never unlocks it. Also checks the camera lift on the top tier.
        IEnumerator Extraction(SFCourseChapter ch)
        {
            var lead=g.Player;var partner=g.Partner;float ex=ch.extractionX,eh=ch.extractionHeight;
            var final=g.FinalBoss;var mids=g.CourseCast.Where(a=>a.boss&&a!=final).ToArray();
            Check(final!=null&&final.Alive,"F10 the final boss is alive before the extraction fixture");
            Ready(lead,ex-1,eh+.05f);Ready(partner,ex-2.5f,eh+.05f);yield return new WaitForSeconds(.3f);
            Check(lead.motor.IsGrounded&&Near(lead.Height,eh,.12f)&&Near(partner.Height,eh,.12f),"F10 both heroes stand on the extraction deck at "+eh);
            yield return Shot("extraction-deck",ex-6,1+ch.cameraLift);
            int data=g.DataCount;g.DataCount=2;
            Check(g.InteractionHint().Contains("2 OF 3 INTEL"),"F10 two intel reads '2 OF 3 INTEL'");
            g.DataCount=3;
            foreach(var m in mids){m.health=0;g.Defeated(m);Check(g.Notice==ch.midBossDownNotice&&g.Speech!="We're done here. Let's go.","F10 mid-boss "+m.identity+" down: '"+ch.midBossDownNotice+"' and no extraction line");}
            Check(!g.BossDown&&g.InteractionHint().EndsWith("STOP "+ch.bossShort),"F10 "+(mids.Length>0?"with only the mid-boss down ":"")+"the hint says STOP "+ch.bossShort);
            g.NoticeTime=0;g.TryExtract();
            Check(g.State==SFState.Playing&&g.Notice==ch.bossBlock,"F10 extraction is blocked: '"+ch.bossBlock+"'");
            final.health=0;g.Defeated(final);
            Check(g.BossDown&&g.Notice==ch.bossDownNotice,"F10 the final boss down reads '"+ch.bossDownNotice+"'");
            foreach(var m in mids){g.Defeated(m);Check(g.Notice==SFEnemyProfiles.Title(m.identity)+" DOWN  /  REACH "+ch.extractionLabel,"F10 mid-boss "+m.identity+" downed after the final boss names itself: '"+g.Notice+"'");}
            if(eh>.01f)
            {
                Ready(lead,ex-1,.05f);Ready(partner,ex-2.5f,.05f);yield return new WaitForSeconds(.3f);
                Check(lead.Height<.2f&&g.InteractionHint()==""&&!g.TryInteract()&&g.State==SFState.Playing,"F10 under the extraction deck there is no hint and no extraction");
                Ready(lead,ex-1,eh+.05f);Ready(partner,ex-2.5f,eh-1.5f+.05f);yield return new WaitForSeconds(.3f);
                Check(Near(partner.Height,eh-1.5f,.12f)&&g.InteractionHint().EndsWith("PARTNER UP HERE"),"F10 a partner one tier below (within 6) blocks the hint with 'PARTNER UP HERE'");
                g.NoticeTime=0;g.TryExtract();
                Check(g.State==SFState.Playing&&g.Notice=="Bring your partner to extraction.","F10 a partner one tier below blocks extraction with the partner text");
            }
            Ready(lead,ex-1,eh+.05f);Ready(partner,ex-2.5f,eh+.05f);yield return new WaitForSeconds(.3f);
            g.enabled=true;yield return new WaitForSeconds(1.5f);g.enabled=false;
            float y=g.Camera.transform.position.y;
            Check(ch.cameraLift<=0?Near(y,1,.001f):y>1+ch.cameraLift*.5f&&y<=1+ch.cameraLift+.001f,"camera lifts toward +"+ch.cameraLift+" with the lead on the "+eh+" tier (y "+y.ToString("0.000")+")");
            yield return Shot("extraction",g.Camera.transform.position.x,y);
            Ready(lead,ex-1,eh+.05f);Ready(partner,ex-2.5f,eh+.05f);yield return new WaitForSeconds(.3f);
            Check(g.InteractionHint().EndsWith("EXTRACT TOGETHER"),"F10 both heroes on the extraction deck: EXTRACT TOGETHER");
            Check(g.TryInteract()&&g.State==SFState.Won&&g.Notice==ch.wonNotice,"F10 extracting together wins with '"+ch.wonNotice+"'");
            g.DataCount=data;
        }

        void Tables()
        {
            var all=SFCourseChapters.All;
            Check(all.Length==3&&all[0].key=="curitiba"&&all[1].key=="rio"&&all[2].key=="anbar","chapter table holds Curitiba, Rio and Al Anbar in order");
            Check(Enumerable.Range(0,all.Length).All(i=>all[i].index==i),"chapter index matches its table position");
            Check(all[0].next==1&&all[1].next==2&&all[2].next==-1,"chapter order is Curitiba > Rio > Al Anbar > wrap");
            Check(SFCourseChapters.Get(-1)==all[0]&&SFCourseChapters.Get(99)==all[all.Length-1],"chapter lookup clamps out-of-range indices");
            Check(g.CourseMode&&g.Chapter==SFCourseChapters.Get(g.CourseChapter)&&g.InChapter==(g.CourseChapter>0),"course scene resolves its chapter from the static index");
            bool mode=g.CourseMode;g.CourseMode=false;bool campaign=g.Chapter==all[0]&&!g.InChapter;g.CourseMode=mode;
            Check(campaign,"campaign mode always reads chapter 0 and is never InChapter");
            Check(OldIds.All(id=>SFEnemyProfiles.Get(id)==null),"existing enemy ids have no profile, so their code paths stay unchanged");
            Check(OldIds.All(id=>SFEnemyProfiles.Title(id)==(id=="foreman"?"THE FOREMAN":id.ToUpperInvariant())),"existing boss-bar titles keep today's wording");
            Check(new[]{"smuggler","smuggler_shotgun","raider","merc_boss","armored_boss"}.All(id=>{var p=SFEnemyProfiles.Get(id);return p!=null&&OldIds.Contains(p.layout)&&p.hp>0&&p.speed>0&&p.scale>0&&!string.IsNullOrEmpty(p.title);}),"new enemy ids map onto existing layouts with health, speed and a title");
            Check(SFEnemyProfiles.Title("merc_boss")=="THE BROKER"&&SFEnemyProfiles.Title("armored_boss")=="THE ARCHITECT","new bosses carry their boss-bar titles");
            Check(!SFEnemyProfiles.Usable(null,1),"a missing sheet is never usable");
        }

        // F1: the chapter 0 record must equal what BuildLevel, SpawnEnemies, StartGame, Update and TryExtract produce today.
        IEnumerator NoDrift()
        {
            var ch=SFCourseChapters.All[0];
            Check(g.Platforms.Length==ch.platforms.Length&&g.Platforms.Select((p,i)=>Near(p.x,ch.platforms[i].x)&&Near(p.width,ch.platforms[i].width)&&Near(p.height,ch.platforms[i].height)).All(ok=>ok),"F1 platforms match chapter 0 data in order");
            Check(g.Hazards.Length==ch.hazards.Length&&g.Hazards.Select((x,i)=>Near(x,ch.hazards[i].x)).All(ok=>ok),"F1 hazard strips match chapter 0 data");
            Check(g.HazardHeights.Length==0&&g.HazardHalf.Length==0&&ch.hazards.All(h=>h.height==0&&Near(h.half,.75f)),"F1 chapter 0 hazards stay street strips with .75 half-width");
            Check(g.Pickups.Count==ch.pickups.Length&&g.Pickups.Select((p,i)=>p.kind==ch.pickups[i].kind&&Near(p.x,ch.pickups[i].x)&&Near(p.height,ch.pickups[i].height)&&p.lane==0).All(ok=>ok),"F1 pickups match chapter 0 data in order");
            var foes=g.Actors.Where(a=>!a.Friendly).ToArray();
            Check(foes.Length==ch.cast.Length&&foes.Select((a,i)=>a.identity==ch.cast[i].id&&Near(a.X,ch.cast[i].x,.05f)&&Near(a.lane,ch.cast[i].lane)&&a.boss==ch.cast[i].boss).All(ok=>ok),"F1 cast ids, x, lanes and boss flags match chapter 0 data");
            Check(foes.Count(a=>a.boss)==1&&foes.First(a=>a.boss).identity=="vesper"&&ch.cast.Count(e=>e.final)==1&&ch.cast.First(e=>e.final).id=="vesper","F1 only Vesper is a boss, and it is the final boss");
            Check(ch.cast.All(e=>e.height==0&&e.hp==0&&!e.hold),"F1 chapter 0 cast spawns on the street with default health and no hold");
            Check(Near(g.Checkpoint,ch.startX)&&g.CheckpointHeight==0&&ch.startHeight==0,"F1 checkpoint starts at x3, height 0");
            var renderers=g.World.GetComponentsInChildren<SpriteRenderer>(true);
            Check(Enumerable.Range(0,6).All(i=>renderers.Count(r=>r.name==ch.plateName+i)==1),"F1 plates are named '"+ch.plateName+"0..5'");
            Check(renderers.Count(r=>r.name==ch.hazardSpan)==ch.hazards.Length,"F1 exactly "+ch.hazards.Length+" '"+ch.hazardSpan+"' objects");
            var beacon=renderers.FirstOrDefault(r=>r.name=="Extraction beacon");
            Check(beacon!=null&&Near(beacon.transform.position.x,ch.extractionX)&&Near(beacon.transform.position.y,-2.7f+ch.extractionHeight),"F1 extraction beacon matches chapter 0 data");
            var props=renderers.Where(r=>r.name=="Street prop").ToArray();
            Check(props.Length==ch.props.Length&&props.Select((r,i)=>Near(r.transform.position.x,ch.props[i].x)&&Near(r.transform.position.y,-3+ch.props[i].lane)&&Near(r.transform.localScale.x*r.sprite.bounds.size.x,ch.props[i].width)).All(ok=>ok),"F1 street props match chapter 0 data in order");

            bool shake=g.ShakeEnabled;g.ShakeEnabled=false;
            g.StartGame();
            Check(g.State==SFState.Playing&&g.Speaker==ch.introSpeaker&&g.Speech==ch.intro,"F1 intro line matches chapter 0 data");
            Check(Near(g.Player.X,ch.startX,.05f)&&Near(g.Partner.X,ch.startX-1.5f,.05f),"F1 heroes start at startX and startX-1.5");
            yield return new WaitForSeconds(1);
            Check(g.Camera.transform.position.y==1f,"F1 course camera y is exactly 1 after 1 s of play");
            var cp=ch.checkpoints[0];
            Check(Near(g.Checkpoint,ch.startX)&&g.CheckpointHeight==0,"F1 checkpoint is still x3 before x"+cp.trigger);
            g.Player.Teleport(new Vector2(cp.trigger+.5f,.2f));yield return null;yield return null;
            Check(Near(g.Checkpoint,cp.x)&&g.CheckpointHeight==cp.height&&g.Notice==cp.label,"F1 checkpoint moves to x"+cp.x+" with its label past x"+cp.trigger);
            var beat=ch.beats[0];g.Say("","");
            g.Player.Teleport(new Vector2(beat.x+.5f,.2f));yield return null;yield return null;
            Check(ch.cast[beat.actor].id=="vesper"&&g.Speaker==beat.speaker&&g.Speech==beat.line,"F1 boss beat fires with chapter 0 speaker and line past x"+beat.x);
            int data=g.DataCount;g.DataCount=3;
            g.Player.Teleport(new Vector2(ch.extractionX-2,.2f));yield return null;
            Check(g.InteractionHint().EndsWith("STOP "+ch.bossName),"F1 extraction hint names the chapter 0 boss");
            g.NoticeTime=0;g.TryExtract();
            Check(g.State==SFState.Playing&&g.Notice==ch.bossBlock,"F1 blocked extraction text matches chapter 0 data");
            g.Defeated(g.Actors.First(a=>a.boss));
            Check(g.Notice==ch.bossDownNotice,"F1 boss-down notice matches chapter 0 data");
            g.SetState(SFState.Won);
            Check(g.Notice==ch.wonNotice,"F1 won notice matches chapter 0 data");
            g.DataCount=data;g.ShakeEnabled=shake;
        }

        static bool Same(Color a,Color b)=>Near(a.r,b.r,.002f)&&Near(a.g,b.g,.002f)&&Near(a.b,b.b,.002f)&&Near(a.a,b.a,.002f);

        // F5, existing ids: read in the Menu before any enemy tick, so colours are the untouched Configure render.
        void ExistingIdentities()
        {
            var foes=g.Actors.Where(a=>!a.Friendly).ToArray();var plain=new Color(.84f,.87f,.94f,1);
            Check(foes.Length>0&&foes.All(a=>a.LayoutId==a.identity&&a.ArtId==a.identity&&!a.HoldTier),"F5 latch, keel and vesper keep LayoutId and ArtId equal to their identity");
            Check(foes.All(a=>a.maxHealth==(a.identity=="vesper"?240:a.identity=="keel"?52:64)&&a.health==a.maxHealth),"F5 latch/keel/vesper health stays 64/52/240");
            Check(foes.All(a=>Same(a.visual.color,plain)&&a.visual.transform.localScale==Vector3.one),"F5 latch, keel and vesper stay untinted at unit scale");
            Check(foes.Where(a=>a.identity=="keel").All(a=>a.HasEnemyFirearm&&!a.Rifle&&a.EnemyAmmo.Capacity==6&&a.EnemyAmmo.Reserve==12&&Near(a.EnemyReloadDuration,4.1f))&&foes.Where(a=>a.identity!="keel").All(a=>!a.HasEnemyFirearm),"F5 keel keeps its shotgun kit; latch and vesper stay unarmed");
            Check(new[]{g.Fernando,g.Stefanie}.All(h=>h.LayoutId==h.identity&&h.ArtId==h.identity&&h.visual.transform.localScale==Vector3.one),"F5 heroes resolve to their own id at unit scale");
        }

        // F5, new ids: each profiled id (plus a regular foreman) spawns with spawnHeight 1.1 on the x14-19 deck, then every pose must come
        // from its ArtId set (one source, never mixed). Sweep actors stay out of g.Actors, so no AI moves them; they are destroyed afterwards.
        IEnumerator IdentitySweep()
        {
            string[] ids={"smuggler","smuggler_shotgun","raider","merc_boss","armored_boss","foreman"};
            var made=new List<SFActor>();
            for(int i=0;i<ids.Length;i++)
            {
                var go=new GameObject("F5 "+ids[i]);go.transform.SetParent(g.World,true);var actor=go.AddComponent<SFActor>();
                actor.Configure(g,ids[i],false,new Vector2(14.6f+i*.8f,0),ids[i]=="merc_boss"||ids[i]=="armored_boss",1.1f);made.Add(actor);
            }
            yield return new WaitForSeconds(1);
            Check(made.All(a=>a.motor.IsGrounded&&Mathf.Abs(a.Height-1.1f)<.15f),"F5 spawnHeight 1.1 stands every sweep actor on the x14-19 deck: "+string.Join(", ",made.Select(a=>a.identity+"@"+a.Height.ToString("0.00"))));
            var by=made.ToDictionary(a=>a.identity);var gun=by["smuggler_shotgun"];
            Check(gun.HasEnemyFirearm&&gun.ranged&&gun.Armed&&!gun.Rifle&&gun.EnemyAmmo.Capacity==6&&gun.EnemyAmmo.Loaded==6&&gun.EnemyAmmo.Reserve==12&&Near(gun.EnemyReloadDuration,4.1f),"F5 smuggler_shotgun carries the keel shotgun kit: 6+12 rounds, 4.1 s reload, no rifle");
            Check(new[]{"raider","merc_boss"}.All(id=>!by[id].ranged&&(by[id].LayoutId=="silk"||by[id].LayoutId=="cantilever")),"F5 raider and merc_boss fight as kickers (silk/cantilever layout)");
            Check(new[]{"smuggler","raider","merc_boss","armored_boss","foreman"}.All(id=>!by[id].HasEnemyFirearm&&!by[id].ranged&&!by[id].Armed),"F5 melee identities stay unarmed");
            Check(by["merc_boss"].boss&&Near(by["merc_boss"].maxHealth,260)&&by["armored_boss"].boss&&Near(by["armored_boss"].maxHealth,320)&&!by["foreman"].boss,"F5 bosses: the Broker 260 HP, the Architect 320 HP; the sweep foreman is a regular");
            var latch=g.Actors.FirstOrDefault(a=>a.identity=="latch"&&a.Alive&&!a.boss);
            Check(latch!=null&&Paired(latch),"F5 control: a Curitiba latch still engages the paired latch clinch");
            Check(!Paired(by["smuggler"]),"F5 a smuggler (latch layout) never uses the paired latch clinch");
            foreach(var a in made)
            {
                string id=a.identity;var p=SFEnemyProfiles.Get(id);bool own=a.ArtId==id;
                string info="INFO: F5 "+id+" draws "+(own?"its own art":"fallback '"+a.ArtId+"' art");log.Add(info);Debug.Log(info);
                Check(p==null?a.LayoutId==id&&own:a.LayoutId==p.layout&&(own||a.ArtId==p.layout),"F5 "+id+" resolves LayoutId "+a.LayoutId+" and ArtId "+a.ArtId);
                Check(Near(a.maxHealth,p!=null?p.hp:64)&&a.health==a.maxHealth&&!a.HoldTier,"F5 "+id+" health "+a.maxHealth+" follows its profile");
                a.action=SFAction.Idle;a.actionClock=a.invulnerable=a.moveAmount=0;a.Render(0);
                var want=new Color(.84f,.87f,.94f,1)*(p!=null&&!own?p.tint:Color.white);
                // Profile tint and scale dress only a fallback: an id's own sheets are authored at their intended size (bosses about 3.15 units).
                var size=p!=null&&!own?p.scale:1;
                Check(Same(a.visual.color,want)&&a.visual.transform.localScale==Vector3.one*size,"F5 "+id+" tint and scale follow its profile ("+(own?"own art: untinted, scale 1":"fallback art: profile tint, scale "+size)+")");
                var bad=new List<string>();
                Pose(a,"idle",bad);a.moveAmount=.6f;Pose(a,"walk",bad);a.moveAmount=0;
                foreach(var attack in new[]{SFAction.Punch,SFAction.Kick,SFAction.Shoot})
                {
                    a.cooldown=0;a.BeginAttack(attack);a.action=attack;a.actionClock=0;Pose(a,attack+" before",bad);
                    a.actionClock=a.attackAt+.05f;Pose(a,attack+" after",bad);
                }
                a.action=SFAction.Hurt;a.actionClock=.1f;Pose(a,"hurt",bad);
                a.action=SFAction.Knocked;a.actionClock=.5f;Pose(a,"knocked",bad);a.actionClock=1.8f;Pose(a,"getting up",bad);
                if(a.HasEnemyFirearm){a.action=SFAction.Reload;a.attackDuration=a.EnemyReloadDuration;a.actionClock=0;Pose(a,"reload start",bad);a.actionClock=a.attackDuration*.5f;Pose(a,"reload mid",bad);}
                a.health=0;a.action=SFAction.Down;a.actionClock=0;Pose(a,"dead",bad);
                Check(bad.Count==0,"F5 "+id+" draws idle, walk, punch/kick/shoot, hurt, knocked"+(a.HasEnemyFirearm?", reload":"")+" and dead from its "+a.ArtId+" sheets"+(bad.Count>0?": "+string.Join("; ",bad):""));
            }
            foreach(var a in made){Destroy(a.shadow.gameObject);Destroy(a.gameObject);}
        }

        // The rendered sprite must be the CurrentFrame of the sheet loaded from the actor's own ArtId.
        static void Pose(SFActor a,string state,List<string> bad)
        {
            a.Render(0);var src=string.IsNullOrEmpty(a.CurrentSheet)?null:Resources.Load<SFArt>("SF/"+(a.CurrentSheet=="enemy"?a.ArtId:a.ArtId+"_"+a.CurrentSheet));
            if(src==null||src.frames==null||a.CurrentFrame<0||a.CurrentFrame>=src.frames.Length||a.visual.sprite==null||src.frames[a.CurrentFrame]!=a.visual.sprite)bad.Add(state+" "+a.CurrentSheet+"#"+a.CurrentFrame);
        }

        // Grip e exactly as TryGrapple does, at contact spacing on the street, and ask whether the paired latch sheet would draw.
        bool Paired(SFActor e)
        {
            var p=g.Player;p.ReleaseGrip();p.health=p.maxHealth;p.action=SFAction.Idle;p.Armed=false;p.FieldUniform=false;p.facing=1;
            p.Teleport(new Vector2(40,0));p.lane=0;e.Teleport(new Vector2(40.85f,0));e.lane=0;
            p.GripTarget=e;e.HeldBy=p;p.action=SFAction.Grapple;e.action=SFAction.Held;
            bool paired=p.UsePairedClinch;p.ReleaseGrip();return paired;
        }

        void Finish()
        {
            bool pass=log.Count>0&&log.All(l=>!l.StartsWith("FAIL"));
            string result=(g.InChapter?"CHAPTER CHECK "+g.CourseChapter+" COMPLETE: ":"CHAPTER DATA QA COMPLETE: ")+(pass?"PASS":"FAIL");
            log.Add(result);Debug.Log(result);
            if(Application.platform==RuntimePlatform.WebGLPlayer)return;
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(path);
            File.WriteAllLines(Path.Combine(path,g.InChapter?"chapter-check-"+g.CourseChapter+".txt":"chapter-data-check.txt"),log);Application.Quit(pass?0:2);
        }
    }

    // F3: pure-data layout rules for chapters 1+. Hero walk 2.496 / jog 3.744 / run 5.2, gravity 9.81 x 2.7, run-jump reach 4.06.
    // A walk-off leaves the edge .14 past it and falls freely; it lands on the highest deck under it when it reaches that deck's height.
    public static class SFChapterLayoutRules
    {
        const float Gravity=9.81f*2.7f,EdgeLead=.14f,MaxHeight=4.5f,DeadLow=1.75f,DeadHigh=2.45f,DeadReach=3.2f,HopGap=1.5f,HopReach=4.06f,HopSpare=.3f;
        static readonly float[] Speeds={2.496f,3.744f,5.2f};
        static readonly string[] OldIds={"latch","keel","vesper","silk","ratchet","cantilever","foreman"};
        static readonly string[] Kinds={"data","medical","food","drink","ammo"};
        // Documented exceptions: Al Anbar's crane girders A10-A12 and the A13 step-down have no support below; Rio's optional R11 > R14 leap is a 2.0 gap.
        static bool Unsupported(SFCourseChapter ch,int i)=>ch.key=="anbar"&&i>=10&&i<=13;
        static float HopLimit(SFCourseChapter ch,int a,int b)=>ch.key=="rio"&&a==11&&b==14?2f:HopGap;

        public static List<string> ValidateAll()
        {
            var lines=new List<string>();var chapters=SFCourseChapters.All.Where(c=>c.index>0).ToArray();
            foreach(var ch in chapters)lines.AddRange(Validate(ch));
            var ids=chapters.Select(c=>new HashSet<string>(c.cast.Select(e=>e.id))).ToArray();bool disjoint=true;
            for(int i=0;i<ids.Length;i++)for(int j=i+1;j<ids.Length;j++)if(ids[i].Overlaps(ids[j]))disjoint=false;
            lines.Add((disjoint?"PASS: ":"FAIL: ")+"F3 chapters 1+ share no enemy ids");
            return lines;
        }

        // Highest deck below h that the falling body is over when it reaches that deck's height; -1 is the street.
        static int Land(SFPlatform[] p,float edge,int dir,float h,float v,out float x)
        {
            int best=-1;float top=0;
            for(int i=0;i<p.Length;i++){if(p[i].height>=h-.05f||p[i].height<=top)continue;float at=edge+dir*(EdgeLead+v*Mathf.Sqrt(2*(h-p[i].height)/Gravity));if(at>=p[i].x&&at<=p[i].x+p[i].width){best=i;top=p[i].height;}}
            x=edge+dir*(EdgeLead+v*Mathf.Sqrt(2*(h-top)/Gravity));return best;
        }

        public static List<string> Validate(SFCourseChapter ch)
        {
            var lines=new List<string>();var p=ch.platforms;string tag="F3 "+ch.key+" ";
            void Rule(string name,List<string> failures)=>lines.Add(failures.Count==0?"PASS: "+tag+name:"FAIL: "+tag+name+": "+string.Join("; ",failures));
            string D(int i)=>i==-1?"street":i<0?"nothing":"#"+i+"("+p[i].x+"-"+(p[i].x+p[i].width)+"@"+p[i].height+")";
            // -1 street, -2 no surface, else the deck index at that height covering x (inset from both ends).
            int Surface(float x,float h,float inset=0){if(h<.01f)return -1;for(int i=0;i<p.Length;i++)if(Mathf.Abs(p[i].height-h)<.01f&&x>=p[i].x+inset-.001f&&x<=p[i].x+p[i].width-inset+.001f)return i;return -2;}
            float Gap(int a,int b)=>Mathf.Max(p[a].x,p[b].x)-Mathf.Min(p[a].x+p[a].width,p[b].x+p[b].width);
            float Half(SFChapterHazard z)=>z.half>0?z.half:.75f;
            var hazardSurface=ch.hazards.Select(z=>Surface(z.x,z.height)).ToArray();

            var bad=new List<string>();
            for(int i=0;i<p.Length;i++)if(p[i].height<=0||p[i].height>MaxHeight+.001f||p[i].width<=0)bad.Add(D(i));
            Rule("deck heights in (0, 4.5]",bad);

            bad=new List<string>();
            for(int i=0;i<p.Length;i++){if(p[i].height>DeadLow&&p[i].height<DeadHigh)bad.Add(D(i)+" vs street");for(int j=i+1;j<p.Length;j++){float dh=Mathf.Abs(p[i].height-p[j].height);if(dh>DeadLow&&dh<DeadHigh&&Gap(i,j)<=DeadReach)bad.Add(D(i)+" vs "+D(j));}}
            Rule("no deck pair with dh in (1.75, 2.45) within 3.2",bad);

            bad=new List<string>();
            for(int i=0;i<p.Length;i++){if(p[i].height<=DeadLow||Unsupported(ch,i))continue;bool supported=false;for(int j=0;j<p.Length;j++){float dh=p[i].height-p[j].height;if(dh>=1.25f-.001f&&dh<=1.75f+.001f&&-Gap(i,j)>=1.5f-.001f)supported=true;}if(!supported)bad.Add(D(i));}
            Rule("every deck above 1.75 has a support 1.25-1.75 below overlapping >=1.5 (crane whitelist)",bad);

            bad=new List<string>();
            for(int i=0;i<p.Length;i++)for(int j=i+1;j<p.Length;j++)
            {
                float gap=Gap(i,j);if(Mathf.Abs(p[i].height-p[j].height)>.01f||gap<=0||gap>DeadReach)continue;
                int a=p[i].x<p[j].x?i:j,b=a==i?j:i;float need=HopReach-gap+HopSpare;
                if(gap>HopLimit(ch,a,b)+.001f)bad.Add(D(a)+" > "+D(b)+" gap "+gap);
                if(Mathf.Min(p[a].width,p[b].width)<need-.001f)bad.Add(D(a)+" <> "+D(b)+" landing deck narrower than "+need);
            }
            Rule("same-height gap hops <=1.5 (R11 > R14 <=2.0) onto decks >=4.06-gap+.3 wide",bad);

            bad=new List<string>();
            for(int i=0;i<p.Length;i++)foreach(int dir in new[]{-1,1})
            {
                float edge=dir<0?p[i].x:p[i].x+p[i].width;
                if(edge<=.71f||edge>=ch.levelEnd-.01f||Enumerable.Range(0,p.Length).Any(j=>j!=i&&Mathf.Abs(p[j].height-p[i].height)<.01f&&edge>=p[j].x-.01f&&edge<=p[j].x+p[j].width+.01f))continue;
                foreach(float v in Speeds){int s=Land(p,edge,dir,p[i].height,v,out float x);for(int k=0;k<ch.hazards.Length;k++)if(hazardSurface[k]==s&&Mathf.Abs(x-ch.hazards[k].x)<1f)bad.Add(D(i)+(dir<0?" left":" right")+" at "+v+" lands "+x.ToString("0.00")+" on "+D(s)+" by hazard "+ch.hazards[k].x);}
            }
            Rule("walk-off landings (walk/jog/run) keep >=1.0 from same-surface hazard centres",bad);

            bad=new List<string>();
            for(int k=0;k<ch.hazards.Length;k++)
            {
                var z=ch.hazards[k];int s=hazardSurface[k];
                if(string.IsNullOrEmpty(z.kind)||string.IsNullOrEmpty(z.live)||string.IsNullOrEmpty(z.idle))bad.Add("hazard "+z.x+" text");
                if(s==-2)bad.Add("hazard "+z.x+"@"+z.height+" is not on a deck");
                else if(s>=0){float left=z.x-Half(z)-p[s].x,right=p[s].x+p[s].width-(z.x+Half(z));if(left<2.2f-.001f||right<2.2f-.001f)bad.Add("hazard "+z.x+" edge margin "+left.ToString("0.00")+"/"+right.ToString("0.00")+" on "+D(s));}
                else for(int i=0;i<p.Length;i++)if(p[i].height<=DeadLow&&p[i].x<z.x+Half(z)&&p[i].x+p[i].width>z.x-Half(z))bad.Add("street hazard "+z.x+" under "+D(i));
            }
            Rule("deck hazards keep >=2.2 edge margin; street hazards have no deck <=1.75 overhead",bad);

            bad=new List<string>();var intel=ch.pickups.Where(q=>q.kind=="data").ToArray();if(intel.Length!=3)bad.Add(intel.Length+" intel");
            foreach(var q in intel)
            {
                int s=-2;for(int i=0;i<p.Length;i++)if(Mathf.Abs(p[i].height+.75f-q.height)<.01f&&q.x-1.1f>=p[i].x-.001f&&q.x+1.1f<=p[i].x+p[i].width+.001f)s=i;
                if(s<0){bad.Add("intel "+q.x+"@"+q.height+" is not at deck+.75 with 1.1 inside");continue;}
                if(Mathf.Abs(.8f-q.height)<.8f)bad.Add("intel "+q.x+" is collectible from the street");
                for(int i=0;i<p.Length;i++)if(i!=s&&Mathf.Abs(p[i].height+.8f-q.height)<.8f&&p[i].x<q.x+1.1f&&p[i].x+p[i].width>q.x-1.1f)bad.Add("intel "+q.x+" is collectible from "+D(i));
            }
            Rule("3 intel at deck+.75, 1.1 inside the deck, collectible from no other surface",bad);

            bad=new List<string>();
            foreach(var q in ch.pickups)
            {
                if(!Kinds.Contains(q.kind))bad.Add("pickup kind "+q.kind);
                int s=Surface(q.x,q.height-(q.kind=="data"?.75f:.8f));
                if(s==-2){if(q.kind!="data")bad.Add(q.kind+" "+q.x+"@"+q.height+" is not at deck+.8");continue;}
                if(q.x<=.7f||q.x>=ch.levelEnd)bad.Add(q.kind+" "+q.x+" is outside the level");
                for(int k=0;k<ch.hazards.Length;k++)if(hazardSurface[k]==s&&Mathf.Abs(q.x-ch.hazards[k].x)<Half(ch.hazards[k])+.4f)bad.Add(q.kind+" "+q.x+" is inside hazard "+ch.hazards[k].x);
            }
            Rule("supplies at deck+.8; every pickup outside same-surface hazards",bad);

            // A pickup under a deck less than a tier above its surface overlaps that deck's slab, and the deck above cannot collect it.
            bad=new List<string>();
            foreach(var q in ch.pickups)
            {
                int s=Surface(q.x,q.height-(q.kind=="data"?.75f:.8f));if(s==-2)continue;float floor=s<0?0:p[s].height;
                for(int i=0;i<p.Length;i++)if(i!=s&&p[i].height>floor+.01f&&p[i].height<floor+1.25f&&q.x>p[i].x-.45f&&q.x<p[i].x+p[i].width+.45f)bad.Add(q.kind+" "+q.x+"@"+q.height+" sits under "+D(i));
            }
            Rule("no pickup sits under a deck less than 1.25 above its surface (x +-.45)",bad);

            bad=new List<string>();var cast=ch.cast.OrderBy(e=>e.x).ToArray();
            foreach(var e in cast)
            {
                if(SFEnemyProfiles.Get(e.id)==null&&!OldIds.Contains(e.id))bad.Add("unknown id "+e.id);
                if(e.id=="latch"||e.id=="keel"||e.id=="vesper")bad.Add("Curitiba id "+e.id);
                if(e.height<.01f){if(e.x<=.7f||e.x>=ch.levelEnd)bad.Add(e.id+" "+e.x+" is outside the level");}
                else if(Surface(e.x,e.height,.3f)<0)bad.Add(e.id+" "+e.x+"@"+e.height+" is not standing on a deck");
                if(Mathf.Abs(e.lane)>.55f)bad.Add(e.id+" lane "+e.lane);
            }
            for(int i=1;i<cast.Length;i++)if(cast[i].id==cast[i-1].id)bad.Add("identical neighbours "+cast[i].id+" at "+cast[i-1].x+"/"+cast[i].x);
            int regular=cast.Where(e=>!e.boss).Select(e=>e.id).Distinct().Count();if(regular<3)bad.Add(regular+" regular ids");
            Rule("cast stands on its decks, no identical neighbours, >=3 regular ids, no latch/keel/vesper",bad);

            bad=new List<string>();var finals=ch.cast.Where(e=>e.final).ToArray();
            if(finals.Length!=1)bad.Add(finals.Length+" final bosses");
            else{if(!finals[0].boss||!finals[0].hold)bad.Add("the final boss must be a boss and hold its deck");if(SFEnemyProfiles.Title(finals[0].id)!=ch.bossName)bad.Add("boss bar "+SFEnemyProfiles.Title(finals[0].id)+" vs "+ch.bossName);}
            if(ch.cast.Any(e=>e.boss&&!e.final)&&string.IsNullOrEmpty(ch.midBossDownNotice))bad.Add("mid-boss without midBossDownNotice");
            Rule("exactly one final boss, and it holds its deck",bad);

            bad=new List<string>();
            foreach(var c in ch.checkpoints)
            {
                int a=Surface(c.x,c.height,.3f),b=Surface(c.x-1.5f,c.height,.3f);
                if(string.IsNullOrEmpty(c.label))bad.Add("checkpoint "+c.x+" label");
                if(a==-2||a!=b)bad.Add("checkpoint "+c.x+"@"+c.height+" and its partner spot are not on one deck .3 inside");
                if(a==-1&&(c.x-1.5f<1f||c.x>ch.levelEnd-.3f))bad.Add("checkpoint "+c.x+" is outside the street bounds");
                for(int k=0;k<ch.hazards.Length;k++)if(a!=-2&&hazardSurface[k]==a&&(Mathf.Abs(c.x-ch.hazards[k].x)<2.95f||Mathf.Abs(c.x-1.5f-ch.hazards[k].x)<2.95f))bad.Add("checkpoint "+c.x+" is near hazard "+ch.hazards[k].x);
            }
            Rule("checkpoint and partner spots on one deck, .3 inside, >=2.95 from its hazards",bad);

            bad=new List<string>();
            if(ch.extractionHeight>=.01f&&!Enumerable.Range(0,p.Length).Any(i=>Mathf.Abs(p[i].height-ch.extractionHeight)<.01f&&p[i].x<=ch.extractionX-3+.001f&&p[i].x+p[i].width>=ch.extractionX+1-.001f))bad.Add("no deck covers "+(ch.extractionX-3)+".."+(ch.extractionX+1)+"@"+ch.extractionHeight);
            if(ch.extractionX+1>ch.levelEnd+.001f)bad.Add("extraction runs past levelEnd");
            Rule("the extraction deck covers [x-3, x+1]",bad);

            // Reachability from the street, so a data tweak cannot strand a goal behind a gap no other rule sees. Every deck up to 1.75 is reached
            // from the street; a reached deck reaches a deck 1.25-1.75 above that overlaps it by >=1.5 (climb), a same-height deck within the hop
            // limit (hop), and wherever Land() puts a walk/jog/run walk-off from an edge not covered by a same-height deck (drop).
            bad=new List<string>();var reached=new bool[p.Length];var open=new Queue<int>();
            void Reach(int i){if(i>=0&&!reached[i]){reached[i]=true;open.Enqueue(i);}}
            for(int i=0;i<p.Length;i++)if(p[i].height<=DeadLow+.001f)Reach(i);
            while(open.Count>0)
            {
                int a=open.Dequeue();
                for(int b=0;b<p.Length;b++)
                {
                    float dh=p[b].height-p[a].height;int l=p[a].x<p[b].x?a:b,r=l==a?b:a;
                    if(dh>=1.25f-.001f&&dh<=1.75f+.001f&&-Gap(a,b)>=1.5f-.001f)Reach(b);
                    if(b!=a&&Mathf.Abs(dh)<.01f&&Gap(a,b)<=HopLimit(ch,l,r)+.001f)Reach(b);
                }
                foreach(int dir in new[]{-1,1})
                {
                    float edge=dir<0?p[a].x:p[a].x+p[a].width;
                    if(edge<=.71f||edge>=ch.levelEnd-.01f||Enumerable.Range(0,p.Length).Any(j=>j!=a&&Mathf.Abs(p[j].height-p[a].height)<.01f&&edge>=p[j].x-.01f&&edge<=p[j].x+p[j].width+.01f))continue;
                    foreach(float v in Speeds)Reach(Land(p,edge,dir,p[a].height,v,out _));
                }
            }
            var goals=new List<(string what,int deck)>();
            foreach(var q in ch.pickups){int s=Surface(q.x,q.height-(q.kind=="data"?.75f:.8f));if(s>=0)goals.Add((q.kind+" "+q.x,s));}
            foreach(var e in ch.cast){int s=e.height<.01f?-1:Surface(e.x,e.height,.3f);if(s>=0)goals.Add((e.id+" "+e.x,s));}
            foreach(var c in ch.checkpoints){int s=Surface(c.x,c.height,.3f);if(s>=0)goals.Add(("checkpoint "+c.x,s));}
            if(ch.startHeight>=.01f){int s=Surface(ch.startX,ch.startHeight,.3f);if(s>=0)goals.Add(("start "+ch.startX,s));}
            for(int i=0;i<p.Length;i++)if(Unsupported(ch,i))goals.Add(("whitelisted deck",i));
            foreach(var goal in goals)if(!reached[goal.deck])bad.Add(goal.what+" on unreachable "+D(goal.deck));
            var exits=Enumerable.Range(0,p.Length).Where(i=>ch.extractionHeight>=.01f&&Mathf.Abs(p[i].height-ch.extractionHeight)<.01f&&p[i].x<=ch.extractionX-3+.001f&&p[i].x+p[i].width>=ch.extractionX+1-.001f).ToArray();
            if(exits.Length>0&&!exits.Any(i=>reached[i]))bad.Add("extraction on unreachable "+D(exits[0]));
            Rule("every pickup, cast, checkpoint, extraction and whitelisted deck is reachable from the street (climb, hop, walk-off)",bad);

            bad=new List<string>();
            string[] text={ch.key,ch.city,ch.title,ch.subtitle,ch.briefing,ch.startLabel,ch.platePrefix,ch.plateFallback,ch.plateName,ch.introSpeaker,ch.intro,ch.bossName,ch.bossShort,ch.bossBlock,ch.bossDownNotice,ch.extractionLabel,ch.goalRow,ch.touchJumpRow,ch.winTitle,ch.winLine,ch.wonNotice,ch.nextLabel,ch.hazardSpan};
            if(text.Any(string.IsNullOrEmpty))bad.Add("missing chapter text");
            foreach(var beat in ch.beats)if(beat.actor>=ch.cast.Length||string.IsNullOrEmpty(beat.speaker)||string.IsNullOrEmpty(beat.line))bad.Add("beat at "+beat.x);
            if(ch.next>=SFCourseChapters.All.Length)bad.Add("next "+ch.next);
            if(ch.musicRoute<0||ch.musicRoute>=SFGame.RouteNames.Length)bad.Add("music route "+ch.musicRoute);
            if(ch.startHeight>=.01f?Surface(ch.startX,ch.startHeight,.3f)<0||Surface(ch.startX-1.5f,ch.startHeight,.3f)<0:ch.startX-1.5f<1f)bad.Add("start spot");
            Rule("text, beats, next chapter, music route and start spot are complete",bad);
            return lines;
        }
    }
}
