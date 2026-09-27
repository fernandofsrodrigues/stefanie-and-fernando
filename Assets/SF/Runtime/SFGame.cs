using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace StefanieAndFernando
{
    public enum SFState { Menu, Playing, Paused, Won, Lost }
    public sealed partial class SFGame : MonoBehaviour
    {
        public SFState State=SFState.Menu;
        public Transform World;
        public Camera Camera;
        public Material Unlit;
        public Sprite Pixel,Ellipse,Ring;
        public readonly Color Gold=new Color(.94f,.70f,.38f),Teal=new Color(.37f,.86f,.83f);
        public List<SFActor> Actors=new List<SFActor>();
        public SFActor Fernando,Stefanie;
        public SFActor Player => Selected==0?Fernando:Stefanie;
        public SFActor Partner => Selected==0?Stefanie:Fernando;
        public SFAudio Audio;
        public int Selected,DataCount,KOs,Assists;
        public int Ammo=>(Fernando?.TotalAmmo??0)+(Stefanie?.TotalAmmo??0);
        public float Bond=70,AssistCooldown,Elapsed,Checkpoint=3;
        static bool selectedShake=true;
        public bool ShakeEnabled {get=>selectedShake;set=>selectedShake=value;}
        public bool ShowHelp;
        public bool SwapThumbs;
        public bool WalkMode { get; private set; }
        public bool MotionStudyPreview;
        public string Speaker="",Speech="",Notice="";
        public float SpeechTime,NoticeTime;
        // The high step stays clear of the street-jump apex and its overlap ground probe.
        public SFPlatform[] Platforms={new SFPlatform(14,5,1.1f),new SFPlatform(34,4,1),new SFPlatform(53,4,1.1f),new SFPlatform(57,4,2.4f),new SFPlatform(91,5,1.5f)};
        public readonly List<SFPickup> Pickups=new List<SFPickup>();
        public float[] Hazards={38,75,103};
        SpriteRenderer[] plates;
        readonly List<SFEffect> effects=new List<SFEffect>();
        readonly List<SpriteRenderer> stageProps=new List<SpriteRenderer>();
        readonly List<SpriteRenderer> hazardStrips=new List<SpriteRenderer>();
        readonly List<SpriteRenderer> hazardWashes=new List<SpriteRenderer>();
        float cameraX=10,shake,swapCooldown,followSide=-1,followPeak;SFActor followLead;
        bool checkpointReached,bossAnnounced;
        SFHUD hud;
        public bool SmokeMode;
        static bool? selectedCourse;
        static bool pendingCourseQA,pendingCourseRulesQA;
        // Course chapter (0 Curitiba, 1 Rio, 2 Al Anbar) survives Restart like selectedCourse. -sfNoChapters is the kill switch: chapter 0 only.
        static int courseChapter,carriedLead=-1;static bool chapterArgApplied,pendingChapterQA;internal static readonly bool ChaptersEnabled=!Environment.GetCommandLineArgs().Contains("-sfNoChapters");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetChapterStatics(){courseChapter=0;carriedLead=-1;chapterArgApplied=pendingChapterQA=false;}
        // Chapter is a pure getter: SFCourseRulesCheck flips CourseMode in place, and the campaign always reads chapter 0.
        // CourseChapter is the queued static; InChapter/Chapter/SceneChapter read sceneChapter, the scene's own snapshot taken in Awake. GoToChapter
        // and ChooseCourse queue the static before the reload lands next frame, so the closing scene must not draw the next chapter's strings or pill.
        public int CourseChapter=>courseChapter;int sceneChapter;internal int SceneChapter=>sceneChapter;internal bool InChapter=>CourseMode&&sceneChapter>0;public SFCourseChapter Chapter=>SFCourseChapters.Get(CourseMode?sceneChapter:0);
        public float CheckpointHeight;public float[] HazardHeights=new float[0],HazardHalf=new float[0];bool[] beatFired=new bool[0];
        internal static bool LocalQAAllowed=>Debug.isDebugBuild||Application.platform==RuntimePlatform.WebGLPlayer&&(Application.absoluteURL.StartsWith("http://127.0.0.1:")||Application.absoluteURL.StartsWith("http://localhost:"));
        public void ChooseCourse(bool course){if(DriveActive||State==SFState.Playing)return;selectedCourse=course;courseChapter=0;carriedLead=-1;Restart();}
        public void RunCourseQA(){if(!LocalQAAllowed||State!=SFState.Menu)return;pendingCourseQA=true;ChooseCourse(true);}

        public void RunCourseRulesQA(){if(!LocalQAAllowed||State!=SFState.Menu)return;pendingCourseRulesQA=true;ChooseCourse(true);}

        void Awake()
        {
            RestoreTouchPreference();
            Application.targetFrameRate=60;CompletedMissions=PlayerPrefs.GetInt("SF.CompletedMissions",0);
            SwapThumbs=PlayerPrefs.GetInt("SF.SwapThumbs",0)!=0;
            SmokeMode=Environment.GetCommandLineArgs().Contains("-sfSmoke");
            CourseMode=selectedCourse??Environment.GetCommandLineArgs().Any(a=>a=="-sfCourse"||a=="-sfSmoke"||a=="-sfRoute"||a=="-sfControls"||a=="-sfCourseRules"||a=="-sfResourceProbe"||a=="-sfChapter"||a=="-sfChapterCheck"||a=="-sfChapterFlow")||
                (Application.platform==RuntimePlatform.WebGLPlayer&&!Application.absoluteURL.Contains("qa="));
            // Every existing QA entry point, the campaign and the kill switch force chapter 0; -sfChapter N applies once per process.
            var args=Environment.GetCommandLineArgs();if(!CourseMode||!ChaptersEnabled||SFResourceCheck.Requested||args.Any(a=>a=="-sfSmoke"||a=="-sfCourseRules"||a=="-sfControls"||a=="-sfResourceProbe"))courseChapter=0;else if(!chapterArgApplied){chapterArgApplied=true;int i=Array.IndexOf(args,"-sfChapter");if(i>=0&&i+1<args.Length&&int.TryParse(args[i+1],out int n))courseChapter=Mathf.Clamp(n,0,SFCourseChapters.All.Length-1);}
            sceneChapter=courseChapter;if(InChapter)ApplyChapter();
            Unlit=Resources.Load<Material>("SF/Unlit");
            Pixel=MakeSprite(4,4,(x,y)=>Color.white);
            Ellipse=MakeSprite(96,96,(x,y)=>new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-new Vector2(x,y).magnitude),1.4f)));
            Ring=MakeSprite(96,96,(x,y)=>new Color(1,1,1,Mathf.Clamp01(1-Mathf.Abs(new Vector2(x,y).magnitude-.84f)*20)));
            var classroomPlayer=FindAnyObjectByType<PlayerMovement>();
            Camera=Camera.main;
            if(Camera==null){Camera=new GameObject("Main Camera").AddComponent<Camera>();Camera.tag="MainCamera";}
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if(root!=gameObject&&root!=Camera.gameObject&&(classroomPlayer==null||root!=classroomPlayer.gameObject))root.SetActive(false);
            Camera.orthographic=true;Camera.orthographicSize=5.625f;Camera.transform.position=new Vector3(10,1,-10);
            Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.025f,.04f,.07f);
            if(Camera.GetComponent<AudioListener>()==null)Camera.gameObject.AddComponent<AudioListener>();
            World=new GameObject("SF gameplay").transform;
            Physics2D.IgnoreLayerCollision(7,7,true);
            BuildLevel();
            Fernando=CreateActor("fernando",true,new Vector2(13,-.05f),false,classroomPlayer?classroomPlayer.gameObject:null);
            Stefanie=CreateActor("stefanie",true,new Vector2(15,.2f));
            SpawnEnemies();
            Audio=gameObject.AddComponent<SFAudio>();hud=gameObject.AddComponent<SFHUD>();hud.game=this;
            SetState(SFState.Menu);
            if(SFResourceCheck.Requested){SmokeMode=true;gameObject.AddComponent<SFResourceCheck>();return;}
            if((pendingCourseRulesQA||Environment.GetCommandLineArgs().Contains("-sfCourseRules"))&&CourseMode){pendingCourseRulesQA=false;SmokeMode=true;gameObject.AddComponent<SFCourseRulesCheck>();return;}
            if(pendingCourseQA&&CourseMode){pendingCourseQA=false;SmokeMode=true;gameObject.AddComponent<SFRouteCheck>();return;}
            // RunChapterQA (local WebGL has no -sfRoute): the chapter route bot on the requested chapter.
            if(SFChapterRouteCheck.Want>0&&CourseMode){SmokeMode=true;gameObject.AddComponent<SFChapterRouteCheck>();return;}
            if((pendingChapterQA||args.Contains("-sfChapterCheck"))&&CourseMode){pendingChapterQA=false;SmokeMode=true;gameObject.AddComponent<SFChapterCheck>();return;}
            // The flow check spans several scene loads (its Stage is static) and ends in the campaign, so it is not gated on CourseMode.
            if(SFChapterFlowCheck.Requested){SmokeMode=true;gameObject.AddComponent<SFChapterFlowCheck>();return;}
            // C9 chapter select (-sfChapterSelect, or RunChapterSelectQA): also spans scene loads, from the campaign through each chapter's menu.
            if(SFChapterSelectCheck.Requested){SmokeMode=true;gameObject.AddComponent<SFChapterSelectCheck>();return;}
            if(SmokeMode)StartCoroutine(Smoke());
