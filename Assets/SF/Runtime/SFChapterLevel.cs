using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StefanieAndFernando
{
    // Course chapters 1+ (Rio, Al Anbar). Chapter 0 (Curitiba) never enters this path: it keeps the literal BuildLevel/SpawnEnemies code,
    // and every shared hook in SFGame/SFHUD either reduces to today's expression when InChapter is false or is an InChapter ternary.
    // The chapter flow (AdvanceCourse/GoToChapter) is the one addition every chapter shares, Curitiba's Won card included.
    public sealed partial class SFGame
    {
        // Chapter-only state. finalBoss stays null in chapter 0, so BossDown keeps today's any-boss rule there.
        SFActor finalBoss;readonly List<SFActor> courseCast=new List<SFActor>();int checkpointIndex;float cameraLift;
        internal SFActor FinalBoss=>finalBoss;internal IReadOnlyList<SFActor> CourseCast=>courseCast;internal float CameraLift=>cameraLift;
        // Vertical catch-up: game time the current stranded run began and the last call that saw it (a gap in calls restarts the run),
        // the partner's floor when it began, and the partner it times (a lead swap starts a new run).
        float strandedSince=-1,strandedSeen=-1,strandedFloor;SFActor strandedBuddy;internal int VerticalCatchUps;

        // Loads the chapter layout into the shared level fields before the level is built. Awake calls it only when InChapter.
        void ApplyChapter()
        {
            var ch=Chapter;
            Platforms=(SFPlatform[])ch.platforms.Clone();Hazards=ch.hazards.Select(h=>h.x).ToArray();HazardHeights=ch.hazards.Select(h=>h.height).ToArray();HazardHalf=ch.hazards.Select(h=>h.half>0?h.half:.75f).ToArray();
            Checkpoint=ch.startX;CheckpointHeight=ch.startHeight;RouteIndex=ch.musicRoute;beatFired=new bool[ch.beats.Length];if(carriedLead>=0){Selected=carriedLead;carriedLead=-1;}
        }

        // Hazard helpers. Chapter 0 has no HazardHeights/HazardHalf, so each helper reduces to today's street rule (height 0, half-width .75).
        internal float HzH(int i)=>i<HazardHeights.Length?HazardHeights[i]:0;
        internal float HzW(int i)=>i<HazardHalf.Length?HazardHalf[i]:.75f;
        // Same level as hazard i. Street strips count at every height in chapter 0 (today's rule) and only on the street in chapters.
        internal bool HzLevel(float h,int i)=>HzH(i)<=0?(!InChapter||h<.5f):Mathf.Abs(h-HzH(i))<.5f;
        // Standing within band of hazard i's surface. For a street strip this is today's h<band test.
        internal bool HzDeck(float h,int i,float band)=>HzH(i)<=0?h<band:Mathf.Abs(h-HzH(i))<band;

        // Extraction and boss gates. In chapter 0 these are exactly today's x>127 and any-boss-down rules.
        internal bool ExtractionReach=>InChapter?Player.X>Chapter.extractionX-3&&Mathf.Abs(Player.Height-Chapter.extractionHeight)<.6f:Player.X>127;
        internal bool BossDown=>finalBoss!=null?!finalBoss.Alive:Actors.Any(a=>a.boss&&!a.Alive);
        internal bool PartnerLevel=>!InChapter||Mathf.Abs(Partner.Height-Player.Height)<1;

        // Plate slot i tries <prefix>i, then plate_<prefix>i, then <fallback>i. Only a single-frame SFArt with a live frame counts, so a sheet
        // the importer sliced into 8 frames is skipped rather than drawn. Returns null (and id null) when nothing usable exists. Never throws.
        internal static SFArt LoadPlate(SFCourseChapter ch,int i,out string id)
        {
            bool prefix=!string.IsNullOrEmpty(ch.platePrefix);
            foreach(string name in new[]{prefix?ch.platePrefix+i:null,prefix?"plate_"+ch.platePrefix+i:null,string.IsNullOrEmpty(ch.plateFallback)?null:ch.plateFallback+i})
            {
                if(name==null)continue;
                var art=Resources.Load<SFArt>("SF/"+name);
                if(art!=null&&art.frames!=null&&art.frames.Length==1&&art.frames[0]!=null){id=name;return art;}
            }
            id=null;return null;
        }

        void BuildChapterLevel(SFCourseChapter ch)
        {
            // Six plates on the Curitiba grid (x 23i+11.5, y 1, 24 wide). A missing slot is skipped with a warning.
            plates=new SpriteRenderer[6];
            for(int i=0;i<6;i++)
            {
                var art=LoadPlate(ch,i,out string id);
                if(art==null){Debug.LogWarning("SF plate missing "+ch.platePrefix+i+": no usable art or fallback, slot left empty");continue;}
                if(!string.IsNullOrEmpty(ch.plateFallback)&&id==ch.plateFallback+i)Debug.LogWarning("SF plate fallback "+ch.platePrefix+i+" -> "+id);
                var sr=CreateSprite(ch.plateName+i,art.frames[0],Color.white,-100+i);
                sr.transform.SetParent(World,false);sr.transform.position=new Vector3(i*23+11.5f,1,0);
                sr.transform.localScale=Vector3.one*(24/sr.sprite.bounds.size.x);plates[i]=sr;
            }
            var ground=new GameObject("SF Ground - classroom collision layer");ground.layer=6;ground.transform.SetParent(World);ground.transform.position=new Vector3(66,-.25f,0);
            ground.AddComponent<BoxCollider2D>().size=new Vector2(140,.5f);
            foreach(var p in Platforms)
            {
                var go=new GameObject("Jump platform");go.layer=6;go.transform.SetParent(World);go.transform.position=new Vector3(p.x+p.width/2,p.height-.08f,0);
                var col=go.AddComponent<BoxCollider2D>();col.size=new Vector2(p.width,.16f);col.usedByEffector=true;
                var eff=go.AddComponent<PlatformEffector2D>();eff.useOneWay=true;eff.surfaceArc=165;
                ChapterDeck(p,ch.platformStyle);
            }
            foreach(var q in ch.pickups)AddPickup(q.x,q.height,0,q.kind);
            foreach(var q in ch.props)AddProp(q.sheet,q.frame,q.x,q.lane,q.width);
            for(int i=0;i<Hazards.Length;i++)ChapterHazard(ch,i);
            var extraction=CreateSprite("Extraction beacon",Ring,Teal,60);extraction.transform.SetParent(World);extraction.transform.position=new Vector3(ch.extractionX,-2.7f+ch.extractionHeight,0);extraction.transform.localScale=new Vector3(2.2f,.45f,1);
            stageProps.Add(extraction);
            Interaction("Extraction goal",ch.extractionX,1.5f+ch.extractionHeight,5,3).goal=true;
        }

        // Deck art by chapter style: 1 laje (concrete slab on brick piers), 2 girder (steel on scaffold poles), otherwise today's look.
        // Supports stop at the highest deck below them (the street is 0), so stacked roofs do not show piers through the lower decks.
        void ChapterDeck(SFPlatform p,int style)
        {
            float cx=p.x+p.width/2,top=-3+p.height;
            float Base(float x){float b=0;foreach(var q in Platforms)if(q.height<p.height-.01f&&q.height>b&&x>=q.x&&x<=q.x+q.width)b=q.height;return b;}
            void Support(string name,float x,float w,Color color){float b=Base(x);Box(name,x,-3+(b+p.height)/2,w,p.height-b,color,59);}
            if(style==1)
            {
                Box("Laje depth",cx,top-.12f,p.width,.3f,new Color(.30f,.26f,.23f),60);
                Box("Laje lip",cx,top+.02f,p.width,.055f,new Color(.78f,.70f,.60f),61);
                int piers=Mathf.Max(1,Mathf.CeilToInt((p.width-.4f)/5));float step=(p.width-.4f)/piers;
                for(int s=0;s<=piers;s++)Support("Brick pier",p.x+.2f+s*step,.3f,new Color(.50f,.29f,.21f));
            }
            else if(style==2)
            {
                Box("Girder depth",cx,top-.09f,p.width,.22f,new Color(.24f,.22f,.20f),60);
                Box("Girder lip",cx,top+.02f,p.width,.055f,new Color(.86f,.52f,.18f),61);
                for(float x=p.x+.25f;x<p.x+p.width;x+=.5f)Box("Girder rivet",x,top-.09f,.045f,.045f,Gold*.8f,62);
                int bays=Mathf.Max(1,Mathf.CeilToInt((p.width-.36f)/4));float step=(p.width-.36f)/bays;var steel=new Color(.46f,.47f,.46f);
                for(int s=0;s<=bays;s++)Support("Scaffold pole",p.x+.18f+s*step,.08f,steel);
                // One diagonal brace across the first bay, from the left pole's foot to the deck underside.
                float x0=p.x+.18f,y0=-3+Base(x0),x1=x0+step,y1=top-.2f;var d=new Vector2(x1-x0,y1-y0);
                var brace=Box("Scaffold brace",(x0+x1)/2,(y0+y1)/2,d.magnitude,.06f,steel,59);brace.transform.eulerAngles=new Vector3(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
            }
            else
            {
                Box("Platform depth",cx,top-.12f,p.width,.3f,new Color(.11f,.16f,.18f),60);
                Box("Platform lip",cx,top+.02f,p.width,.055f,new Color(.51f,.63f,.62f),61);
                for(float x=p.x+.25f;x<p.x+p.width;x+=.75f)Box("Platform rivet",x,top-.08f,.045f,.045f,Gold*.8f,62);
                for(int s=0;s<2;s++)Support("Platform support",p.x+(s==0?.18f:p.width-.18f),.12f,new Color(.08f,.12f,.14f));
            }
        }

        // A street hazard draws today's strip under the chapter's span name and tint. An elevated one draws a .45-tall strip, glow and
        // markers on its deck at y -3+h+.05, sorted 63-66 so they sit above the deck depth (60) and lip (61) but below the actors.
        void ChapterHazard(SFCourseChapter ch,int i)
        {
            float x=Hazards[i],h=HzH(i),half=HzW(i);var tint=ch.hazardTint;bool street=h<=0;
            float y=street?-3.02f:-3+h+.05f,tall=street?1.2f:.45f;int order=street?47:63;
            Interaction(ch.hazardSpan+" hazard",x,h+.3f,half*2+.3f,.6f).hazard=true;
            Box(ch.hazardSpan+" fault",x,y,half*2+.3f,tall,new Color(.055f,.09f,.10f,.7f),order);
            hazardWashes.Add(Box(ch.hazardSpan,x,y,half*2,tall,new Color(tint.r,tint.g,tint.b,0),order+1));
            var glow=CreateSprite(ch.hazardSpan+" glow",Ellipse,new Color(tint.r,tint.g,tint.b,.08f),street?48:65);
            glow.transform.SetParent(World);glow.transform.position=new Vector3(x,y,0);glow.transform.localScale=new Vector3(half*2+.3f,tall,1);
            stageProps.Add(glow);hazardStrips.Add(glow);
            for(int m=0;m<6;m++){var b=Box("Hazard marker",x-half+m*half*.4f,street?-3.04f:y,.12f,.08f,Gold,street?56:66);b.transform.eulerAngles=new Vector3(0,0,-25);}
        }

        // Cast from data: spawn height from the deck, optional health override, deck-hold flag, and the final boss that gates extraction.
        void SpawnChapterEnemies(SFCourseChapter ch)
        {
            courseCast.Clear();finalBoss=null;
            foreach(var e in ch.cast)
            {
                var actor=CreateActor(e.id,false,new Vector2(e.x,e.lane),e.boss,null,e.height);
                if(e.hp>0)actor.maxHealth=actor.health=e.hp;
                actor.HoldTier=e.hold;if(e.final)finalBoss=actor;courseCast.Add(actor);
            }
        }

        // StartGame's chapter branch: both heroes at the chapter start (partner 1.5 behind on the same surface) and the chapter intro line.
        void StartChapter()
        {
            var ch=Chapter;
            Player.Teleport(new Vector2(ch.startX,ch.startHeight+.2f));Partner.Teleport(new Vector2(ch.startX-1.5f,ch.startHeight+.2f));
            cameraX=10;cameraLift=0;Say(ch.introSpeaker,ch.intro);Audio.StartAmbience();
        }

        // Replaces chapter 0's Update triggers in chapters. Checkpoints advance by index when the lead passes the trigger, at any height.
        // Each beat fires once when the lead passes its x, and speaks only if its actor is still alive (or the beat has no actor).
        internal void TickChapterTriggers()
        {
            var ch=Chapter;
            if(checkpointIndex<ch.checkpoints.Length&&Player.X>ch.checkpoints[checkpointIndex].trigger){var c=ch.checkpoints[checkpointIndex++];Checkpoint=c.x;CheckpointHeight=c.height;Notify(c.label);}
            for(int i=0;i<ch.beats.Length&&i<beatFired.Length;i++)
            {
                var b=ch.beats[i];if(beatFired[i]||Player.X<=b.x)continue;beatFired[i]=true;
                var who=b.actor>=0&&b.actor<courseCast.Count?courseCast[b.actor]:null;
                if(b.actor<0||who!=null&&who.Alive)Say(b.speaker,b.line);
            }
        }

        // Deck guard (SFChapterEnemy.hold, so chapters only): never jump, and stop .5 short of either edge of the deck it stands on.
        // Off a deck (street, or mid-air) only the jump is cleared.
        void HoldCommand(SFActor a,ref SFCommand c)
        {
            c.jump=false;if(!a.motor.IsGrounded||a.Height<.3f)return;
            var p=Platforms.FirstOrDefault(q=>a.X>q.x-.3f&&a.X<q.x+q.width+.3f&&Mathf.Abs(a.Height-q.height)<.25f);
            if(p.width>0&&(c.move.x>0&&a.X>=p.x+p.width-.5f||c.move.x<0&&a.X<=p.x+.5f))c.move.x=0;
        }

        // Index of the deck an actor stands on (HoldCommand's lookup), or -1 on the street or off every deck.
        int DeckIndex(SFActor a)=>System.Array.FindIndex(Platforms,p=>a.X>p.x-.3f&&a.X<p.x+p.width+.3f&&Mathf.Abs(a.Height-p.height)<.25f);
        // A forced move (combo finisher, grip, throw, aerial-kick push) never carries a deck guard past HoldCommand's .5 edge margin, since it
        // cannot jump back. It never pulls a guard inward either. HoldTier is only set from chapter data, so chapter 0 always gets x back.
        internal float HoldClampX(SFActor a,float x){if(!a.HoldTier)return x;int d=DeckIndex(a);if(d<0)return x;var p=Platforms[d];return Mathf.Clamp(x,Mathf.Min(a.X,p.x+.5f),Mathf.Max(a.X,p.x+p.width-.5f));}
        // Partner foe filter, chapters only (chapter 0 always gets false, so its nearest-foe pick is today's): an opponent grounded on a deck
        // that is neither the partner's nor the lead's (a guard parked across a gap at the same height) is not the partner's fight, even
        // while the partner is airborne. Otherwise it steers at that deck mid-jump or walks off the lead's girder toward it and loops.
        internal bool OffDeckFoe(SFActor a,SFActor buddy){if(!InChapter||!a.motor.IsGrounded||a.Height<.3f)return false;int d=DeckIndex(a);return d>=0&&d!=DeckIndex(buddy)&&d!=DeckIndex(Player);}

        // Chapters with verticalCatchUp, in both directions. Stranded: no fight, the lead grounded and not rising or falling, the partner
        // grounded, not rising or falling and stalled (|vx|<.4), and the two more than 1.2 apart in height. After 2.5 s of game time stranded,
        // the partner teleports onto the lead's surface: the follow spot cleared of hazards, clamped .4 inside the lead's deck (street:
        // 1..levelEnd-.5), at the lead's height+.15 and lane 0, with a teal impact. That frame's command is cleared so the partner does not hop on arrival.
        // A hop in place does not end a run: while the lead stays settled, the partner's hop frames (off the ground, or on it but rising or
        // falling) neither end it nor fire it, and it restarts only when the partner settles on another floor (more than .25 from where it
        // began). A lead swap restarts it for the new partner.
        void VerticalCatchUp(SFActor buddy,SFActor nearest,Vector2 follow,ref SFCommand c)
        {
            if(buddy!=strandedBuddy){strandedBuddy=buddy;strandedSince=-1;}
            bool leadSettled=nearest==null&&Player.Alive&&Player.motor.IsGrounded&&Mathf.Abs(Player.motor.Body.linearVelocity.y)<.1f;
            bool buddySettled=buddy.motor.IsGrounded&&Mathf.Abs(buddy.motor.Body.linearVelocity.y)<.1f;
            bool stranded=leadSettled&&buddySettled&&Mathf.Abs(buddy.motor.Body.linearVelocity.x)<.4f&&Mathf.Abs(Player.Height-buddy.Height)>1.2f;
            float gap=Elapsed-strandedSeen;strandedSeen=Elapsed;
            if(leadSettled&&!buddySettled&&strandedSince>=0&&gap>=0&&gap<=.25f)return;
            if(!stranded){strandedSince=-1;return;}
            if(strandedSince<0||gap<0||gap>.25f||Mathf.Abs(buddy.Height-strandedFloor)>.25f){strandedSince=Elapsed;strandedFloor=buddy.Height;}
            if(Elapsed-strandedSince<2.5f)return;
            var deck=Platforms.FirstOrDefault(p=>Player.X>p.x-.3f&&Player.X<p.x+p.width+.3f&&Mathf.Abs(Player.Height-p.height)<.25f);
            float x=HazardClearX(follow.x);x=deck.width>0?Mathf.Clamp(x,deck.x+.4f,deck.x+deck.width-.4f):Mathf.Clamp(x,1,Chapter.levelEnd-.5f);
            DeckTeleport(buddy,new Vector2(x,Player.Height+.15f));buddy.lane=0;buddy.CourseStepIndex=-1;
            c.move=Vector2.zero;c.jump=false;strandedSince=-1;VerticalCatchUps++;
            Impact(buddy.VisualPosition+Vector3.up,Teal,"",0);
        }

        // Chapter partner teleports (vertical catch-up, and the 13-unit leash in CompanionCommand): a teleport between closely stacked
        // one-way decks (Rio R12/R13/R14) must discard the old contact pairs, otherwise the body can settle below its new deck. Dropping
        // the simulation around the move does that (port of Codex a84c8d1) and restores the prior simulated state. Chapter 0 never calls this.
        void DeckTeleport(SFActor a,Vector2 position)
        {
            bool simulated=a.motor.Body.simulated;a.motor.Freeze(true);
            a.Teleport(position);a.motor.Freeze(!simulated);
        }

        // Camera lift eases toward the chapter lift as the lead climbs past 2.5, reaching it at 4.5. Chapter 0 never calls this.
        void TickCameraLift(float dt)
        {
            float want=Chapter.cameraLift*Mathf.Clamp01((Player.Height-2.5f)/2f);
            cameraLift=Mathf.Lerp(cameraLift,want,1-Mathf.Exp(-dt*3));
        }

        // Chapter flow: Curitiba > Rio > Al Anbar > Curitiba. The Won card's primary button, Enter and pad Start call AdvanceCourse; nothing
        // auto-advances, so Won persists for the route bot and Smoke. -sfNoChapters (ChaptersEnabled false) hides NEXT and keeps chapter 0.
        public bool CourseHasNext=>CourseMode&&ChaptersEnabled&&!DriveActive;
        // Set once a chapter load is queued, so a second press in the same frame (Enter plus a click) cannot skip a chapter.
        bool chapterLoading;
        // A chapter with driveAfter opens its road interlude first; if the drive is refused, the direct cut runs so the player is never stuck.
        public void AdvanceCourse()
        {
            if(chapterLoading||!CourseHasNext||State!=SFState.Won)return;
            var ch=Chapter;if(ch.driveAfter&&StartDrive(false,false,true)){journeyNext=5;return;}
            GoToChapter(ch.next<0?0:ch.next);
        }
        // Loads chapter n fresh (Restart reloads the scene; the static chapter survives it). The lead carries into chapters 1+ only, and audio
        // options are flushed first because nothing saves them when the scene unloads.
        public void GoToChapter(int n)
        {
            if(chapterLoading)return;chapterLoading=true;
            courseChapter=ChaptersEnabled?Mathf.Clamp(n,0,SFCourseChapters.All.Length-1):0;carriedLead=courseChapter==0?-1:Selected;
            Audio?.FlushOptions();Restart();
        }
        // SKIP DRIVE on the interlude's ready card: close the road first (meshes and sprites freed before the reload), then load the next chapter.
        public void SkipCourseDrive()
        {
            if(!CourseMode||!DriveActive)return;
            EndDrive();GoToChapter(Chapter.next);
        }

        // Local QA entry points (the WebGL shell's SendMessage, like RunCourseQA; menu only, never in a public build).
        // RunChapterQA("1"/"2"): reloads the course on that chapter and runs the route bot (SFChapterRouteCheck), which ends with
        // 'CHAPTER n QA COMPLETE: PASS|FAIL'. RunChapterQA("check1"/"check2") runs the chapter fixtures instead ('CHAPTER CHECK n COMPLETE'),
        // and "check0" the data mode on Curitiba ('CHAPTER DATA QA COMPLETE'). The chapter is static, so it survives the reload.
        public void RunChapterQA(string n)
        {
            if(!LocalQAAllowed||State!=SFState.Menu)return;
            string s=(n??"").Trim();bool check=s.StartsWith("check",System.StringComparison.Ordinal);
            if(!int.TryParse(check?s.Substring(5):s,out int c)||c<(check?0:1)||c>=SFCourseChapters.All.Length){Debug.LogWarning("RunChapterQA: no chapter '"+n+"'");return;}
            if(check)pendingChapterQA=true;else SFChapterRouteCheck.Want=c;
            selectedCourse=true;courseChapter=c;carriedLead=-1;Restart();
        }
        // RunChapterFlowQA(): the F12 flow check from Curitiba's menu ('CHAPTER FLOW QA COMPLETE: PASS|FAIL'). Ignored while a flow run is live.
        public void RunChapterFlowQA()
        {
            if(!LocalQAAllowed||State!=SFState.Menu||!SFChapterFlowCheck.Request())return;
            ChooseCourse(true);
        }

        // C9 chapter select: the course menu's CURITIBA / RIO / AL ANBAR pills (SFHUDChapters.cs). Hidden in the campaign, under
        // -sfNoChapters (the menu is then exactly 0.8.44's) and whenever the menu is not showing. Human play only: every automated run sets
        // SmokeMode, and there the menu stays 0.8.44's (no pills, no extra controller targets, SelectChapter refused) so Codex's menu
        // captures and controller fixtures are unchanged. SFChapterSelectCheck alone opts in with ChapterSelectForced; -sfNoChapters still wins.
        public bool ChapterSelectVisible=>CourseMode&&ChaptersEnabled&&State==SFState.Menu&&!DriveActive&&(ChapterSelectForced||!SmokeMode);
        internal bool ChapterSelectForced;
        // A pill loads chapter n fresh at its own menu (title, briefing and start label) through GoToChapter, the Won card's path, so the
        // lead carries into chapters 1+ only. The current chapter's pill, a rotate-blocked screen and every other state do nothing.
        public void SelectChapter(int n)
        {
            if(!ChapterSelectVisible||LayoutBlocked||n<0||n>=SFCourseChapters.All.Length||n==courseChapter)return;
            GoToChapter(n);
        }
        // Keyboard chapter select: 1 / 2 / 3 (top row or numpad) choose CURITIBA / RIO / AL ANBAR through SelectChapter, the pills' own path,
        // so the current chapter's key, the campaign, -sfNoChapters and every state but the course menu do nothing. SFGame.Update calls it
        // only in human play (outside SmokeMode). The digits are free on the menu: SFInput reads 1/2 as weapon slots only during play.
        internal void ChapterSelectKeys(Keyboard k)
        {
            if(k==null||!ChapterSelectVisible)return;
            int n=k.digit1Key.wasPressedThisFrame||k.numpad1Key.wasPressedThisFrame?0:k.digit2Key.wasPressedThisFrame||k.numpad2Key.wasPressedThisFrame?1:k.digit3Key.wasPressedThisFrame||k.numpad3Key.wasPressedThisFrame?2:-1;
            if(n>=0)SelectChapter(n);
        }
        // RunChapterSelectQA(): the chapter-select check from the campaign menu ('CHAPTER SELECT QA COMPLETE: PASS|FAIL'). Ignored while a run is live.
        public void RunChapterSelectQA()
        {
            if(!LocalQAAllowed||State!=SFState.Menu||DriveActive||!SFChapterSelectCheck.Request())return;
            ChooseCourse(false);
        }

        // JLTV interlude plates (course only, called from StartDrive). A plate counts only as one live frame, so a sliced or missing asset
        // falls back with a warning and never throws.
        //   Departure: plate_desert_convoy, else the Serra convoy panorama under a dust tint. An Al Anbar foot plate has no road band, so it is never used here.
        //   Arrival: plate_desert_arrival, else Al Anbar's plate_5_0 (seen only at the stop), else the Serra overlook under the same tint.
        static readonly Color DesertDust=new Color(1,.86f,.66f);
        static SFArt DrivePlate(string id)
        {
            var art=Resources.Load<SFArt>("SF/"+id);
            return art!=null&&art.frames!=null&&art.frames.Length==1&&art.frames[0]!=null?art:null;
        }
        void CourseDrivePlates(ref SFArt departure,ref SFArt arrival,ref Color departureTint)
        {
            var road=DrivePlate("plate_desert_convoy");
            if(road!=null)departure=road;
            else{departureTint=DesertDust;Debug.LogWarning("SF plate fallback plate_desert_convoy -> plate_serra_convoy");}
            var stop=DrivePlate("plate_desert_arrival");
            if(stop!=null){arrival=stop;return;}
            stop=DrivePlate("plate_5_0");
            if(stop!=null){arrival=stop;Debug.LogWarning("SF plate fallback plate_desert_arrival -> plate_5_0");return;}
            driveTint=DesertDust;Debug.LogWarning("SF plate fallback plate_desert_arrival -> plate_serra_overlook_hd");
        }
    }
}
