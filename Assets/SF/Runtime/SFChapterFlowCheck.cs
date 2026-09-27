using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StefanieAndFernando
{
    // F12 chapter flow (-sfChapterFlow), with Rio's JLTV interlude. Every stage runs in a fresh scene after Restart(); Stage and the log are
    // static, so they survive the reloads. Stages run in the order 0 1 2 3 4 5 8 9 6.
    //   0 Curitiba: Won keeps its notice and persists; NEXT takes the default focus above RESTART CHAPTER and RETURN TO CAMPAIGN; Enter advances.
    //   1 Rio: loaded fresh with the lead carried (Stefanie); RESTART CHAPTER keeps the chapter.
    //   2 Rio after the restart: no road during play; controller Start on the Won card opens the JLTV interlude (locked vehicle, course
    //     titles, desert plates or their fallbacks); LEAVE ROAD returns to the Won card without a second result cue; DESERT DRIVE again,
    //     drive to the forward base (retrying on a recovery), and E there loads Al Anbar.
    //   3 Al Anbar: 17 decks, two bosses with armored_boss final; the Won card's NEXT button (controller A) wraps to Curitiba.
    //   4 Curitiba again: the F1 constants and no carried lead; GoToChapter(99) clamps to the last chapter.
    //   5 Al Anbar: the carried lead is Fernando's; GoToChapter(1) reopens Rio.
    //   8 Rio: the interlude's ready card has SKIP DRIVE in CHANGE's slot; controller A on it cuts to Al Anbar.
    //   9 Al Anbar after the skip: RETURN TO CAMPAIGN from the Won card.
    //   6 Campaign: chapter 0, CourseMode false, no NEXT; its Won card still opens the untinted Serra road with CHANGE.
    // Under -sfNoChapters stage 0 instead proves NEXT is hidden and Enter, Start and AdvanceCourse leave the Won card alone.
    // Ends with 'CHAPTER FLOW QA COMPLETE: PASS|FAIL' and QA/chapter-flow.txt. A local WebGL build starts it with RunChapterFlowQA().
    public sealed class SFChapterFlowCheck:MonoBehaviour
    {
        internal const int Done=-2,CampaignReload=10;
        // -1 idle, 0..6 and 8..9 the stage the next scene runs, 7 the kill-switch stage expecting no reload, CampaignReload while stage 6
        // watches the campaign's Won card ignore AdvanceCourse (a reload then fails), Done after the result line.
        internal static int Stage=-1;
        static readonly List<string> log=new List<string>();
        static bool savedShake=true;
        // RunChapterFlowQA (a local WebGL build has no -sfChapterFlow argument) arms a fresh run for the next scene load.
        static bool webRequested;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){Stage=-1;log.Clear();savedShake=true;webRequested=false;}
        internal static bool Requested=>Stage>=0||Stage==-1&&(webRequested||Environment.GetCommandLineArgs().Contains("-sfChapterFlow"));
        internal static bool Request(){if(Stage>=0)return false;Stage=-1;log.Clear();webRequested=true;return true;}

        SFGame g;SFHUD hud;
        void Check(bool ok,string name){string line=(ok?"PASS: ":"FAIL: ")+name;log.Add(line);Debug.Log(line);}
        void Info(string text){string line="INFO: "+text;log.Add(line);Debug.Log(line);}
        void Send(Gamepad pad,GamepadState state){InputSystem.QueueStateEvent(pad,state);InputSystem.Update();g.ProcessGamepad();}
        static bool Near(float a,float b,float tolerance=.01f)=>Mathf.Abs(a-b)<=tolerance;
        // GUI space grows downward, as in SFHUD.TickGamepadMenu (stick up moves focus toward smaller y).
        static readonly Vector2 ScreenUp=new Vector2(0,-1),ScreenDown=new Vector2(0,1);

        IEnumerator Start()
        {
            g=GetComponent<SFGame>();hud=GetComponent<SFHUD>();
            if(Stage<0){Stage=0;webRequested=false;log.Clear();savedShake=g.ShakeEnabled;}
            yield return new WaitForSeconds(.3f);
            Debug.Log("Chapter flow QA: stage "+Stage+", chapter "+g.CourseChapter+", course "+g.CourseMode+", chapters enabled "+SFGame.ChaptersEnabled);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach(var stale in InputSystem.devices.Where(d=>d.name.StartsWith("SFFlow",StringComparison.Ordinal)).ToArray())InputSystem.RemoveDevice(stale);
            switch(Stage)
            {
                case 0:yield return Curitiba();break;
                case 1:yield return RioFresh();break;
                case 2:yield return RioRestarted();break;
                case 3:yield return Anbar();break;
                case 4:yield return CuritibaAgain();break;
                case 5:yield return AnbarClamped();break;
                case 6:Campaign();yield return new WaitForSeconds(.5f);Stage=6;Check(g.State==SFState.Won&&!g.CourseMode,"stage 6: the campaign's Won state ignores AdvanceCourse");CampaignRoad();Finish();break;
                case 7:Check(false,"-sfNoChapters: the Won card reloaded a scene");Finish();break;
                case CampaignReload:Check(false,"stage 6: the campaign's Won card reloaded a scene");Finish();break;
                case 8:yield return RioSkip();break;
                case 9:yield return ReturnToCampaign();break;
                default:Check(false,"unknown flow stage "+Stage);Finish();break;
            }
        }

        // Waits for the queued scene load; if this scene is still alive after 3 s, the step failed.
        IEnumerator AwaitLoad(string step)
        {
            yield return new WaitForSecondsRealtime(3);
            Check(false,step+" (no scene load within 3 s)");Finish();
        }

        // Reads the Won card from the last rendered frame. The first control drawn takes the default controller focus, and moving down
        // from NEXT must reach RESTART CHAPTER (y610) and then RETURN TO CAMPAIGN (y684). Focus is put back on the first control afterwards.
        IEnumerator WonCard(bool next,string tag)
        {
            for(int i=0;i<3;i++)yield return null;
            yield return new WaitForEndOfFrame();
            var menu=hud.PadMenu;
            if(menu.Count==0){Info(tag+": no rendered HUD frame, Won card layout not checked");yield break;}
            var first=menu.Focus;
            if(next)
            {
                menu.Move(ScreenUp);menu.Move(ScreenUp);var top=menu.Focus;
                menu.Move(ScreenDown);var restart=menu.Focus;menu.Move(ScreenDown);var back=menu.Focus;
                menu.Move(ScreenUp);menu.Move(ScreenUp);
                Check(first.y==467&&first.height==58&&top==first,tag+": the Won card draws NEXT first (y467, primary), so it takes the default controller focus");
                Check(restart.y==610&&restart.height==48&&back.y==684&&back.height==48&&menu.Focus==first,tag+": RESTART CHAPTER and RETURN TO CAMPAIGN stay below NEXT ("+menu.Count+" controls)");
            }
            else Check(first.y==610&&first.height==48,tag+": no NEXT button; RESTART CHAPTER keeps the default focus as in 0.8.44");
        }

        IEnumerator Curitiba()
        {
            if(!g.CourseMode){Check(false,"stage 0: the flow check must start in the course, not the campaign");Finish();yield break;}
            if(g.CourseChapter!=0){Info("stage 0 started on chapter "+g.CourseChapter+"; loading Curitiba first");g.GoToChapter(0);yield return AwaitLoad("stage 0: GoToChapter(0)");yield break;}
            Check(g.CourseMode&&g.CourseChapter==0&&!g.InChapter&&g.State==SFState.Menu,"stage 0: the course opens on Curitiba (chapter 0) at its menu");
            if(!SFGame.ChaptersEnabled){yield return KillSwitch();yield break;}
            g.Selected=1;g.StartGame();
            Check(g.State==SFState.Playing&&g.Player==g.Stefanie,"stage 0: Curitiba starts with Stefanie leading");
            g.SetState(SFState.Won);
            Check(g.Notice=="Both home. Mission complete."&&g.CourseHasNext&&g.Chapter.nextLabel=="NEXT CHAPTER  /  RIO  >","stage 0: Curitiba's Won notice is unchanged and NEXT reads 'NEXT CHAPTER  /  RIO  >'");
            yield return new WaitForSeconds(.5f);
            Check(g.State==SFState.Won&&g.CourseChapter==0,"stage 0: Won persists; nothing auto-advances");
            yield return WonCard(true,"stage 0");
            var keyboard=InputSystem.AddDevice<Keyboard>("SFFlowKeyboard");keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            Stage=1;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));InputSystem.Update();g.ProcessPauseAndRetryKeys();
            InputSystem.RemoveDevice(keyboard);
            Check(g.CourseChapter==1,"stage 0: Enter on Curitiba's Won card queues Rio (chapter 1)");
            yield return AwaitLoad("stage 0: Enter loads Rio");
        }

        IEnumerator KillSwitch()
        {
            g.StartGame();g.SetState(SFState.Won);
            Check(!g.CourseHasNext&&g.Notice=="Both home. Mission complete.","-sfNoChapters: CourseHasNext is false on Curitiba's Won card");
            yield return WonCard(false,"-sfNoChapters");
            Stage=7;
            var keyboard=InputSystem.AddDevice<Keyboard>("SFFlowKeyboard");keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));InputSystem.Update();g.ProcessPauseAndRetryKeys();
            InputSystem.RemoveDevice(keyboard);
            var pad=InputSystem.AddDevice<Gamepad>("SFFlowPad");pad.MakeCurrent();
            Send(pad,new GamepadState().WithButton(GamepadButton.South));Send(pad,default);Send(pad,new GamepadState().WithButton(GamepadButton.Start));Send(pad,default);
            InputSystem.RemoveDevice(pad);
            g.AdvanceCourse();
            yield return new WaitForSeconds(1);
            Check(g.State==SFState.Won&&g.CourseChapter==0&&!g.InChapter,"-sfNoChapters: Enter, Start and AdvanceCourse leave Curitiba's Won card in place");
            Finish();
        }

        IEnumerator RioFresh()
        {
            var ch=SFCourseChapters.All[1];
            Check(g.CourseMode&&g.CourseChapter==1&&g.InChapter&&g.Chapter==ch&&g.State==SFState.Menu,"stage 1: NEXT loads Rio (chapter 1) at its menu");
            Check(g.Selected==1&&g.Player==g.Stefanie,"stage 1: the lead carries over (Stefanie preselected)");
            Check(g.DataCount==0&&g.KOs==0&&g.Assists==0&&g.Elapsed==0&&Near(g.Bond,70)&&g.Fernando.health==g.Fernando.maxHealth&&g.Stefanie.health==g.Stefanie.maxHealth,"stage 1: Rio starts fresh (no intel, KOs, assists, time or damage carried)");
            Check(g.Platforms.Length==ch.platforms.Length&&g.RouteIndex==ch.musicRoute&&g.Actors.Count(a=>!a.Friendly)==ch.cast.Length&&g.FinalBoss!=null&&g.FinalBoss.identity=="merc_boss","stage 1: Rio builds its "+ch.platforms.Length+" decks, music route "+ch.musicRoute+" and cast with the Broker final");
            yield return null;
            Stage=2;g.Restart();
            yield return AwaitLoad("stage 1: RESTART CHAPTER reloads");
        }

        IEnumerator RioRestarted()
        {
            var ch=SFCourseChapters.All[1];
            Check(g.CourseMode&&g.CourseChapter==1&&g.InChapter&&g.State==SFState.Menu&&g.DataCount==0,"stage 2: RESTART CHAPTER reloads Rio, not Curitiba");
            g.Selected=1;g.StartGame();
            Check(g.State==SFState.Playing&&g.Speaker==ch.introSpeaker&&g.Speech==ch.intro,"stage 2: Rio starts with its own intro line");
            Check(!g.StartDrive(false,false,true)&&!g.StartDrive()&&!g.DriveActive&&g.State==SFState.Playing,"stage 2: no road opens during live play");
            g.SetState(SFState.Won);
            Check(g.Notice==ch.wonNotice&&g.CourseHasNext&&ch.driveAfter&&g.Chapter.nextLabel=="DESERT DRIVE  /  AL ANBAR  >","stage 2: Rio's Won card offers 'DESERT DRIVE  /  AL ANBAR  >' (driveAfter)");
            yield return new WaitForSeconds(.5f);
            Check(g.State==SFState.Won&&g.CourseChapter==1&&!g.DriveActive,"stage 2: Won persists in Rio; nothing auto-advances");
            yield return WonCard(true,"stage 2");
            var pad=InputSystem.AddDevice<Gamepad>("SFFlowPad");pad.MakeCurrent();
            Send(pad,new GamepadState().WithButton(GamepadButton.South));Send(pad,default);
            Check(g.GamepadActive&&!g.Pad.WaitingForNeutral&&g.State==SFState.Won&&g.CourseChapter==1,"stage 2: the first controller contact only arms the pad on the Won card");
            int resultPlays=g.Audio.ResultPlays;
            Send(pad,new GamepadState().WithButton(GamepadButton.Start));Send(pad,default);
            InputSystem.RemoveDevice(pad);
            Check(g.DriveActive&&g.Drive.JLTV&&g.Drive.Phase==SFDrivePhase.Ready&&g.State==SFState.Playing&&g.CourseChapter==1&&!g.CourseHasNext,"stage 2: controller Start on Rio's Won card opens the JLTV interlude instead of loading a chapter");
            if(!g.DriveActive){Finish();yield break;}
            Check(g.JourneyTitle=="INTERLUDE  /  AL ANBAR DESERT CONVOY"&&g.DriveExitLabel=="CONTINUE TO AL ANBAR"&&g.ConvoyVehicle=="DESERT JLTV","stage 2: the interlude reads 'INTERLUDE  /  AL ANBAR DESERT CONVOY', DESERT JLTV and 'CONTINUE TO AL ANBAR'");
            CoursePlates();
            var drive=g.Drive;g.ChangeReadyVehicle();
            Check(g.DriveActive&&g.Drive==drive&&g.Drive.JLTV&&g.Drive.Phase==SFDrivePhase.Ready,"stage 2: ChangeReadyVehicle is a no-op in the course (the Desert JLTV is locked)");
            g.AdvanceCourse();
            Check(g.Drive==drive&&g.CourseChapter==1&&!g.StartDrive(false,false,true),"stage 2: AdvanceCourse and a second StartDrive do nothing while the interlude runs");
            // Pause card LEAVE ROAD.
            g.SetState(SFState.Paused);g.EndDrive();
            Check(!g.DriveActive&&g.State==SFState.Won&&g.CourseChapter==1&&g.CourseHasNext&&g.World.gameObject.activeSelf&&GameObject.Find("Serra pickup interlude")==null,"stage 2: LEAVE ROAD returns to Rio's Won card and DESERT DRIVE is offered again");
            Check(g.Audio.ResultPlays==resultPlays,"stage 2: returning from the road does not replay the result cue");
            g.AdvanceCourse();
            Check(g.DriveActive&&g.Drive!=drive&&g.Drive.JLTV&&g.Drive.Phase==SFDrivePhase.Ready,"stage 2: DESERT DRIVE reopens a fresh interlude");
            if(!g.DriveActive){Finish();yield break;}
            // Drive it as the player would: driver on the throttle with the AI gunner, RETRY CHECKPOINT on any recovery.
            g.Drive.Board();int retries=0,ticks=0;
            for(;ticks<6000&&g.Drive.Phase!=SFDrivePhase.Complete;ticks++){if(g.Drive.Phase==SFDrivePhase.Recovery){retries++;g.Drive.Retry();}g.TickDrive(.05f,new SFCommand{punch=true});}
            Info("stage 2: interlude reached "+g.Drive.Phase+" after "+ticks+" ticks: distance "+g.Drive.Distance.ToString("0")+", condition "+g.Drive.Integrity+"%, "+g.Drive.Defeated+" stopped, "+retries+" checkpoint retries");
            Check(g.Drive.Phase==SFDrivePhase.Complete,"stage 2: the JLTV interlude can be driven to the forward base");
            if(g.Drive.Phase!=SFDrivePhase.Complete){g.EndDrive();Finish();yield break;}
            Stage=3;
            g.TickDrive(.05f,new SFCommand{interact=true});
            Check(!g.DriveActive&&g.CourseChapter==2&&GameObject.Find("Serra pickup interlude")==null,"stage 2: E at the forward base (FinishDrive) frees the road, then queues Al Anbar");
            yield return AwaitLoad("stage 2: FinishDrive loads Al Anbar");
        }

        // Interlude plate renderers under the drive root: departure 'Forest approach', arrival 'Coastal overlook'.
        static SpriteRenderer PlateRenderer(string name)
        {
            var root=GameObject.Find("Serra pickup interlude");if(root==null)return null;
            var t=root.transform.Find(name);return t!=null?t.GetComponent<SpriteRenderer>():null;
        }
        static SFArt UsablePlate(string id){var a=Resources.Load<SFArt>("SF/"+id);return a!=null&&a.frames!=null&&a.frames.Length==1&&a.frames[0]!=null?a:null;}
        static bool Rgb(Color a,Color b)=>Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b);

        // Course plates: desert art when present, else the Serra plates under the dust tint; plate_5_0 may only ever be the arrival.
        void CoursePlates()
        {
            var departure=PlateRenderer("Forest approach");var arrival=PlateRenderer("Coastal overlook");
            Check(departure!=null&&arrival!=null,"stage 2: the interlude keeps its 'Serra pickup interlude' root with departure and arrival plates");
            if(departure==null||arrival==null)return;
            var road=UsablePlate("plate_desert_convoy");var stop=UsablePlate("plate_desert_arrival");if(stop==null)stop=UsablePlate("plate_5_0");var dust=new Color(1,.86f,.66f);
            bool dep=road!=null?departure.sprite==road.frames[0]&&Rgb(departure.color,Color.white):departure.sprite==Resources.Load<SFArt>("SF/plate_serra_convoy").Frame(0)&&Rgb(departure.color,dust);
            bool arr=stop!=null?arrival.sprite==stop.frames[0]&&Rgb(arrival.color,Color.white):arrival.sprite==Resources.Load<SFArt>("SF/plate_serra_overlook_hd").Frame(0)&&Rgb(arrival.color,dust);
            Info("stage 2: interlude departure "+(road!=null?"plate_desert_convoy":"plate_serra_convoy + dust tint")+", arrival "+(stop!=null?stop.name:"plate_serra_overlook_hd + dust tint"));
            Check(dep&&arr,"stage 2: the interlude uses the desert plates or their fallbacks, never an Al Anbar foot plate under the vehicle");
        }

        IEnumerator Anbar()
        {
            var ch=SFCourseChapters.All[2];
            Check(g.CourseMode&&g.CourseChapter==2&&g.InChapter&&g.Chapter==ch&&g.State==SFState.Menu&&g.DataCount==0&&!g.DriveActive,"stage 3: Rio's interlude loads Al Anbar (chapter 2) fresh at its menu");
            Check(GameObject.Find("Serra pickup interlude")==null&&g.World.gameObject.activeSelf,"stage 3: no road objects survive the chapter load");
            var bosses=g.Actors.Where(a=>!a.Friendly&&a.boss).ToArray();
            Check(g.Platforms.Length==17&&ch.platforms.Length==17&&g.RouteIndex==ch.musicRoute,"stage 3: Al Anbar builds its 17 decks and music route "+ch.musicRoute);
            Check(bosses.Length==2&&g.FinalBoss!=null&&g.FinalBoss.identity=="armored_boss"&&bosses.Contains(g.FinalBoss),"stage 3: two bosses, with armored_boss (the Architect) final");
            Check(g.Selected==1&&g.Player==g.Stefanie,"stage 3: the lead carries into Al Anbar");
            g.StartGame();g.SetState(SFState.Won);
            Check(g.Notice==ch.wonNotice&&g.CourseHasNext&&ch.next<0&&g.Chapter.nextLabel=="PLAY AGAIN FROM CURITIBA  >","stage 3: Al Anbar's Won card offers 'PLAY AGAIN FROM CURITIBA  >'");
            yield return new WaitForSeconds(.5f);
            Check(g.State==SFState.Won&&g.CourseChapter==2,"stage 3: Won persists in Al Anbar");
            yield return WonCard(true,"stage 3");
            Stage=4;
            if(hud.PadMenu.Count==0){Info("stage 3: no rendered HUD, AdvanceCourse called directly");g.AdvanceCourse();yield return AwaitLoad("stage 3: AdvanceCourse wraps to Curitiba");yield break;}
            var pad=InputSystem.AddDevice<Gamepad>("SFFlowPad");pad.MakeCurrent();
            Send(pad,new GamepadState().WithButton(GamepadButton.South));Send(pad,default);
            for(int i=0;i<3;i++)yield return null;
            yield return new WaitForEndOfFrame();
            Check(g.GamepadActive&&hud.PadMenu.Focus.y==467&&hud.PadMenu.Focus.height==58,"stage 3: with a controller active, NEXT holds the focus");
            // A on the focused NEXT button: the next repainted frame consumes it and calls AdvanceCourse. The pad is removed by the next stage.
            Send(pad,new GamepadState().WithButton(GamepadButton.South));
            yield return AwaitLoad("stage 3: A on the Won card's NEXT button wraps to Curitiba");
        }

        IEnumerator CuritibaAgain()
        {
            var ch=SFCourseChapters.All[0];
            Check(g.CourseMode&&g.CourseChapter==0&&!g.InChapter&&g.State==SFState.Menu&&g.DataCount==0,"stage 4: Al Anbar's NEXT wraps to Curitiba (chapter 0) at its menu");
            Check(g.Selected==0&&g.Player==g.Fernando,"stage 4: no lead is carried into Curitiba (Fernando, as on a fresh launch)");
            Check(g.Platforms.Length==ch.platforms.Length&&g.Platforms.Select((p,i)=>Near(p.x,ch.platforms[i].x)&&Near(p.width,ch.platforms[i].width)&&Near(p.height,ch.platforms[i].height)).All(ok=>ok),"stage 4: F1 platforms match chapter 0 data");
            Check(g.Hazards.SequenceEqual(new float[]{38,75,103})&&g.HazardHeights.Length==0&&g.HazardHalf.Length==0,"stage 4: F1 hazards 38/75/103 with no hazard heights or half-widths");
            var foes=g.Actors.Where(a=>!a.Friendly).ToArray();
            Check(foes.Select(a=>a.identity).SequenceEqual(new[]{"latch","latch","keel","latch","latch","keel","latch","vesper"})&&foes.Select((a,i)=>Near(a.X,ch.cast[i].x,.05f)).All(ok=>ok),"stage 4: F1 cast latch/keel/vesper at x 24..119");
            Check(foes.Count(a=>a.boss)==1&&foes.Single(a=>a.boss).identity=="vesper"&&g.FinalBoss==null&&g.CourseCast.Count==0,"stage 4: F1 only Vesper is a boss and no chapter final boss is set");
            var renderers=g.World.GetComponentsInChildren<SpriteRenderer>(true);
            Check(Enumerable.Range(0,6).All(i=>renderers.Count(r=>r.name=="Curitiba plate "+i)==1)&&!renderers.Any(r=>r.name.StartsWith("Rio plate")||r.name.StartsWith("Anbar plate")),"stage 4: F1 plates 'Curitiba plate 0..5' and no chapter plates");
            Check(renderers.Count(r=>r.name=="Electrical current span")==3,"stage 4: F1 exactly 3 'Electrical current span' objects");
            Check(Near(g.Checkpoint,3)&&g.CheckpointHeight==0&&g.RouteIndex==0&&g.RouteName=="CURITIBA","stage 4: F1 checkpoint x3 at height 0, music route 0, HUD city CURITIBA");
            g.ShakeEnabled=false;g.StartGame();
            Check(g.State==SFState.Playing&&g.Speaker=="STEFANIE"&&g.Speech=="Three pieces of intel. One way out. Together.","stage 4: F1 intro line is Curitiba's");
            yield return new WaitForSeconds(1);
            Check(g.State==SFState.Playing&&g.Camera.transform.position.y==1f,"stage 4: F1 course camera y is exactly 1 after 1 s of play");
            g.ShakeEnabled=savedShake;
            g.SetState(SFState.Won);
            Stage=5;g.GoToChapter(99);
            Check(g.CourseChapter==SFCourseChapters.All.Length-1,"stage 4: GoToChapter(99) clamps to the last chapter");
            yield return AwaitLoad("stage 4: GoToChapter loads Al Anbar");
        }

        IEnumerator AnbarClamped()
        {
            Check(g.CourseMode&&g.CourseChapter==2&&g.InChapter&&g.State==SFState.Menu,"stage 5: GoToChapter(99) opens Al Anbar");
            Check(g.Selected==0&&g.Player==g.Fernando,"stage 5: the carried lead is Fernando's");
            yield return null;
            Stage=8;g.GoToChapter(1);
            Check(g.CourseChapter==1,"stage 5: GoToChapter(1) queues Rio");
            yield return AwaitLoad("stage 5: GoToChapter(1) loads Rio");
        }

        // Rio again: the interlude's ready card draws SKIP DRIVE in CHANGE's slot (y555), and controller A on it cuts straight to Al Anbar.
        IEnumerator RioSkip()
        {
            var ch=SFCourseChapters.All[1];
            Check(g.CourseMode&&g.CourseChapter==1&&g.InChapter&&g.Chapter==ch&&g.State==SFState.Menu&&g.Selected==0&&g.Player==g.Fernando,"stage 8: Rio reopens at its menu with Fernando carried");
            g.StartGame();g.SetState(SFState.Won);g.AdvanceCourse();
            Check(g.DriveActive&&g.Drive.JLTV&&g.Drive.Phase==SFDrivePhase.Ready&&g.CourseChapter==1,"stage 8: DESERT DRIVE opens the interlude's ready card");
            if(!g.DriveActive){Finish();yield break;}
            var pad=InputSystem.AddDevice<Gamepad>("SFFlowPad");pad.MakeCurrent();
            Send(pad,new GamepadState().WithButton(GamepadButton.South));Send(pad,default);
            Check(g.GamepadActive&&!g.Pad.WaitingForNeutral&&g.DriveActive&&g.Drive.Phase==SFDrivePhase.Ready,"stage 8: the first controller contact only arms the pad on the ready card");
            for(int i=0;i<3;i++)yield return null;
            yield return new WaitForEndOfFrame();
            var menu=hud.PadMenu;
            Stage=9;
            if(menu.Count==0){Info("stage 8: no rendered HUD, SkipCourseDrive called directly");g.SkipCourseDrive();yield return AwaitLoad("stage 8: SkipCourseDrive loads Al Anbar");yield break;}
            for(int i=0;i<6&&!(menu.Focus.y==555&&menu.Focus.height==60);i++)menu.Move(ScreenDown);
            Check(g.DriveMenuActive&&menu.Focus.y==555&&menu.Focus.height==60,"stage 8: the ready card has SKIP DRIVE in CHANGE's slot ("+menu.Count+" controls)");
            // A on the focused SKIP DRIVE button: the next repainted frame consumes it and calls SkipCourseDrive. The pad is removed by the next stage.
            Send(pad,new GamepadState().WithButton(GamepadButton.South));
            yield return AwaitLoad("stage 8: A on SKIP DRIVE cuts to Al Anbar");
        }

        IEnumerator ReturnToCampaign()
        {
            Check(g.CourseMode&&g.CourseChapter==2&&g.InChapter&&g.State==SFState.Menu&&g.DataCount==0&&!g.DriveActive,"stage 9: SKIP DRIVE loads Al Anbar fresh at its menu");
            Check(g.Selected==0&&g.Player==g.Fernando&&GameObject.Find("Serra pickup interlude")==null,"stage 9: the lead carries past the skipped drive and no road objects survive the load");
            g.StartGame();g.SetState(SFState.Won);
            yield return null;
            Stage=6;g.ChooseCourse(false);
            Check(g.CourseChapter==0,"stage 9: RETURN TO CAMPAIGN resets the chapter to 0 before the reload");
            yield return AwaitLoad("stage 9: RETURN TO CAMPAIGN loads the campaign");
        }

        void Campaign()
        {
            Check(!g.CourseMode&&g.CourseChapter==0&&!g.InChapter&&!g.CourseHasNext&&g.Chapter==SFCourseChapters.All[0],"stage 6: RETURN TO CAMPAIGN opens the campaign on chapter 0 with no NEXT");
            Check(g.HazardHeights.Length==0&&g.FinalBoss==null&&g.CheckpointHeight==0,"stage 6: the campaign carries no chapter state");
            // Stage is parked on CampaignReload so a reload here fails the run instead of looping on stage 6; case 6 restores it after the wait.
            g.SetState(SFState.Won);Stage=CampaignReload;g.AdvanceCourse();
        }

        // The campaign's road is untouched by the interlude: Serra title, BYD first, CHANGE cycles the vehicle, and both plates stay untinted.
        void CampaignRoad()
        {
            Check(g.StartDrive()&&g.DriveActive&&!g.Drive.JLTV&&g.JourneyTitle=="SERRA / ROAD TRIP"&&g.DriveExitLabel=="BACK TO MISSIONS"&&g.ConvoyVehicle=="BYD SHARK 6","stage 6: the campaign's Won card still opens the Serra road trip with the BYD");
            if(!g.DriveActive)return;
            var departure=PlateRenderer("Forest approach");var arrival=PlateRenderer("Coastal overlook");
            Check(departure!=null&&arrival!=null&&departure.sprite==Resources.Load<SFArt>("SF/plate_serra_convoy").Frame(0)&&arrival.sprite==Resources.Load<SFArt>("SF/plate_serra_overlook_hd").Frame(0)&&departure.color==Color.white&&Rgb(arrival.color,Color.white),"stage 6: the campaign road keeps the untinted Serra plates");
            g.ChangeReadyVehicle();
            Check(g.DriveActive&&g.Drive.Ram,"stage 6: the campaign's CHANGE still cycles the vehicle");
            g.EndDrive();
            Check(!g.DriveActive&&g.State==SFState.Won&&!g.CourseMode,"stage 6: leaving the campaign road returns to its Won state");
        }

        void Finish()
        {
            if(Stage==Done)return;
            Stage=Done;
            foreach(var stale in InputSystem.devices.Where(d=>d.name.StartsWith("SFFlow",StringComparison.Ordinal)).ToArray())InputSystem.RemoveDevice(stale);
            if(g!=null)g.ShakeEnabled=savedShake;
            bool pass=log.Count>0&&log.All(l=>!l.StartsWith("FAIL",StringComparison.Ordinal));
            string result="CHAPTER FLOW QA COMPLETE: "+(pass?"PASS":"FAIL");log.Add(result);Debug.Log(result);
            if(Application.platform==RuntimePlatform.WebGLPlayer)return;
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(path);
            File.WriteAllLines(Path.Combine(path,"chapter-flow.txt"),log);Application.Quit(pass?0:2);
        }
    }
}