#if !UNITY_WEBGL
            else if(Environment.GetCommandLineArgs().Contains("-sfGamepad")){SmokeMode=true;gameObject.AddComponent<SFGamepadCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfWardrobe")){SmokeMode=true;gameObject.AddComponent<SFWardrobeCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfFirearmMotion")){SmokeMode=true;gameObject.AddComponent<SFFirearmMotionCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfCombat")){SmokeMode=true;gameObject.AddComponent<SFCombatCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfDrive")){SmokeMode=true;gameObject.AddComponent<SFDriveCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfCloseCombat")){SmokeMode=true;gameObject.AddComponent<SFCloseCombatCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfClinch")){SmokeMode=true;gameObject.AddComponent<SFPairedClinchCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfTakedown")){SmokeMode=true;gameObject.AddComponent<SFPairedTakedownCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfEnemyFirearms")){SmokeMode=true;gameObject.AddComponent<SFEnemyFirearmsCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfReload")){SmokeMode=true;gameObject.AddComponent<SFReloadCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfUppercut")){SmokeMode=true;gameObject.AddComponent<SFUppercutCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfRoundhouse")){SmokeMode=true;gameObject.AddComponent<SFRoundhouseCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfBoxing")){SmokeMode=true;gameObject.AddComponent<SFBoxingCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfHook")){SmokeMode=true;gameObject.AddComponent<SFHookCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfAerial")){SmokeMode=true;gameObject.AddComponent<SFAerialCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfDepth")){SmokeMode=true;gameObject.AddComponent<SFDepthCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfGait")){SmokeMode=true;gameObject.AddComponent<SFGaitCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfTouch")){SmokeMode=true;gameObject.AddComponent<SFTouchCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfUsability")){SmokeMode=true;gameObject.AddComponent<SFUsabilityCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfRifle")){SmokeMode=true;gameObject.AddComponent<SFRifleCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfRoute")){SmokeMode=true;if(InChapter)gameObject.AddComponent<SFChapterRouteCheck>();else gameObject.AddComponent<SFRouteCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfControls")){SmokeMode=true;gameObject.AddComponent<SFControlsCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfSandbox")){SmokeMode=true;gameObject.AddComponent<SFSandboxCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfPolish")){SmokeMode=true;gameObject.AddComponent<SFPolishCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfMotion")){SmokeMode=true;gameObject.AddComponent<SFMotionCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfParty")){SmokeMode=true;gameObject.AddComponent<SFPartyCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfCampaign")){SmokeMode=true;gameObject.AddComponent<SFCampaignCheck>();}
            else if(Environment.GetCommandLineArgs().Contains("-sfFeel")){SmokeMode=true;gameObject.AddComponent<SFFeelCheck>();}
#endif
        }

        Sprite MakeSprite(int w,int h,Func<float,float,Color> color)
        {
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);tex.wrapMode=TextureWrapMode.Clamp;
            var colors=new Color[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)colors[y*w+x]=color(x/(float)(w-1)*2-1,y/(float)(h-1)*2-1);
            tex.SetPixels(colors);tex.Apply();return Sprite.Create(tex,new Rect(0,0,w,h),new Vector2(.5f,.5f),w);
        }

        public SpriteRenderer CreateSprite(string name,Sprite sprite,Color color,int order)
        {
            var sr=new GameObject(name).AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.color=color;sr.sortingOrder=order;sr.sharedMaterial=Unlit;
            return sr;
        }

        SpriteRenderer Box(string name,float x,float y,float w,float h,Color color,int order)
        {
            var sr=CreateSprite(name,Pixel,color,order);sr.transform.SetParent(World,false);sr.transform.position=new Vector3(x,y,0);sr.transform.localScale=new Vector3(w,h,1);stageProps.Add(sr);return sr;
        }

        void BuildLevel()
        {
            if(!CourseMode){BuildSandboxLevel();return;}
            if(InChapter){BuildChapterLevel(Chapter);return;}
            // Plates remain intact. Slight overlap is a temporary scene join, not a claim of seamless source art.
            plates=new SpriteRenderer[6];
            for(int i=0;i<6;i++)
            {
                var sr=CreateSprite("Curitiba plate "+i,Resources.Load<SFArt>("SF/curitiba_"+i).Frame(0),Color.white,-100+i);
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
                Box("Platform depth",p.x+p.width/2,-3+p.height-.12f,p.width,.3f,new Color(.11f,.16f,.18f),60);
                Box("Platform lip",p.x+p.width/2,-3+p.height+.02f,p.width,.055f,new Color(.51f,.63f,.62f),61);
                for(float x=p.x+.25f;x<p.x+p.width;x+=.75f)Box("Platform rivet",x,-3+p.height-.08f,.045f,.045f,Gold*.8f,62);
                for(int s=0;s<2;s++)Box("Platform support",p.x+(s==0?.18f:p.width-.18f),-3+p.height/2,.12f,p.height,new Color(.08f,.12f,.14f),59);
            }
            AddPickup(17,1.85f,0,"data");AddPickup(59,3.15f,0,"data");AddPickup(94,2.25f,0,"data");
            AddPickup(31,.8f,0,"medical");AddPickup(65,.8f,0,"ammo");AddPickup(108,.8f,0,"medical");
            AddPickup(11,.8f,0,"food");AddPickup(42,.8f,0,"drink");AddPickup(80,.8f,0,"food");
            AddProp("props_solid",0,8,1.1f,2.1f);AddProp("props_occluder",0,8,1.8f,1.5f);
            AddProp("props_solid",5,22,.85f,2.3f);AddProp("props_occluder",1,32,1.1f,1.9f);
            AddProp("props_solid",3,50,1.1f,2.2f);AddProp("props_solid",4,72,1.1f,1.8f);
            AddProp("props_solid",2,100,1.1f,2.3f);
            foreach(float x in Hazards)
            {
                Interaction("Electrical hazard",x,.3f,1.8f,.6f).hazard=true;
                Box("Wet electrical fault",x,-3.02f,1.8f,1.2f,new Color(.055f,.09f,.10f,.7f),47);
                hazardWashes.Add(Box("Electrical current span",x,-3.02f,1.5f,1.2f,new Color(Teal.r,Teal.g,Teal.b,0),48));
                var glow=CreateSprite("Electrical current glow",Ellipse,new Color(Teal.r,Teal.g,Teal.b,.08f),48);
                glow.transform.SetParent(World);glow.transform.position=new Vector3(x,-3.02f,0);glow.transform.localScale=new Vector3(1.8f,1.2f,1);
                stageProps.Add(glow);hazardStrips.Add(glow); // Both layers remain below contact shadows (50).
                for(int i=0;i<6;i++){var b=Box("Hazard marker",x-.75f+i*.3f,-3.04f,.12f,.08f,Gold,56);b.transform.eulerAngles=new Vector3(0,0,-25);}
            }
            var extraction=CreateSprite("Extraction beacon",Ring,Teal,60);extraction.transform.SetParent(World);extraction.transform.position=new Vector3(130,-2.7f,0);extraction.transform.localScale=new Vector3(2.2f,.45f,1);
            stageProps.Add(extraction);
            Interaction("Extraction goal",130,1.5f,5,3).goal=true;
        }

        void AddProp(string sheet,int frame,float x,float lane,float width)
        {
            var art=Resources.Load<SFArt>("SF/"+sheet);if(art==null)return;
            var sr=CreateSprite("Street prop",art.Frame(frame),new Color(.68f,.78f,.84f),55);
            sr.transform.SetParent(World);sr.transform.position=new Vector3(x,-3+lane,0);
            sr.transform.localScale=Vector3.one*(width/sr.sprite.bounds.size.x);stageProps.Add(sr);
        }

        SFInteraction Interaction(string name,float x,float y,float width,float height)
        {
            var go=new GameObject(name);go.transform.SetParent(StageRoot);go.transform.position=new Vector3(x,y,0);
            var collider=go.AddComponent<BoxCollider2D>();collider.isTrigger=true;collider.size=new Vector2(width,height);
            var interaction=go.AddComponent<SFInteraction>();interaction.game=this;return interaction;
        }

        void AddPickup(float x,float height,float lane,string kind)
        {
            var sr=CreateSprite(kind,kind=="data"?Pixel:Ring,kind=="medical"?new Color(.65f,.92f,.66f):Teal,65);
            sr.transform.SetParent(StageRoot);sr.transform.localScale=Vector3.one*(kind=="data"?.26f:.45f);sr.transform.rotation=Quaternion.Euler(0,0,45);
            string sheet=kind=="medical"?"pickups_medical":kind=="food"?"pickups_food":kind=="drink"?"pickups_drinks":kind=="ammo"&&!CourseMode?"weapons_hero":null;
            if(sheet!=null){var art=Resources.Load<SFArt>("SF/"+sheet);sr.sprite=art.Frame(kind=="food"?4:kind=="ammo"?2:0);sr.color=Color.white;sr.transform.rotation=Quaternion.identity;sr.transform.localScale=Vector3.one*(.65f/Mathf.Max(sr.sprite.bounds.size.x,sr.sprite.bounds.size.y));}
            var pickup=new SFPickup{x=x,height=height,lane=lane,kind=kind,visual=sr};Pickups.Add(pickup);
            Interaction("Collectible "+kind,x,height,.8f,.8f).pickup=pickup;
        }

        SFActor CreateActor(string id,bool hero,Vector2 position,bool boss=false,GameObject existing=null,float height=0)
        {
            GameObject go=existing!=null?existing:new GameObject(id);go.transform.SetParent(World,true);
            var actor=go.AddComponent<SFActor>();actor.Configure(this,id,hero,position,boss,height);Actors.Add(actor);return actor;
        }

        void SpawnEnemies()
        {
            foreach(var old in Actors.Where(a=>!a.Friendly).ToArray()){Actors.Remove(old);Destroy(old.shadow.gameObject);Destroy(old.gameObject);}
            if(InChapter){SpawnChapterEnemies(Chapter);return;}
            string[] cast=CourseMode?new[]{"latch","latch","keel","latch","latch","keel","latch","vesper"}:
                RouteIndex==2?new[]{"silk","latch","ratchet","silk","latch","keel","silk","cantilever"}:
                RouteIndex>=3?new[]{"latch","silk","ratchet","latch","silk","ratchet","latch","foreman"}:
                new[]{"latch","silk","keel","latch","silk","keel","latch","vesper"};
            float[] x={24,28,45,48,69,85,88,119};float[] lane={.1f,-.25f,.2f,-.1f,-.2f,.3f,-.25f,0};
            for(int i=0;i<cast.Length;i++)CreateActor(cast[i],false,new Vector2(x[i],lane[i]),i==7);
            if(!CourseMode)
            {
                string[] extras={"latch","silk","keel","latch","silk","ratchet","latch","silk","keel","ratchet","latch","silk"};
                float[] positions={21,30,42,52,64,72,82,91,100,108,115,122};
                for(int i=0;i<extras.Length;i++)CreateActor(extras[i],false,new Vector2(positions[i],i%2==0?-.8f:.8f));
            }
        }

        public void StartGame()
        {
            if(State!=SFState.Menu||LayoutBlocked||DriveActive)return;
            if(!CourseMode)ApplyLoadouts();
            SetState(SFState.Playing);
            if(InChapter){StartChapter();return;}
            Player.Teleport(new Vector2(3,.2f));Partner.Teleport(new Vector2(1.5f,.2f));
            cameraX=10;Say("STEFANIE",CourseMode?"Three pieces of intel. One way out. Together.":"Just a walk. Stay close.");Audio.StartAmbience();
        }

        public void SetState(SFState state,bool playResultCue=true)
        {
            if(state==SFState.Playing&&LayoutBlocked)state=SFState.Paused;
            if(CourseMode&&state!=SFState.Paused)ShowHelp=false; // Course help is a pause-card page; it never covers live play.
            var previous=State;State=state;Touch.Reset();Pad.ResetHeld();hud?.PadMenu.Cancel();
            if(previous!=state)hud?.DisarmCourseQuit(); // Confirmation cannot survive leaving a card, even before the next render.
            foreach(var actor in Actors)actor.motor.Freeze(state!=SFState.Playing||DriveActive);
            foreach(var prop in stageProps)prop.enabled=state!=SFState.Menu;
            foreach(var item in Pickups)item.visual.enabled=state!=SFState.Menu&&!item.taken;
            if(state==SFState.Won)Notice=InChapter?Chapter.wonNotice:"Both home. Mission complete.";
            Audio?.StateChanged(previous,state,playResultCue);
        }

        internal bool CanQuitCourse=>CourseMode&&!Application.isEditor&&Application.platform!=RuntimePlatform.WebGLPlayer&&State!=SFState.Playing;
        public void QuitCourse(){if(!CanQuitCourse)return;Audio?.FlushOptions();Application.Quit();}

        public void RetryCheckpoint()
        {
            Fernando.Revive();Stefanie.Revive();Fernando.health=Fernando.maxHealth;Stefanie.health=Stefanie.maxHealth;
            SetState(SFState.Playing);
            // CheckpointHeight stays 0 in chapter 0, so this is today's street retry there. Teleport also clears each hero's CourseStepIndex.
            Fernando.Teleport(new Vector2(Checkpoint,CheckpointHeight+.2f));Stefanie.Teleport(new Vector2(Checkpoint-1.5f,CheckpointHeight+.2f));
            // Chapters also restart the partner's vertical catch-up timer.
            if(InChapter)strandedSince=-1;
            foreach(var a in new[]{Fernando,Stefanie}){if(a.TotalAmmo==0)a.SidearmAmmo.Supply(17);}Bond=70;if(CourseMode)NoticeTime=0;Say("FERNANDO","We regroup. Then we go together.");
        }

        public void Restart()=>SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        internal SFCommand ReadCommand()
        {
            if(DriveActive)return ReadDriveCommand();
            if(GamepadActive){var pad=Pad.Read(Player.Armed,Player.Rifle,Player.GripTarget!=null,Time.deltaTime);if(CourseMode&&pad.backup){pad.backup=false;pad.interact=true;}return pad;} // Course has no backup: LB (guard) + D-pad Up still revives, collects and extracts.
            if(TouchControls){if(ShowHelp){Touch.Reset();return default;}return Touch.Read(Player,Screen.width*900f/Screen.height,DriveActive);}
            var command=SFInput.Read(Player.Armed&&Player.GripTarget==null,SwapThumbs);
            if(command.aim&&Mouse.current!=null){var p=Camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());command.aimFacing=p.x>Player.X?1:-1;}
            // UI buttons own the top/bottom bars. Clicking Pause must not also fire.
            if(Mouse.current!=null&&Mouse.current.position.y.ReadValue()<Screen.height*.10f){command.punch=command.shoot=command.kick=command.grapple=command.ground=false;}
            return command;
        }

        // Losing the foreground must never leave an unattended mission or held touch running.
        public void PauseForInterruption()
        {
            Touch.Reset();Pad.ResetHeld();
            if(State==SFState.Playing)SetState(SFState.Paused);
        }
        bool AutomatedRun=>Environment.GetCommandLineArgs().Any(a=>a.StartsWith("-sf",StringComparison.Ordinal));
        void OnApplicationFocus(bool focused){if(!focused&&!AutomatedRun)PauseForInterruption();}
        void OnApplicationPause(bool paused){if(paused&&!AutomatedRun)PauseForInterruption();}

        internal void ApplyGait(ref SFCommand command)
        {
            if(command.toggleWalk){WalkMode=!WalkMode;Notify(WalkMode?(GamepadActive?"WALK ON  /  HOLD LS TO RUN":"WALK ON  /  HOLD SHIFT TO RUN"):(GamepadActive?"WALK OFF  /  HOLD LS TO RUN":"WALK OFF  /  HOLD SHIFT TO RUN"));}
            command.walk=(command.walk||WalkMode)&&!command.run;
        }

        void Update()
        {
            TickFeel();
            if(!SmokeMode)RefreshControlTakeover();
            RefreshTouchLayout(Screen.width,Screen.height);
            if(!SmokeMode)ProcessGamepad();
            var k=Keyboard.current;
            if(hud.PadHelpOpen)
            {
                if(!SmokeMode&&k!=null&&k.escapeKey.wasPressedThisFrame){hud.PadHelpOpen=false;Pad.ResetHeld();}
                return;
            }
            if(hud.AudioCreditsOpen)
            {
                if(!SmokeMode&&k!=null&&k.escapeKey.wasPressedThisFrame)hud.CloseAudioCredits();
                return;
            }
            if(!SmokeMode&&k!=null)
            {
                ProcessPauseAndRetryKeys();
                if(State==SFState.Menu&&k.enterKey.wasPressedThisFrame)StartGame();
                // Course menu: 1 / 2 / 3 choose a chapter like its pill (SFChapterLevel.cs). After Enter, so Enter plus a digit starts play.
                ChapterSelectKeys(k);
                // Course: H during play pauses onto the help page; while paused it toggles help. Menu and result cards ignore it.
                if(k.hKey.wasPressedThisFrame&&!DriveActive){if(!CourseMode)ShowHelp=!ShowHelp;else if(State==SFState.Playing){SetState(SFState.Paused);ShowHelp=true;}else if(State==SFState.Paused)ShowHelp=!ShowHelp;}
                if(k.f6Key.wasPressedThisFrame&&!DriveActive&&!CourseMode)ToggleMotionPreview();
                if(k.f7Key.wasPressedThisFrame)EnableTouch(TouchControls?"0":"1");
                if(k.f11Key.wasPressedThisFrame)Screen.fullScreen=!Screen.fullScreen;
                if(k.homeKey.wasPressedThisFrame)Zoom=5.625f;
            }
            float dt=Mathf.Min(Time.deltaTime,.05f);
            TickAtmosphere(dt);
            if(DriveActive){TickDrive(dt,State==SFState.Playing?(SmokeMode?SmokeCommand:ReadCommand()):default);return;}
            if(State!=SFState.Playing){if(State==SFState.Menu){Fernando.Render(dt);Stefanie.Render(dt);}return;}
            Elapsed+=dt;SpeechTime=Mathf.Max(0,SpeechTime-dt);NoticeTime=Mathf.Max(0,NoticeTime-dt);
            AssistCooldown=Mathf.Max(0,AssistCooldown-dt);swapCooldown=Mathf.Max(0,swapCooldown-dt);Bond=Mathf.Min(100,Bond+dt*2.5f);
            var command=SmokeMode?SmokeCommand:ReadCommand();
            ApplyGait(ref command);
            if(command.assist)TryAssist();if(command.interact)TryInteract();if(command.swap)Swap();
            if(command.backup)CallBackup();TickBackup(dt);
            Player.Tick(dt,command);Partner.Tick(dt,CompanionCommand());
            foreach(var actor in Actors.Where(a=>!a.hero))actor.Tick(dt,actor.ally?AllyCommand(actor):EnemyCommand(actor));
            TickPickups(dt);TickHazards();TickEffects(dt);TickTracers(dt);TickParty(dt,command);
            // Chapters run their own checkpoints and beats. Chapter 0 keeps its two literal triggers below, untouched.
            if(InChapter)TickChapterTriggers();else
            {
                if(CourseMode&&!checkpointReached&&Player.X>61){checkpointReached=true;Checkpoint=62;Notify("CHECKPOINT  /  CONSERVATORY APPROACH");}
                if(CourseMode&&!bossAnnounced&&Player.X>109){bossAnnounced=true;if(Actors.Any(a=>a.boss&&a.Alive))Say("VESPER","This road ends here.");}
            }
            if(!Player.Alive&&!Partner.Alive)SetState(SFState.Lost);
            float halfWidth=Camera.orthographicSize*Camera.aspect;
            float desired=Mathf.Clamp(Player.X+Player.facing*Mathf.Min(3.2f,halfWidth*.32f),halfWidth-.5f,(CourseMode?133.5f:133)-halfWidth);cameraX=Mathf.Lerp(cameraX,desired,1-Mathf.Exp(-dt*5)); // Course view ends 1.5 past the x132 bound, not 6.
            shake=Mathf.MoveTowards(shake,0,dt*.7f);
            var jitter=ShakeEnabled?UnityEngine.Random.insideUnitCircle*shake:Vector2.zero;
            // Chapter camera lift only. In chapter 0 cameraLift is never read, so the course camera y stays exactly 1.
            if(InChapter)TickCameraLift(dt);
            Camera.transform.position=new Vector3(cameraX+jitter.x,(CourseMode?1+(InChapter?cameraLift:0):.35f)+jitter.y,-10);
        }

        internal void ProcessPauseAndRetryKeys()
        {
            var k=Keyboard.current;if(k==null)return;
            // P avoids the browser reserving Escape to leave fullscreen.
            if(k.escapeKey.wasPressedThisFrame||k.pKey.wasPressedThisFrame)
            {if(CourseMode&&ShowHelp&&State==SFState.Paused)ShowHelp=false;else if(State==SFState.Playing)SetState(SFState.Paused);else if(State==SFState.Paused)SetState(SFState.Playing);}
            if(CourseMode&&State==SFState.Lost&&(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame))RetryCheckpoint();
            // Course Won card: Enter takes the primary NEXT button (hidden, and ignored here, under -sfNoChapters).
            else if(CourseMode&&State==SFState.Won&&CourseHasNext&&(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame))AdvanceCourse();
        }

        internal SFCommand CompanionCommand()
        {
            if(!CourseMode)return TacticalPartnerCommand();
            var buddy=Partner;if(!buddy.Alive)return default;
            var nearest=Actors.Where(a=>!a.Friendly&&a.Alive&&a.HeldBy==null&&Mathf.Abs(a.X-Player.X)<7&&!(a.motor.IsGrounded&&buddy.motor.IsGrounded&&Mathf.Abs(a.Height-buddy.Height)>=1.3f)&&!OffDeckFoe(a,buddy)).OrderBy(a=>Mathf.Abs(a.X-buddy.X)).FirstOrDefault(); // Skip an opponent settled a ledge away: no strike reaches it, so keep formation instead of punching air.
            // Mid-climb (waypoint set, not yet settled) the partner finishes the step before engaging; rising through x53 otherwise makes a deck enemy the target and drops the waypoint.
            if(nearest!=null&&buddy.CourseStepIndex>=0&&!(buddy.motor.IsGrounded&&buddy.motor.Body.linearVelocity.y<=.1f))nearest=null;
            if(Player!=followLead||Mathf.Abs(Player.X-followPeak)>2){followLead=Player;followSide=buddy.X<Player.X?-1:1;followPeak=Player.X;}else if((Player.X-followPeak)*followSide<0)followPeak=Player.X;else if(Mathf.Abs(Player.X-followPeak)>1){followSide=-followSide;followPeak=Player.X;}float followX=Player.X+followSide*1.6f;if(followX<1||followX>131.5f)followX=Player.X-followSide*1.6f;float depth=buddy.lane>=Player.lane?.3f:-.3f;if(Mathf.Abs(Player.lane+depth)>.55f)depth=-depth;var follow=new Vector2(followX,buddy.motor.IsGrounded&&buddy.Height>.5f?Player.lane:Player.lane+depth); // Trail the side the lead last travelled from (1 unit hysteresis; re-seeded from the partner's real side on a lead change or teleport) and keep the partner's own depth side. Standing on a platform it shares the lead's depth.
            float goal=follow.x;float targetLane=follow.y;
            // No fight and the lead stands on a platform: keep the spot on that platform; when cramped there, take the lead's roomier other side.
            if(nearest==null&&Player.motor.IsGrounded&&Player.motor.Body.linearVelocity.y<=.1f&&Player.Height>.5f)foreach(var p in Platforms)if(Player.X>p.x-.3f&&Player.X<p.x+p.width+.3f&&Mathf.Abs(Player.Height-p.height)<.25f){goal=HazardClearX(Mathf.Clamp(goal,p.x+.4f,p.x+p.width-.4f));float alt=HazardClearX(Mathf.Clamp(Player.X-followSide*1.6f,p.x+.4f,p.x+p.width-.4f));if(Mathf.Abs(goal-Player.X)<1.2f&&Mathf.Abs(alt-Player.X)>Mathf.Abs(goal-Player.X)){goal=alt;followSide=-followSide;followPeak=Player.X;}break;}
            SFCommand c=new SFCommand{run=true};
            if(nearest!=null&&Player.Alive){float side=Mathf.Sign(nearest.X-buddy.X);goal=nearest.X-side*1.25f;targetLane=nearest.lane;bool flank=Mathf.Abs(Player.X-goal)<1.1f;if(flank){goal=nearest.X+side*1.25f;targetLane=follow.y;for(int i=0;i<Hazards.Length;i++)if(HzLevel(buddy.Height,i)&&Mathf.Abs(goal-Hazards[i])<1.8f)goal=Player.X-side*1.6f;}c.punch=!flank&&Mathf.Abs(nearest.X-buddy.X)<1.7f&&Mathf.Abs(nearest.lane-buddy.lane)<.6f;bool kickUp=InChapter&&nearest.action==SFAction.Kick;float reach=(nearest.boss||kickUp?2.4f:1.7f)+.3f;c.guard=(nearest.action==SFAction.Punch||kickUp)&&nearest.actionClock<nearest.attackAt&&Mathf.Abs(nearest.X-buddy.X)<reach&&Mathf.Abs(nearest.lane-buddy.lane)<.7f&&(buddy.X-nearest.X)*nearest.facing>0;} // The lead already holds this side: circle at formation depth to the opponent's far side, or wait behind the lead when that side is a hazard strip. Guard only against a wind-up that can reach the partner (chapters: a kick wind-up too, reach 2.4).
            // An opponent settled on a platform: keep the partner's fight/flank spot on that platform instead of just past its edge.
            if(nearest!=null&&nearest.motor.IsGrounded)foreach(var p in Platforms)if(nearest.X>p.x-.3f&&nearest.X<p.x+p.width+.3f&&Mathf.Abs(nearest.Height-p.height)<.25f){goal=Mathf.Clamp(goal,p.x+.4f,p.x+p.width-.4f);break;}
            float cleared=HazardClearX(goal);if(nearest==null&&cleared!=goal&&(cleared-Player.X)*followSide<0){followSide=-followSide;followPeak=Player.X;}goal=cleared; // A strip that moves the follow spot to the lead's other side makes that the trailing side.
            c.move=new Vector2(Mathf.Abs(goal-buddy.X)>.4f?Mathf.Sign(goal-buddy.X):0,Mathf.Abs(targetLane-buddy.lane)>.12f?Mathf.Sign(targetLane-buddy.lane):0);
            // Live current ahead, or too little off time left to clear the strip at the partner's current speed (run 5.2, accel 26): wait at its edge. Never stop inside a strip: there it neither attacks nor guards, it walks out.
            StripWait(buddy,ref c);
            for(int i=0;i<Hazards.Length;i++)if(HzDeck(buddy.Height,i,.5f)&&Mathf.Abs(buddy.X-Hazards[i])<1.2f)c.punch=c.guard=false;
            if(nearest!=null&&Mathf.Abs(nearest.X-buddy.X)<2)buddy.facing=nearest.X>buddy.X?1:-1;
            bool leadUp=Player.Alive&&Player.motor.IsGrounded&&Player.motor.Body.linearVelocity.y<=.1f&&Player.Height>buddy.Height+.5f&&Mathf.Abs(Player.X-buddy.X)<6; // Not rising: jumping up through a one-way platform also reads as grounded.
            c.jump=leadUp&&buddy.motor.IsGrounded&&buddy.motor.Body.linearVelocity.y<=.1f&&Platforms.Any(p=>p.height<=Player.Height+.2f&&buddy.X>p.x-1&&buddy.X<p.x+p.width&&buddy.Height<p.height-.1f&&p.height-buddy.Height<1.8f);
            if(nearest==null)CourseStepCommand(buddy,Player,ref c);else buddy.CourseStepIndex=-1;
            // Chapters only: a step waypoint replaces the move, so the strip wait runs again on it (a street strip between the partner and its step).
            if(InChapter&&buddy.CourseStepIndex>=0)StripWait(buddy,ref c);
            // The 13-unit leash. In chapters it also drops the partner's old one-way deck contacts (DeckTeleport); chapter 0 keeps the plain teleport.
            if(CourseMode&&Mathf.Abs(Player.X-buddy.X)>13){var leash=new Vector2(HazardClearX(follow.x),Player.Height+.15f);if(InChapter)DeckTeleport(buddy,leash);else buddy.Teleport(leash);buddy.lane=follow.y;}
            // Chapters only: a partner stranded more than a tier above or below the lead is brought to the lead's surface after 2.5 s.
            if(InChapter&&Chapter.verticalCatchUp)VerticalCatchUp(buddy,nearest,follow,ref c);
            // Chapters only: no takeoff while a strip on the tier just above lies within a jump's travel (3.6 ahead, .3 margin), so the partner
            // never lands or slides into a deck strip it cannot see from below (Rio R2 under R3, R5 over R4). A running jump flies 3.0 and any jump
            // lands at run speed and slides about .36, and a stop within 1.2 of a strip's centre walks on into it. It walks on or stops, then jumps.
            if(InChapter&&c.jump){float lo=Mathf.Min(buddy.X,buddy.X+c.move.x*3.6f),hi=Mathf.Max(buddy.X,buddy.X+c.move.x*3.6f);for(int i=0;i<Hazards.Length;i++)if(HzH(i)>buddy.Height+.1f&&HzH(i)-buddy.Height<1.8f&&Hazards[i]+HzW(i)+.3f>lo&&Hazards[i]-HzW(i)-.3f<hi)c.jump=false;}
            if(!CourseMode){var navigation=Navigate(buddy,new Vector2(goal,targetLane));c.move=navigation.move;}
            return c;
        }
        // The course partner's strip wait (CompanionCommand): stops c.move.x at the edge of a live strip ahead on its level, or of one it cannot clear in the off time left.
        void StripWait(SFActor buddy,ref SFCommand c)
        {
            if(buddy.motor.IsGrounded)for(int i=0;i<Hazards.Length;i++){float h=Hazards[i],gap=Mathf.Abs(buddy.X-h),v0=Mathf.Clamp(buddy.motor.Body.linearVelocity.x*Mathf.Sign(h-buddy.X),0,5.2f);if(!HzLevel(buddy.Height,i)||gap<1.2f||gap>=1.8f+v0*v0/76f||(h-buddy.X)*c.move.x<=0)continue;if(HazardActive||Elapsed%3.5f+(gap+.75f)/5.2f+(5.2f-v0)*(5.2f-v0)/270.4f>1.35f)c.move.x=0;}
        }
        // Course partner spots stay 1.8 from strip centres (.75 damage half-width plus a conservative waiting margin), on the lead's side.
        // With the lead on the street, a spot that would stack on it moves past the lead, away from the strip. Platforms keep their own crowding rule.
        // Chapters: a strip counts when it is on the lead's or the partner's level. The move-past-the-lead rule stays street-only, because on a deck
        // that spot can lie beyond the deck edge. Chapter 0 (street strips, HzLevel always true) evaluates exactly as before.
        float HazardClearX(float x){for(int i=0;i<Hazards.Length;i++){float h=Hazards[i];if((HzLevel(Player.Height,i)||HzLevel(Partner.Height,i))&&Mathf.Abs(x-h)<1.8f){float s=Player.X>h?1:-1;x=h+s*1.8f;if(HzH(i)<=0&&HzDeck(Player.Height,i,.5f)&&Mathf.Abs(x-Player.X)<1.2f)x=Player.X+s*1.6f;}}return x;}

        // A street jump cannot reach the upper 2.4-unit deck. Commit to the lower
        // neighboring step until landing; changing goals at the jump apex misses it.
        void CourseStepCommand(SFActor actor,SFActor target,ref SFCommand command)
        {
            var destination=CourseMode&&target.Alive&&target.motor.IsGrounded&&Mathf.Abs(target.motor.Body.linearVelocity.y)<.1f
                ?Platforms.FirstOrDefault(p=>target.X>p.x-.3f&&target.X<p.x+p.width+.3f&&Mathf.Abs(target.Height-p.height)<.15f):default(SFPlatform);
            if(destination.width<=0){actor.CourseStepIndex=-1;return;}
            if(actor.CourseStepIndex>=0)
            {
                var pending=Platforms[actor.CourseStepIndex];
                if(destination.height<=pending.height+.2f||Mathf.Abs(target.X-(pending.x+pending.width*.5f))>8||
                    actor.motor.IsGrounded&&Mathf.Abs(actor.motor.Body.linearVelocity.y)<.1f&&Mathf.Abs(actor.Height-pending.height)<.12f)
                    actor.CourseStepIndex=-1;
            }
            if(actor.CourseStepIndex<0&&actor.motor.IsGrounded&&actor.motor.Body.linearVelocity.y<=.1f&&destination.height-actor.Height>1.8f)
            {
                float best=float.MaxValue;
                for(int i=0;i<Platforms.Length;i++)
                {
                    var step=Platforms[i];
                    if(step.height<=actor.Height+.25f||step.height>actor.Height+1.8f||step.height>=destination.height-.2f||destination.height-step.height>1.8f)continue;
                    float x=Mathf.Clamp(target.X,step.x+.65f,step.x+step.width-.65f);
                    float gap=Mathf.Abs(x-target.X),cost=Mathf.Abs(actor.X-x)+gap;
                    bool adjacent=step.x<=destination.x+destination.width+.1f&&step.x+step.width>=destination.x-.1f;
                    if(adjacent&&Mathf.Abs(actor.X-x)<8&&cost<best){best=cost;actor.CourseStepIndex=i;}
                }
            }
            if(actor.CourseStepIndex<0)return;
            var waypoint=Platforms[actor.CourseStepIndex];
            float goal=Mathf.Clamp(target.X,waypoint.x+.65f,waypoint.x+waypoint.width-.65f);
            // Chapters, heroes only (the partner): a step spot inside a strip on the step deck moves 1.8 clear of it on the partner's side.
            if(InChapter&&actor.hero){for(int i=0;i<Hazards.Length;i++)if(HzH(i)>0&&Mathf.Abs(HzH(i)-waypoint.height)<.5f&&Mathf.Abs(goal-Hazards[i])<HzW(i)+1.05f)goal=Hazards[i]+(actor.X>Hazards[i]?1:-1)*(HzW(i)+1.05f);goal=Mathf.Clamp(goal,waypoint.x+.65f,waypoint.x+waypoint.width-.65f);}
            command.move.x=Mathf.Abs(goal-actor.X)>.15f?Mathf.Sign(goal-actor.X):0;
            command.punch=command.guard=false;
            command.jump=actor.motor.IsGrounded&&actor.motor.Body.linearVelocity.y<=.1f&&actor.Height<waypoint.height-.1f&&actor.X>waypoint.x+.25f&&actor.X<waypoint.x+waypoint.width-.25f;
        }

        public SFCommand EnemyCommand(SFActor actor)
        {
            SFCommand c=default;if(!actor.Alive||actor.Restrained||actor.GripTarget!=null||Mathf.Abs(actor.X-Player.X)>(CourseMode&&actor.health<actor.maxHealth?40:11))return c;
            var target=Actors.Where(a=>a.Friendly&&a.Alive).OrderBy(a=>Vector2.Distance(a.GroundPosition,actor.GroundPosition)).FirstOrDefault();if(target==null)return c;
            float dx=target.X-actor.X,distance=Mathf.Abs(dx);if(!actor.Busy)actor.facing=dx>0?1:-1;
            bool kicker=actor.LayoutId=="silk"||actor.LayoutId=="cantilever";
            float range=actor.ranged?6:kicker?2:actor.boss?2:1.3f;
            if(!CourseMode)c=actor.ranged?RangedCoverCommand(actor,target):Navigate(actor,new Vector2(target.X-Mathf.Sign(dx)*range*.85f,target.lane));
            else c.move=new Vector2(distance>range?Mathf.Sign(dx):0,Mathf.Abs(target.lane-actor.lane)>.15f?Mathf.Sign(target.lane-actor.lane):0);
            // Course: a hero settled on a ledge 1.2+ above is out of strike reach, so walk under that ledge and climb it (jump rule below); still rising keeps an active climb alive for the second jump.
            float rise=target.Height-actor.Height;var perch=CourseMode&&rise>.1f&&(rise>=1.2f||actor.motor.Body.linearVelocity.y>.1f)&&Mathf.Abs(target.motor.Body.linearVelocity.y)<.5f?Platforms.FirstOrDefault(p=>target.X>p.x-.3f&&target.X<p.x+p.width+.3f&&Mathf.Abs(target.Height-p.height)<.25f):default(SFPlatform);bool climb=perch.width>0;
            if(climb){float gx=Mathf.Clamp(target.X-Mathf.Sign(dx)*range*.85f,perch.x+.4f,perch.x+perch.width-.4f)-actor.X;c.move.x=Mathf.Abs(gx)>.2f?Mathf.Sign(gx):0;}
            // Course: standing on a ledge 1.2+ above a settled target, step off on the target's side instead of freezing out of reach.
            // Chapters: decks that run to the x132 bound have an unreachable right step-off (past the clamp), so those use the left edge.
            // Chapter deck guards (HoldTier, only ever set from chapter data) never step off, so they skip this and keep walking toward the
            // target's x on their own deck (HoldCommand stops them .5 short of its edges) instead of marching to an edge they cannot use.
            else if(CourseMode&&!actor.HoldTier&&rise<=-1.2f&&actor.motor.IsGrounded&&Mathf.Abs(target.motor.Body.linearVelocity.y)<.5f)foreach(var p in Platforms)if(actor.X>p.x-.3f&&actor.X<p.x+p.width+.3f&&Mathf.Abs(actor.Height-p.height)<.25f){float l=p.x-.35f,r=p.x+p.width+.35f,edge=target.X<p.x?l:target.X>p.x+p.width?r:(actor.X-p.x<p.x+p.width-actor.X?l:r);if(InChapter&&edge>131.6f)edge=l;c.move.x=Mathf.Sign(edge-actor.X);break;} // Edge chosen from the target's side of the ledge (nearest edge when it stands beneath), so it stays stable as the enemy walks.
            if(actor.HasEnemyFirearm)
            {
                if(actor.Reloading)return default;
                // A sheltered shooter can top up after three shots. Every round still comes from finite reserve.
                bool shelterReload=c.crouch&&actor.EnemyAmmo.Capacity-actor.EnemyAmmo.Loaded>=3;
                if((actor.EnemyAmmo.Loaded==0||shelterReload)&&actor.TryEnemyReload())return default;
                if(actor.EnemyAmmo.Loaded==0)
                {
                    // Keep the visible empty weapon; do not fabricate ammo or substitute unarmed attack art.
                    c.move=distance<6?new Vector2(-Mathf.Sign(dx),0):Vector2.zero;c.crouch=false;c.guard=true;
                    // A chapter deck guard backing away from its target still stops short of its deck edge.
                    if(actor.HoldTier)HoldCommand(actor,ref c);
                    return c;
                }
            }
            if(!actor.ranged&&!actor.Busy&&target.Busy&&distance<2.5f&&actor.cooldown>.2f)c.guard=true;
            if(!actor.Busy&&actor.action!=SFAction.Hurt&&!c.crouch&&ClearAttack(actor,target,actor.ranged)&&distance<range+.35f&&Mathf.Abs(target.lane-actor.lane)<(actor.ranged?1.8f:CourseMode?.6f:.55f)&&actor.cooldown<=0&&Mathf.Abs(target.Height-actor.Height)<(CourseMode?1.2f:1.3f)&&(!CourseMode||actor.motor.Body.linearVelocity.y<.5f)&&(!InChapter||!kicker||actor.motor.IsGrounded)&&Actors.Count(a=>!a.Friendly&&a.Alive&&a.Busy)<2)
                actor.BeginAttack(actor.ranged?SFAction.Shoot:kicker?SFAction.Kick:SFAction.Punch); // Course melee lane gate .6 (inside the .65 strike tolerance): a platform actor held at lane 0 still answers a street hero at the .55 clamp. Chapter kickers kick only from the ground (an airborne Kick is the hero aerial kick: x1.3 and a knockdown).
            // Chapters drop the opportunistic hop (under long stacked decks it loops: hop up, step off, hop up). Chapter enemies jump only to climb,
            // and the step-up clause waits for a settled target there, so a hero jumping on a deck under a higher one draws no hop (chapter 0 unchanged).
            c.jump=CourseMode&&actor.motor.IsGrounded&&(distance>2&&rise>-.5f&&c.move.x!=0&&!InChapter||climb||target.Height>actor.Height+.65f&&(!InChapter||target.motor.IsGrounded&&Mathf.Abs(target.motor.Body.linearVelocity.y)<.5f))&&Platforms.Any(p=>actor.X>p.x-.5f&&actor.X<p.x+p.width&&actor.Height<p.height-.1f&&p.height-actor.Height<1.8f);
            CourseStepCommand(actor,target,ref c);
            // Chapter deck guards (HoldTier, only ever set from chapter data) never jump and stop .5 short of their deck's edges.
            if(actor.HoldTier)HoldCommand(actor,ref c);
            return c;
        }

        public void ResolveAttack(SFActor attacker,SFAction action)
        {
            if(action==SFAction.ClinchKnee)
            {
                var held=attacker.GripTarget;
                if(held!=null&&held.Alive&&ClearAttack(attacker,held,false))
                {if(held.Damage(OutgoingDamage(attacker,14,SFAction.ClinchKnee),attacker,false,true)){RegisterHit(attacker);attacker.ClinchHits++;if(attacker.ClinchHits>=3)attacker.ReleaseGrip(true);}}
                return;
            }
            bool ground=action==SFAction.GroundStrike||action==SFAction.Sweep;
            float reach=action==SFAction.Shoot?(attacker.ShotRifle?(attacker.hero?attacker.RifleReach:12):9)+(attacker.ShotAimed?3:0):action==SFAction.Kick?2.4f:ground?1.85f:attacker.boss?2.4f:1.7f;
            float damage=action==SFAction.Shoot?(attacker.ShotRifle?(attacker.hero?attacker.RifleDamage:24):30)*(attacker.ShotAimed?1.15f:1):action==SFAction.Kick?26:ground?20:attacker.boss?22:attacker.hero?17:11;
            if(attacker.ComboAttack){damage=attacker.ComboMove.damage;reach=attacker.ComboMove.reach;}
            if(CourseMode&&attacker.hero&&action==SFAction.Shoot)reach=Mathf.Min(reach,11f); // Course: no shots past the 11-unit enemy activation radius.
            if(attacker.hero&&attacker!=Player&&CourseMode)damage*=.65f;
            if(action==SFAction.Kick&&attacker.AerialAttack)damage*=1.3f;
            damage=OutgoingDamage(attacker,damage,action);
            if(attacker.ally)damage=14;
            float depthReach=action==SFAction.Shoot?(!attacker.hero?1.8f:attacker.ShotAimed?.4f:.65f):.65f;
            // Let the hero who owns a clinch finish it; teammates engage other threats.
            var candidates=Actors.Where(a=>a.Friendly!=attacker.Friendly&&a.Alive&&(a.HeldBy==null||a.HeldBy==attacker||a.HeldBy.Friendly!=attacker.Friendly)&&SFMath.WithinStrike(attacker.GroundPosition,attacker.Height,attacker.facing,a.GroundPosition,a.Height,reach,depthReach)).OrderBy(a=>Mathf.Abs(a.X-attacker.X)).ToArray();
            if(action==SFAction.Shoot)
            {
                Vector3 end=ShotEndpoint(attacker,reach);bool blocked=Mathf.Abs(end.x-attacker.X)<reach-.02f;
                var target=candidates.FirstOrDefault();
                if(target!=null)
                {
                    var cover=ShotCover(attacker,target);
                    if(cover!=null)
                    {
                        float sh=attacker.Height+attacker.MuzzleHeight,th=target.Height+(target.Crouching?.85f:1.55f);
                        cover.Clip(attacker.GroundPosition,sh,target.GroundPosition,th,out float fraction);
                        Vector2 point=Vector2.Lerp(attacker.GroundPosition,target.GroundPosition,fraction);
                        end=new Vector3(point.x,-3+point.y+Mathf.Lerp(sh,th,fraction),0);blocked=true;
                        if(attacker==Player)Notify("SHOT BLOCKED BY COVER");
                    }
                    else
                    {
                        end=target.VisualPosition+Vector3.up*(target.Crouching?.85f:1.55f);blocked=false;
                        if(target.Damage(damage,attacker)&&attacker.hero){Bond=Mathf.Min(100,Bond+7);RegisterHit(attacker);}
                    }
                }
                FirePresentation(attacker,end,blocked);return;
            }
            foreach(var target in candidates)
            {
                if(!ClearAttack(attacker,target,false))continue;
                bool wasDown=target.action==SFAction.Knocked;
                if(target.Damage(ground&&wasDown?damage*1.6f:damage,attacker)){attacker.ConfirmComboHit();
                    if((ground||attacker.AerialAttack||(attacker.ComboAttack&&attacker.ComboMove.finisher))&&target.Alive&&!target.boss)target.KnockDown();
                    if(attacker.ComboAttack&&attacker.ComboMove.finisher){float next=HoldClampX(target,ClampCoverX(target,target.X+attacker.facing*.65f));target.motor.Body.position=new Vector2(next,target.Height);target.transform.position=new Vector3(next,target.Height,0);Impact(target.VisualPosition+Vector3.up*1.4f,Gold,"",.10f);}if(attacker.hero){Bond=Mathf.Min(100,Bond+7);RegisterHit(attacker);}}
                if(action!=SFAction.Kick)break;
            }

        }

        public bool TryGrapple(SFActor actor,float reach=1.9f,bool quiet=false,SFActor chosen=null)
        {
            if(State!=SFState.Playing||!actor.Alive||actor.Busy||actor.Restrained||actor.GripTarget!=null||!actor.motor.IsGrounded)return false;
            var target=Actors.Where(a=>(chosen==null||a==chosen)&&a.Friendly!=actor.Friendly&&a.Alive&&!a.Restrained&&a.HeldBy==null&&SFMath.WithinStrike(actor.GroundPosition,actor.Height,actor.facing,a.GroundPosition,a.Height,reach,.5f)&&Mathf.Abs(a.Height-actor.Height)<.35f).OrderBy(a=>Mathf.Abs(a.X-actor.X)).FirstOrDefault();
            if(target!=null&&!ClearAttack(actor,target,false))target=null;
            if(target==null){if(!quiet)Notify("Move closer and face an opponent to grapple.");return false;}
            if(target.boss&&target.health>target.maxHealth*.35f){if(!quiet)Notify("Opponent resists. Create an opening first.");actor.cooldown=.4f;return false;}
            actor.GripTarget=target;actor.GripTime=3.4f;actor.ClinchHits=0;target.HeldBy=actor;actor.action=SFAction.Grapple;target.action=SFAction.Held;
            actor.actionClock=target.actionClock=0;actor.motor.SetCommand(0,false);target.motor.SetCommand(0,false);
            actor.Armed=false;
            float gripX=HoldClampX(target,ClampCoverX(target,actor.X+actor.facing*.85f));target.Teleport(new Vector2(gripX,target.Height));target.lane=actor.lane;target.facing=-actor.facing;
            Notify(TouchControls?"CLINCH  /  KNEE   ·   THROW   ·   TAKEDOWN":GamepadActive?"CLINCH  /  RT: KNEE   ·   X: THROW   ·   RB: TAKEDOWN":"CLINCH  /  LMB: KNEE   ·   RMB: THROW   ·   G: TAKEDOWN");return true;
        }

        public string PartnerDownHint()=>TouchControls?"PARTNER DOWN / INTERACT NEARBY / ASSIST: 35":GamepadActive?"PARTNER DOWN / D-PAD UP NEARBY / RIGHT: 35":"PARTNER DOWN / E NEARBY TO REVIVE / V: 35 BOND";

        public string InteractionHint()
        {
            string key=TouchControls?"INTERACT":GamepadActive?"D-PAD UP":"E";
            if(!Partner.Alive&&Mathf.Abs(Player.X-Partner.X)<2.3f&&Mathf.Abs(Player.Height-Partner.Height)<1&&Mathf.Abs(Player.lane-Partner.lane)<.65f)return key+"  /  HELP PARTNER UP";
            var item=Pickups.FirstOrDefault(p=>!p.taken&&Mathf.Abs(Player.X-p.x)<1.1f&&Mathf.Abs(Player.Height+.8f-p.height)<.8f&&Mathf.Abs(Player.lane-p.lane)<.65f);
            if(item!=null)return key+"  /  "+(item.kind=="data"?"RECOVER INTEL":item.kind=="ammo"?"TAKE AMMUNITION":"TAKE "+item.kind.ToUpperInvariant());
            if(!CourseMode||!ExtractionReach)return "";
            // Same blockers, same order as TryExtract. PartnerLevel is always true in chapter 0, so its text is unchanged; chapters name a partner on another tier.
            // Chapters name the boss by its short name, so "INTERACT  /  EXTRACT  -  STOP ..." stays on one line of the narrowest hint box.
            bool bossDown=BossDown;
            return key+"  /  "+(DataCount<3?"EXTRACT  -  "+DataCount+" OF 3 INTEL":!bossDown?(InChapter?"EXTRACT  -  STOP "+Chapter.bossShort:"EXTRACT  -  STOP VESPER"):!Partner.Alive||Mathf.Abs(Partner.X-Player.X)>=6?"EXTRACT  -  PARTNER WITHIN 6":!PartnerLevel?"EXTRACT  -  PARTNER UP HERE":"EXTRACT TOGETHER");
        }
        public bool TryInteract()
        {
            if(State!=SFState.Playing||!Player.Alive||Player.Busy||Player.Restrained)return false;
            if(!Partner.Alive&&Mathf.Abs(Player.X-Partner.X)<2.3f&&Mathf.Abs(Player.Height-Partner.Height)<1&&Mathf.Abs(Player.lane-Partner.lane)<.65f){Partner.Revive();Notify("PARTNER RECOVERED");return true;}
            var item=Pickups.FirstOrDefault(p=>!p.taken&&Mathf.Abs(Player.X-p.x)<1.1f&&Mathf.Abs(Player.Height+.8f-p.height)<.8f&&Mathf.Abs(Player.lane-p.lane)<.65f);
            if(item!=null){TryCollect(item);return item.taken;}
            if(CourseMode&&ExtractionReach){TryExtract();return true;}return false;
        }
        public void SetThumbSwap(bool value){SwapThumbs=value;PlayerPrefs.SetInt("SF.SwapThumbs",value?1:0);PlayerPrefs.Save();}

        public bool TryAssist()
        {
            if(State!=SFState.Playing||!Player.Alive)return false;
            if(AssistCooldown>0||Bond<35){Notify("Partner assist is recharging.");return false;}
            if(Mathf.Abs(Player.X-Partner.X)>7){Notify("Move closer to your partner.");return false;}
            Bond-=35;AssistCooldown=6;Assists++;
            if(!Partner.Alive)Partner.Revive();else Partner.health=Mathf.Min(Partner.maxHealth,Partner.health+16);
            Player.invulnerable=Mathf.Max(Player.invulnerable,.8f);Partner.invulnerable=Mathf.Max(Partner.invulnerable,.8f); // Keep Revive()'s longer window.
            foreach(var enemy in Actors.Where(a=>!a.Friendly&&a.Alive&&Mathf.Min(Mathf.Abs(a.X-Player.X),Mathf.Abs(a.X-Partner.X))<3.8f).ToArray())if(ClearAttack(Player,enemy,false)||ClearAttack(Partner,enemy,false))enemy.Damage(38,Player);
            Trace(Player.VisualPosition+Vector3.up,Partner.VisualPosition+Vector3.up,Teal);
            Impact(Partner.VisualPosition+Vector3.up,Teal,"TOGETHER",.07f);Audio.Play("assist",.4f);Say(Partner.identity.ToUpperInvariant(),"I've got you.");return true;
        }

        public bool Swap()
        {
            if(State!=SFState.Playing||swapCooldown>0||!Partner.Alive)return false;
            Player.ReleaseGrip();Player.Aiming=false;Selected=1-Selected;swapCooldown=.6f;Notify("LEAD  /  "+Player.identity.ToUpperInvariant());return true;
        }

        public void Defeated(SFActor actor)
        {
            // finalBoss is set only in chapters: a chapter mid-boss gets its own notice and no "done here" line, since extraction is still locked.
            // A mid-boss taken after the final boss (backtracking) names itself and points at extraction, so it never says KEEP CLIMBING once extraction is open.
            if(!actor.Friendly){KOs++;if(actor.boss){if(finalBoss!=null&&actor!=finalBoss)Notify(finalBoss.Alive?(Chapter.midBossDownNotice??"BOSS DOWN  /  KEEP CLIMBING"):SFEnemyProfiles.Title(actor.identity)+" DOWN  /  REACH "+Chapter.extractionLabel);else{Notify(InChapter?Chapter.bossDownNotice:CourseMode?"BOSS DISARMED  /  REACH EXTRACTION":"BOSS DEFEATED  /  AREA CLEAR");Say("STEFANIE","We're done here. Let's go.");}}}
            else if(actor.hero) {Notify(PartnerDownHint());if(actor==Player&&Partner.Alive){Selected=1-Selected;swapCooldown=.6f;}}
        }

        void TickPickups(float dt)
        {
            foreach(var item in Pickups)
            {
                if(item.taken)continue;
                item.visual.transform.position=new Vector3(item.x,-3+item.height+item.lane+Mathf.Sin(Elapsed*3)*.07f,0);
                if(item.kind=="data"||(item.kind=="ammo"&&CourseMode))item.visual.transform.Rotate(0,0,dt*30);
            }
        }

        public void TryCollect(SFPickup item,SFActor collector=null)
        {
            collector=collector??Player;var recipient=collector==Player?Partner:Player;
            if(item.taken||!collector.Alive||Mathf.Abs(collector.X-item.x)>=1.1f||Mathf.Abs(collector.Height+.8f-item.height)>=.8f||Mathf.Abs(collector.lane-item.lane)>.65f)return;
            if((item.kind=="food"||item.kind=="medical")&&collector.health>=collector.maxHealth&&recipient.health>=recipient.maxHealth){Notify("TEAM HEALTH FULL  /  SUPPLIES SAVED");return;}
            if(item.kind=="drink"&&Bond>=100){Notify("PARTNER ENERGY FULL  /  DRINK SAVED");return;}
            item.taken=true;item.visual.enabled=false;
            if(item.kind=="data"){DataCount++;Bond=Mathf.Min(100,Bond+20);Notify("INTEL RECOVERED  /  "+DataCount+" OF 3");}
            else if(item.kind=="ammo"){collector.Magazine.Supply(12);Notify(collector.identity.ToUpperInvariant()+"  /  +12 RESERVE ROUNDS");}
            else if(item.kind=="drink"){Bond=Mathf.Min(100,Bond+25);Notify("HYDRATION  /  +25 PARTNER ENERGY");}
            else {collector.health=Mathf.Min(collector.maxHealth,collector.health+(item.kind=="food"?30:55));if(recipient.Alive&&Vector2.Distance(collector.GroundPosition,recipient.GroundPosition)<4)recipient.health=Mathf.Min(recipient.maxHealth,recipient.health+(item.kind=="food"?20:35));Notify("RECOVERY SUPPLIES  /  TEAM HEALTH");}
            Audio.Play("collect",.35f);Impact(item.visual.transform.position,Teal,"",0);
        }

        public void TryExtract()
        {
            bool bossDown=BossDown;
            if(DataCount==3&&bossDown&&Partner.Alive&&Player.Alive&&Mathf.Abs(Partner.X-Player.X)<6&&PartnerLevel)SetState(SFState.Won);
            else{string reason=DataCount<3?"Recover all three pieces of intel before extraction.":!bossDown?(InChapter?Chapter.bossBlock:"Vesper is blocking extraction."):"Bring your partner to extraction.";if(NoticeTime<=0||Notice!=reason)Notify(reason);} // A different live notice never hides why extraction failed.
        }

        public bool HazardActive => Elapsed%3.5f>1.4f;
        internal void TickHazards()
        {
            if(!CourseMode||State!=SFState.Playing||DriveActive)return;
            // Chapters tint their strips (Rio teal, Al Anbar welding orange). Chapter 0 keeps Teal.
            var tint=InChapter?Chapter.hazardTint:Teal;
            foreach(var strip in hazardStrips){var color=Color.Lerp(tint,Gold,.5f+.5f*Mathf.Sin(Elapsed*12));color.a=HazardActive?.48f:.08f;strip.color=color;}
            foreach(var wash in hazardWashes)wash.color=new Color(tint.r,tint.g,tint.b,HazardActive?.18f:0); // The whole damage span stays readable, below the contact shadows.
            if(!HazardActive)return;
            // Simulation owns continuous hazard damage: sleeping bodies do not emit trigger-stay callbacks.
            // Actor invulnerability provides the same hit cadence for moving and stationary heroes.
            // A hero is on hazard i's surface within .3 (street strips: today's Height<.3) and inside its half-width (.75 in chapter 0).
            // Deck strips (chapters only) also need the hero not rising: jumping up through a one-way deck reads as grounded.
            // Street strips, and so all of chapter 0, keep today's test (HzH(i)<=0 short-circuits the velocity check).
            foreach(var hero in new[]{Fernando,Stefanie})
                if(hero.Alive&&hero.motor.IsGrounded&&Enumerable.Range(0,Hazards.Length).Any(i=>HzDeck(hero.Height,i,.3f)&&Mathf.Abs(hero.X-Hazards[i])<HzW(i)&&(HzH(i)<=0||hero.motor.Body.linearVelocity.y<=.1f)))hero.Damage(13,null,true);
            for(int i=0;i<Hazards.Length;i++)
            {
                if(UnityEngine.Random.value<.15f)Impact(new Vector3(Hazards[i]+UnityEngine.Random.Range(-1f,1f),-2.9f+HzH(i),0),tint,"",0);
            }
        }

        public void Say(string speaker,string text){Speaker=speaker;Speech=text;SpeechTime=5;AnnounceBoss(speaker);}
        public void Notify(string text){Notice=text;NoticeTime=3.5f;}
        public void Impact(Vector3 position,Color color,string label,float intensity)
        {
            shake=Mathf.Max(shake,intensity);
            for(int i=0;i<5;i++)
            {
                var sr=CreateSprite("Impact",Pixel,color,200);sr.transform.position=position;sr.transform.localScale=new Vector3(.045f,.10f,1);
                effects.Add(new SFEffect{visual=sr,velocity=UnityEngine.Random.insideUnitCircle*2,life=.22f,maxLife=.22f});
            }
        }
        public void Trace(Vector3 from,Vector3 to,Color color)
        {
            var sr=CreateSprite("Trace",Pixel,color,190);sr.transform.position=(from+to)/2;Vector3 delta=to-from;sr.transform.localScale=new Vector3(delta.magnitude,.025f,1);sr.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            effects.Add(new SFEffect{visual=sr,life=.12f,maxLife=.12f});
        }
        void TickEffects(float dt)
        {
            for(int i=effects.Count-1;i>=0;i--){var e=effects[i];e.life-=dt;if(e.life<=0){Destroy(e.visual.gameObject);effects.RemoveAt(i);}else{e.visual.transform.position+=(Vector3)e.velocity*dt;Color c=e.visual.color;c.a=e.life/e.maxLife;e.visual.color=c;}}
        }

        public SFCommand SmokeCommand;
        IEnumerator Smoke()
        {
            // Executes actual runtime systems, including the inherited Rigidbody2D motor.
            yield return new WaitForSeconds(1);Capture("01-title");yield return new WaitForSeconds(.3f);StartGame();
            var log=new List<string>();yield return new WaitForSeconds(.15f);
            Check(Mathf.Abs(Fernando.X-3)<.1f,"start resets title positions to chapter entrance",log);float start=Player.X;
            SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.6f);
            Check(Player.X>start+1,"classroom motor moves hero",log);
            SmokeCommand=new SFCommand{jump=true};yield return new WaitForSeconds(.05f);SmokeCommand=default;yield return new WaitForSeconds(.25f);
            Check(Player.Height>.7f,"classroom motor jumps",log);yield return new WaitForSeconds(1);
            Player.Teleport(new Vector2(15,.03f));yield return new WaitForSeconds(.1f);
            SmokeCommand=new SFCommand{jump=true};yield return new WaitForSeconds(.05f);SmokeCommand=default;yield return new WaitForSeconds(.28f);
            Check(Player.Height>1.15f,"jump passes through one-way platform from below",log);yield return new WaitForSeconds(.7f);
            Check(Mathf.Abs(Player.Height-1.1f)<.15f,"physics lands hero on one-way platform",log);
            Player.Teleport(new Vector2(8,.03f));yield return new WaitForSeconds(.1f);
            var enemy=Actors.First(a=>!a.hero);enemy.Teleport(new Vector2(Player.X+1.1f,0));enemy.lane=Player.lane;float hp=enemy.health;
            Player.facing=1;SmokeCommand=new SFCommand{punch=true};yield return new WaitForSeconds(.65f);SmokeCommand=default;
            Check(enemy.health<hp,"melee applies damage",log);Capture("02-combat");
            Check(Swap(),"character swap",log);yield return new WaitForSeconds(.1f);
            Partner.motor.Body.position=new Vector2(Player.X-1,0);Partner.health=0;Partner.recovery=8;float bond=Bond;
            Check(TryAssist()&&Partner.Alive&&Bond<bond,"assist revives partner and spends bond",log);
            SetState(SFState.Paused);float before=Player.X;yield return new WaitForSeconds(.3f);Check(Mathf.Abs(Player.X-before)<.01f,"pause freezes physics",log);SetState(SFState.Playing);
            yield return new WaitForSeconds(.8f);
            foreach(var p in Pickups.Where(p=>p.kind=="data")){Player.Teleport(new Vector2(p.x,p.height-.8f));Player.lane=p.lane;SmokeCommand=new SFCommand{interact=true};yield return new WaitForSeconds(.22f);SmokeCommand=default;Debug.Log("Signal check: "+p.x+" taken="+p.taken+" state="+Player.action);}
            Check(DataCount==3,"all signal pickups collected",log);
            foreach(var a in Actors.Where(a=>!a.hero)){a.health=0;a.actionClock=4;}
            Player.health=0;Partner.health=0;Player.recovery=Partner.recovery=8;Player.action=Partner.action=SFAction.Down;yield return null;Check(State==SFState.Lost,"both down triggers defeat",log);
            RetryCheckpoint();Check(Mathf.Abs(Fernando.X-Checkpoint)<.1f,"checkpoint resets physical position",log);
            yield return new WaitForSeconds(.1f);Check(State==SFState.Playing&&Player.Alive&&Partner.Alive,"checkpoint retry restores team",log);
            Player.motor.Body.position=new Vector2(129,.2f);Partner.motor.Body.position=new Vector2(127,.2f);yield return new WaitForSeconds(.4f);TryInteract();
            Check(State==SFState.Won,"signals plus boss plus extraction trigger victory",log);Capture("03-complete");
            string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../QA"));System.IO.Directory.CreateDirectory(path);System.IO.File.WriteAllLines(System.IO.Path.Combine(path,"runtime-checks.txt"),log);
            yield return new WaitForSeconds(1);Application.Quit(log.Any(s=>s.StartsWith("FAIL"))?2:0);
        }
        void Check(bool condition,string name,List<string> log){log.Add((condition?"PASS: ":"FAIL: ")+name);Debug.Log(log[log.Count-1]);}
        public void Capture(string name)
        {
            // Camera render works even when the Windows smoke-test window is hidden. HUD is reviewed in the browser.
            string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../QA"));System.IO.Directory.CreateDirectory(path);
            var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);var old=Camera.targetTexture;var active=RenderTexture.active;
            Camera.targetTexture=rt;Camera.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(path,name+"-world.png"),tex.EncodeToPNG());
            Camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(tex);
        }
    }
    public sealed class SFPickup{public float x,height,lane;public string kind;public bool taken;public SpriteRenderer visual;}
    public sealed class SFEffect{public SpriteRenderer visual;public Vector2 velocity;public float life,maxLife;}
}
