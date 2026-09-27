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
    // C9 chapter select (-sfChapterSelect on the desktop; RunChapterSelectQA() in a local WebGL build, e.g. a qa=chapterselect shell hook).
    // Every stage runs in a fresh scene; Stage and the log are static, so they survive the reloads.
    //   0 Campaign: no chapter select, nothing drawn, SelectChapter refused without a reload; CLASS PLATFORMER opens the course.
    //   1 Curitiba's menu: the row's layout at 4:3, 16:9, 21:9 and the narrowest touch width (inside the gutters, no overlap with any menu
    //     element or touch control, and every existing control keeps its controller targets); the rendered row with CURITIBA highlighted,
    //     FERNANDO still first in focus, touch on; human play only (with the force flag off, a SmokeMode run keeps 0.8.44's menu and refuses
    //     the select); the current pill, -1 and 3 are refused. Stefanie leads; controller A on RIO.
    //   2 Rio's menu: Rio's own title/briefing/start label, Stefanie carried, RIO highlighted; START plays Rio; no select during play;
    //     RESTART CHAPTER.
    //   3 Rio's menu again: the chapter survives the restart; Stefanie leads; key 2 (Rio itself) is refused, key 3 opens AL ANBAR.
    //   4 Al Anbar's menu and START, as stage 2; RESTART CHAPTER.
    //   5 Al Anbar's menu again; controller A on CURITIBA.
    //   6 Curitiba's menu: its literal 0.8.44 text and level, Fernando leading (no lead is carried into chapter 0); START plays Curitiba.
    // Under -sfNoChapters stage 1 instead proves the menu is 0.8.44's (no row, no extra controls, Up from FERNANDO stays put), the select
    // is refused, and START still plays Curitiba. Without a rendered HUD (-nographics) the drawn and controller parts log INFO lines and
    // the pills are chosen through SelectChapter directly. Ends with 'CHAPTER SELECT QA COMPLETE: PASS|FAIL' and QA/chapter-select.txt.
    // The pills are human play only (SFGame.ChapterSelectVisible needs !SmokeMode); this check, which runs in SmokeMode like every automated
    // run, is the one opt-in: Awake sets ChapterSelectForced on every scene it runs in. -sfNoChapters still hides them.
    public sealed class SFChapterSelectCheck:MonoBehaviour
    {
        void Awake(){var game=GetComponent<SFGame>();if(game!=null)game.ChapterSelectForced=true;}
        internal const int Done=-2,RefusedReload=90;
        // -1 idle, 0..6 the stage the next scene runs, RefusedReload while a refused select is watched (a reload then fails), Done after the result.
        internal static int Stage=-1;
        static readonly List<string> log=new List<string>();
        // RunChapterSelectQA (a local WebGL build has no -sfChapterSelect argument) arms a fresh run for the next scene load.
        static bool webRequested;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){Stage=-1;log.Clear();webRequested=false;}
        internal static bool Requested=>Stage>=0||Stage==-1&&(webRequested||Environment.GetCommandLineArgs().Contains("-sfChapterSelect"));
        internal static bool Request(){if(Stage>=0)return false;Stage=-1;log.Clear();webRequested=true;return true;}

        SFGame g;SFHUD hud;Gamepad pad;
        void Check(bool ok,string name){string line=(ok?"PASS: ":"FAIL: ")+name;log.Add(line);Debug.Log(line);}
        void Info(string text){string line="INFO: "+text;log.Add(line);Debug.Log(line);}
        static bool Near(float a,float b,float tolerance=.01f)=>Mathf.Abs(a-b)<=tolerance;
        static bool Same(Rect a,Rect b)=>Near(a.x,b.x,.05f)&&Near(a.y,b.y,.05f)&&Near(a.width,b.width,.05f)&&Near(a.height,b.height,.05f);
        static Rect Grow(Rect r,float by)=>new Rect(r.x-by,r.y-by,r.width+by*2,r.height+by*2);
        // GUI space grows downward, as in SFHUD.TickGamepadMenu (stick up moves focus toward smaller y).
        static readonly Vector2 ScreenUp=new Vector2(0,-1),ScreenDown=new Vector2(0,1);
        static readonly Vector2[] Directions={new Vector2(0,-1),new Vector2(0,1),Vector2.left,Vector2.right};
        static readonly string[] DirectionNames={"Up","Down","Left","Right"};
        static int Chapters=>SFCourseChapters.All.Length;
        // The HUD's virtual width, computed as SFHUD.OnGUI does (900 virtual units tall).
        static float Width{get{if(Screen.height<=0)return 1600;float scale=Screen.height/900f;return Screen.width/scale;}}
        bool Rendered=>hud.PadMenu.Count>0;

        // Literal copies of SFHUD.CourseMenu's controls in draw order (QUIT only in a standalone player) and its text blocks.
        static readonly string[] ControlNames={"FERNANDO","STEFANIE","START","RETURN TO CAMPAIGN","TOUCH","MUSIC & CREDITS","QUIT"};
        static Rect[] Existing(float width,bool quit)
        {
            float x=width/2-350;
            var r=new List<Rect>{new Rect(x,405,340,65),new Rect(x+360,405,340,65),new Rect(x,493,700,82),new Rect(x,605,700,65),new Rect(x,693,340,64),new Rect(x+360,693,340,64)};
            if(quit)r.Add(new Rect(width-192,839,170,42));
            return r.ToArray();
        }
        static readonly string[] TextNames={"title","subtitle","briefing","controls hint"};
        static Rect[] Texts(float width){float x=width/2-350;return new[]{new Rect(x,94,700,72),new Rect(x,180,700,34),new Rect(x,250,700,132),new Rect(x-100,790,900,70)};}
        static Rect[] Pills(float width)=>Enumerable.Range(0,Chapters).Select(i=>SFHUD.ChapterPillRect(width,i,Chapters)).ToArray();
        // 4:3, 16:9 and 21:9 at 900 virtual units tall, and 1100, the narrowest width the touch layout runs at (below it the rotate screen shows).
        static readonly (float width,string name)[] Aspects={(1200f,"4:3"),(1600f,"16:9"),(2100f,"21:9"),(1100f,"touch at its narrowest (1100)")};

        IEnumerator Start()
        {
            g=GetComponent<SFGame>();hud=GetComponent<SFHUD>();
            if(Stage<0){Stage=0;webRequested=false;log.Clear();}
            yield return new WaitForSeconds(.3f);
            Debug.Log("Chapter select QA: stage "+Stage+", chapter "+g.CourseChapter+", course "+g.CourseMode+", chapters enabled "+SFGame.ChaptersEnabled);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            RemovePads();
            switch(Stage)
            {
                case 0:yield return Campaign();break;
                case 1:yield return Curitiba();break;
                case 2:yield return Played(1,3,"stage 2");break;
                case 3:yield return Reopened(1,2,4,"stage 3",true);break;
                case 4:yield return Played(2,5,"stage 4");break;
                case 5:yield return Reopened(2,0,6,"stage 5");break;
                case 6:yield return CuritibaAgain();break;
                case RefusedReload:Check(false,"a refused chapter select reloaded the scene");Finish();break;
                default:Check(false,"unknown chapter select stage "+Stage);Finish();break;
            }
        }

        // Lets the HUD repaint (PadMenu and the pill record come from the last Repaint).
        IEnumerator Repaint(){for(int i=0;i<3;i++)yield return null;yield return new WaitForEndOfFrame();}

        // Waits for the queued scene load; if this scene is still alive after 3 s, the step failed.
        IEnumerator AwaitLoad(string step)
        {
            yield return new WaitForSecondsRealtime(3);
            Check(false,step+" (no scene load within 3 s)");Finish();
        }

        void Send(GamepadState state){InputSystem.QueueStateEvent(pad,state);InputSystem.Update();g.ProcessGamepad();}
        // A fresh virtual pad: the first contact only arms it (the menu ignores that press), then it is released to neutral.
        void ArmPad()
        {
            RemovePads();pad=InputSystem.AddDevice<Gamepad>("SFSelectPad");pad.MakeCurrent();
            Send(new GamepadState().WithButton(GamepadButton.South));Send(default);
        }
        void RemovePads(){foreach(var stale in InputSystem.devices.Where(d=>d.name.StartsWith("SFSelect",StringComparison.Ordinal)).ToArray())InputSystem.RemoveDevice(stale);pad=null;}

        // SelectChapter must neither change the chapter nor reload. Stage is parked on RefusedReload meanwhile, so a reload fails the run.
        IEnumerator RefusedSelect(int n,string name)
        {
            int stage=Stage,chapter=g.CourseChapter;Stage=RefusedReload;
            g.SelectChapter(n);
            yield return new WaitForSecondsRealtime(.6f);
            Stage=stage;
            Check(g.CourseChapter==chapter,name+" (no reload; the chapter stays "+chapter+")");
        }

        IEnumerator Campaign()
        {
            if(g.CourseMode){Info("stage 0 started in the course; loading the campaign first");g.ChooseCourse(false);yield return AwaitLoad("stage 0: ChooseCourse(false) loads the campaign");yield break;}
            Check(!g.CourseMode&&g.CourseChapter==0&&g.State==SFState.Menu&&!g.ChapterSelectVisible,"stage 0: the campaign menu offers no chapter select");
            yield return Repaint();
            if(Rendered)Check(!hud.ChapterPillsDrawn&&hud.ChapterPillHighlighted<0,"stage 0: the campaign's menu draws no chapter pills");
            else Info("stage 0: no rendered HUD frame, the campaign menu's drawing is not checked");
            yield return RefusedSelect(1,"stage 0: SelectChapter(1) is refused in the campaign");
            Stage=1;g.ChooseCourse(true);
            Check(g.CourseChapter==0,"stage 0: CLASS PLATFORMER (ChooseCourse(true)) queues the course on chapter 0");
            yield return AwaitLoad("stage 0: ChooseCourse(true) loads the course");
        }

        IEnumerator Curitiba()
        {
            if(!g.CourseMode){Check(false,"stage 1: CLASS PLATFORMER did not open the course");Finish();yield break;}
            Check(g.CourseChapter==0&&!g.InChapter&&g.State==SFState.Menu,"stage 1: the course opens on Curitiba (chapter 0) at its menu");
            if(!SFGame.ChaptersEnabled){yield return KillSwitch();yield break;}
            Check(g.ChapterSelectVisible,"stage 1: the course menu offers the chapter select");
            Check(SFCourseChapters.All.Select(c=>c.city).SequenceEqual(new[]{"CURITIBA","RIO","AL ANBAR"}),"stage 1: the pills read CURITIBA / RIO / AL ANBAR (the chapter data's cities)");
            foreach(var (width,name) in Aspects)Layout(width,name);
            MenuText(0,"stage 1");
            yield return RenderedRow(0,"stage 1");
            yield return TouchRow("stage 1");
            yield return HumanOnly("stage 1");
            yield return RefusedSelect(0,"stage 1: the highlighted CURITIBA pill (SelectChapter(0)) does nothing");
            yield return RefusedSelect(-1,"stage 1: SelectChapter(-1) is refused");
            yield return RefusedSelect(Chapters,"stage 1: SelectChapter("+Chapters+") is refused");
            g.Selected=1;
            Check(g.Player==g.Stefanie,"stage 1: Stefanie leads on Curitiba's menu");
            yield return Pick(1,2,"stage 1");
        }

        // The row against every menu element at one width. Pills are grown by their controller focus bars (4), existing controls likewise.
        // With touch on, the course menu keeps these rects (only labels change) and draws no touch controls; the row is checked against
        // the touch controls and stick anyway. Then every existing control's four controller targets are compared with and without the row.
        void Layout(float width,string aspect)
        {
            var pills=Pills(width);var caption=SFHUD.ChapterCaptionRect(width,Chapters);
            var row=pills.Select(p=>Grow(p,4)).Concat(new[]{caption}).ToArray();
            var bad=new List<string>();
            foreach(var r in row)if(r.xMin<16||r.xMax>width-16||r.yMin<16)bad.Add("outside the 16 gutter "+r);
            for(int i=0;i<row.Length;i++)for(int j=i+1;j<row.Length;j++)if(row[i].Overlaps(row[j]))bad.Add("row items "+i+" and "+j+" overlap");
            var controls=Existing(width,true);
            for(int k=0;k<controls.Length;k++)if(row.Any(r=>r.Overlaps(Grow(controls[k],4))))bad.Add("overlaps "+ControlNames[k]);
            var texts=Texts(width);
            for(int k=0;k<texts.Length;k++)if(row.Any(r=>r.Overlaps(texts[k])))bad.Add("overlaps the "+TextNames[k]);
            for(int id=0;id<SFTouchInput.ControlCount;id++)if(row.Any(r=>r.Overlaps(SFTouchInput.Control(id,width))))bad.Add("overlaps touch control "+id);
            var stick=new Rect(SFTouchInput.StickCenter.x-100,SFTouchInput.StickCenter.y-100,200,200);if(row.Any(r=>r.Overlaps(stick)))bad.Add("overlaps the touch stick");
            Check(bad.Count==0,aspect+": the chapter row ("+pills[0].xMin.ToString("0")+".."+pills[pills.Length-1].xMax.ToString("0")+" x, y "+pills[0].yMin.ToString("0")+".."+pills[0].yMax.ToString("0")+") stays inside the 16 gutter and clear of every menu control, text block and touch control"+(bad.Count>0?": "+string.Join("; ",bad):""));
            foreach(bool quit in new[]{false,true})
            {
                var ex=Existing(width,quit);var with=ex.Concat(pills).ToArray();var changed=new List<string>();
                for(int k=0;k<ex.Length;k++)for(int d=0;d<Directions.Length;d++)
                {
                    var before=Target(ex[k],ex,Directions[d]);var after=Target(ex[k],with,Directions[d]);
                    // A control that had a target keeps it; one that had none may now reach the row.
                    bool ok=before!=ex[k]?after==before:after==ex[k]||pills.Contains(after);
                    if(!ok)changed.Add(ControlNames[k]+" "+DirectionNames[d]+" now reaches "+Name(after,ex,pills));
                }
                bool reach=Target(ex[0],with,ScreenUp)==pills[0]&&pills.Contains(Target(ex[1],with,ScreenUp));
                for(int i=0;i+1<pills.Length;i++)reach&=Target(pills[i],with,Vector2.right)==pills[i+1]&&Target(pills[i+1],with,Vector2.left)==pills[i];
                for(int i=0;i<pills.Length;i++){var down=Target(pills[i],with,ScreenDown);reach&=down==ex[0]||down==ex[1];}
                Check(changed.Count==0&&reach,aspect+(quit?" with QUIT":"")+": every existing control keeps its controller targets; Up from FERNANDO and STEFANIE reaches the row, Left/Right walk it and Down returns to the hero buttons"+(changed.Count>0?": "+string.Join("; ",changed):""));
            }
        }
        // The control SFMenuNavigation focuses after one move from 'from' (added first so it holds the focus; the rest keep their draw order).
        static Rect Target(Rect from,IList<Rect> controls,Vector2 direction)
        {
            var nav=new SFMenuNavigation();nav.Begin("chapter select check");nav.Add(from);foreach(var r in controls)nav.Add(r);nav.End();
            nav.Move(direction);return nav.Focus;
        }
        static string Name(Rect r,Rect[] ex,Rect[] pills)
        {
            int k=Array.IndexOf(ex,r);if(k>=0)return ControlNames[k];
            int p=Array.IndexOf(pills,r);return p>=0?SFCourseChapters.All[p].city+" pill":r.ToString();
        }

        // What CourseMenu draws: chapters 1+ read their title, subtitle, briefing and start label from the chapter data; Curitiba
        // (InChapter false) keeps its literal 0.8.44 strings, which equal chapter 0's data (fixture F1).
        static readonly string[] BriefingStart={"Recover three pieces of intel, clear the route","Recover three pieces of intel on the climb","Climb to the Iron Meridian's"};
        void MenuText(int n,string tag)
        {
            var ch=SFCourseChapters.All[n];string start=n==0?"START PLATFORMER  >":"START CHAPTER "+(n+1)+"  >";
            bool source=n==0?!g.InChapter&&ch.title=="AFTER THE RAIN"&&ch.subtitle=="CLASS PLATFORMER / PARTNER ADVENTURE":g.InChapter&&g.Chapter==ch&&!string.IsNullOrEmpty(ch.title)&&!string.IsNullOrEmpty(ch.subtitle);
            bool briefing=n<BriefingStart.Length&&ch.briefing.StartsWith(BriefingStart[n],StringComparison.Ordinal)&&SFCourseChapters.All.Count(c=>c.briefing==ch.briefing)==1;
            Check(source&&briefing&&ch.startLabel==start,tag+": the menu shows "+ch.city+"'s own title '"+ch.title+"', briefing ('"+(n<BriefingStart.Length?BriefingStart[n]:"?")+"...') and '"+start+"'");
        }

        // The drawn row from the last repaint, and the controller walk over it from the default focus (FERNANDO).
        IEnumerator RenderedRow(int current,string tag)
        {
            yield return Repaint();
            if(!Rendered){Info(tag+": no rendered HUD frame, the drawn row is not checked");yield break;}
            float w=Width;var menu=hud.PadMenu;var ex=Existing(w,g.CanQuitCourse);var pills=Pills(w);
            Check(hud.ChapterPillsDrawn&&hud.ChapterPillHighlighted==current,tag+": the menu draws the chapter pills with "+SFCourseChapters.All[current].city+" highlighted");
            Check(menu.Count==ex.Length+pills.Length&&Same(menu.Focus,ex[0]),tag+": the pills come after the menu's "+ex.Length+" controls ("+menu.Count+" in all) and FERNANDO keeps the default controller focus");
            menu.Move(ScreenUp);bool row=Same(menu.Focus,pills[0]);
            for(int i=1;i<pills.Length;i++){menu.Move(Vector2.right);row&=Same(menu.Focus,pills[i]);}
            menu.Move(ScreenDown);var down=menu.Focus;menu.Move(Vector2.left);var back=menu.Focus;
            Check(row&&Same(down,ex[1])&&Same(back,ex[0]),tag+": controller Up from FERNANDO reaches CURITIBA, Right walks the row to AL ANBAR, Down returns to STEFANIE and Left to FERNANDO");
            menu.Move(ScreenDown);var touch=menu.Focus;menu.Move(Vector2.right);var music=menu.Focus;menu.Move(ScreenUp);var stefanie=menu.Focus;menu.Move(Vector2.left);
            Check(Same(touch,ex[4])&&Same(music,ex[5])&&Same(stefanie,ex[1])&&Same(menu.Focus,ex[0]),tag+": the rendered FERNANDO, STEFANIE, TOUCH and MUSIC & CREDITS keep their rects and controller targets");
        }

        IEnumerator TouchRow(string tag)
        {
            bool touch=g.TouchControls;g.EnableTouch("1");
            yield return Repaint();
            if(g.LayoutBlocked)Info(tag+": this window is narrower than the touch layout (rotate screen), the touch menu is not rendered");
            else if(!Rendered)Info(tag+": no rendered HUD frame, the touch menu is not checked");
            else{var ex=Existing(Width,g.CanQuitCourse);Check(g.TouchControls&&hud.ChapterPillsDrawn&&hud.PadMenu.Count==ex.Length+Chapters&&Same(hud.PadMenu.Focus,ex[0]),tag+": with touch controls on, the menu draws the same row after its "+ex.Length+" controls");}
            g.EnableTouch(touch?"1":"0");
            yield return Repaint();
        }

        // Chooses chapter n's pill: controller A on it when the HUD renders (Up from FERNANDO, then Right along the row), else SelectChapter.
        IEnumerator Pick(int n,int next,string tag)
        {
            string city=SFCourseChapters.All[n].city;int from=g.CourseChapter;
            yield return Repaint();
            Stage=next;
            if(!Rendered)
            {
                Info(tag+": no rendered HUD, SelectChapter("+n+") called directly");
                g.SelectChapter(n);Check(g.CourseChapter==n,tag+": the "+city+" pill queues chapter "+n);
                yield return AwaitLoad(tag+": the "+city+" pill loads chapter "+n);yield break;
            }
            ArmPad();
            Check(g.GamepadActive&&!g.Pad.WaitingForNeutral&&g.State==SFState.Menu&&g.CourseChapter==from,tag+": the first controller contact only arms the pad on the menu");
            yield return Repaint();
            var menu=hud.PadMenu;
            menu.Move(ScreenUp);for(int i=0;i<n;i++)menu.Move(Vector2.right);
            Check(Same(menu.Focus,SFHUD.ChapterPillRect(Width,n,Chapters)),tag+": the controller reaches the "+city+" pill (Up from FERNANDO, then Right)");
            // A on the focused pill: the next repainted frame consumes it and calls SelectChapter. The pad is removed by the next stage.
            Send(new GamepadState().WithButton(GamepadButton.South));
            yield return AwaitLoad(tag+": A on the "+city+" pill loads chapter "+n);
        }

        // START on a chapter's menu through controller Start (StartGame, like the START button and Enter), then what is playing.
        void PressStart(int n,string tag)
        {
            ArmPad();
            Send(new GamepadState().WithButton(GamepadButton.Start));Send(default);
            RemovePads();
            var ch=SFCourseChapters.All[n];
            string speaker=n==0?"STEFANIE":ch.introSpeaker,line=n==0?"Three pieces of intel. One way out. Together.":ch.intro;
            Check(g.State==SFState.Playing&&g.CourseChapter==n&&g.InChapter==(n>0)&&g.Speaker==speaker&&g.Speech==line,tag+": START plays "+ch.city+" with its intro line ('"+line+"')");
            Check(Near(g.Player.X,ch.startX,.3f)&&Near(g.Partner.X,ch.startX-1.5f,.3f)&&Mathf.Abs(g.Player.Height-ch.startHeight)<.5f,tag+": both heroes start at "+ch.city+"'s start (x "+ch.startX+", height "+ch.startHeight+")");
            var others=SFCourseChapters.All.Where((c,i)=>i!=n).Select(c=>c.plateName).ToArray();
            var renderers=g.World.GetComponentsInChildren<SpriteRenderer>(true);
            var foes=g.Actors.Where(a=>!a.Friendly).ToArray();var final=ch.cast.FirstOrDefault(e=>e.final);
            bool boss=n==0?g.FinalBoss==null&&foes.Count(a=>a.boss)==1&&foes.Single(a=>a.boss).identity=="vesper":g.FinalBoss!=null&&g.FinalBoss.identity==final.id;
            Check(g.Platforms.Length==ch.platforms.Length&&g.Hazards.Length==ch.hazards.Length&&foes.Length==ch.cast.Length&&g.RouteIndex==ch.musicRoute&&boss&&!renderers.Any(r=>others.Any(o=>r.name.StartsWith(o,StringComparison.Ordinal))),tag+": the level in play is "+ch.city+"'s ("+ch.platforms.Length+" decks, "+ch.hazards.Length+" hazards, "+ch.cast.Length+" enemies, music route "+ch.musicRoute+", no other chapter's plates)");
        }

        IEnumerator Played(int n,int next,string tag)
        {
            var ch=SFCourseChapters.All[n];
            Check(g.CourseMode&&g.CourseChapter==n&&g.InChapter&&g.Chapter==ch&&g.State==SFState.Menu&&g.DataCount==0&&g.ChapterSelectVisible,tag+": the "+ch.city+" pill opens "+ch.city+" (chapter "+n+") fresh at its own menu");
            MenuText(n,tag);
            Check(g.Selected==1&&g.Player==g.Stefanie,tag+": the lead carries into "+ch.city+" (Stefanie preselected), as through the Won card");
            yield return RenderedRow(n,tag);
            PressStart(n,tag);
            Check(!g.ChapterSelectVisible,tag+": no chapter select during play");
            yield return Repaint();
            if(Rendered)Check(!hud.ChapterPillsDrawn,tag+": the pills are not drawn during play");
            else Info(tag+": no rendered HUD frame, the in-play row is not checked");
            yield return RefusedSelect(n==2?1:2,tag+": SelectChapter during play is refused");
            Stage=next;g.SetState(SFState.Paused);g.Restart();
            yield return AwaitLoad(tag+": RESTART CHAPTER reloads "+ch.city);
        }

        IEnumerator Reopened(int n,int pick,int next,string tag,bool keys=false)
        {
            var ch=SFCourseChapters.All[n];
            Check(g.CourseMode&&g.CourseChapter==n&&g.InChapter&&g.State==SFState.Menu&&g.DataCount==0&&g.ChapterSelectVisible,tag+": RESTART CHAPTER reopens "+ch.city+" at its menu with the chapter select");
            MenuText(n,tag);
            yield return RenderedRow(n,tag);
            g.Selected=1;
            yield return keys?KeyPick(pick,next,tag):Pick(pick,next,tag);
        }

        // The pills are human play only. The same menu with the force flag off (SmokeMode, as in every other automated run) offers no select:
        // nothing is drawn or registered with PadMenu, controller Up from FERNANDO stays put as in 0.8.44, and SelectChapter is refused.
        // Human play (SmokeMode off) is read from the property alone, so no frame runs with SmokeMode off. The flag is restored afterwards.
        IEnumerator HumanOnly(string tag)
        {
            g.ChapterSelectForced=false;
            g.SmokeMode=false;bool human=g.ChapterSelectVisible;g.SmokeMode=true;
            Check(human&&!g.ChapterSelectVisible,tag+": the chapter select is offered in human play and hidden in an automated (SmokeMode) run");
            yield return Repaint();
            if(Rendered)
            {
                var ex=Existing(Width,g.CanQuitCourse);var menu=hud.PadMenu;
                Check(!hud.ChapterPillsDrawn&&menu.Count==ex.Length&&Same(menu.Focus,ex[0]),tag+": in a SmokeMode run no pills are drawn; the menu keeps its "+ex.Length+" controls with FERNANDO focused");
                menu.Move(ScreenUp);
                Check(Same(menu.Focus,ex[0]),tag+": in a SmokeMode run controller Up from FERNANDO stays put, as in 0.8.44");
            }
            else Info(tag+": no rendered HUD frame, the SmokeMode menu is not checked");
            yield return RefusedSelect(1,tag+": SelectChapter(1) is refused in a SmokeMode run");
            g.ChapterSelectForced=true;
            yield return Repaint();
            Check(g.ChapterSelectVisible,tag+": the check's force flag restores the chapter select");
            if(Rendered)Check(hud.ChapterPillsDrawn&&Same(hud.PadMenu.Focus,Existing(Width,g.CanQuitCourse)[0]),tag+": the check's force flag brings the row back with FERNANDO still focused");
            else Info(tag+": no rendered HUD frame, the restored row is not checked");
        }

        // Chooses chapter n with its number key, as a keyboard player on the menu. SFGame.Update reads keys only in human play (outside
        // SmokeMode), so the check hands a virtual keyboard to SFGame.ChapterSelectKeys, the call Update makes. The current chapter's key is
        // refused first (no reload), then chapter n's key loads it. The keyboard is named SFSelect* so the next stage removes it.
        IEnumerator KeyPick(int n,int next,string tag)
        {
            string city=SFCourseChapters.All[n].city;int from=g.CourseChapter;
            RemovePads();var keys=InputSystem.AddDevice<Keyboard>("SFSelectKeys");
            Key[] digits={Key.Digit1,Key.Digit2,Key.Digit3};
            int stage=Stage;Stage=RefusedReload;
            InputSystem.QueueStateEvent(keys,new KeyboardState(digits[from]));InputSystem.Update();g.ChapterSelectKeys(keys);
            InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.Update();
            yield return new WaitForSecondsRealtime(.6f);
            Stage=stage;
            Check(g.CourseChapter==from&&g.State==SFState.Menu,tag+": key "+(from+1)+" ("+SFCourseChapters.All[from].city+", the current chapter) does nothing (no reload)");
            Stage=next;
            InputSystem.QueueStateEvent(keys,new KeyboardState(digits[n]));InputSystem.Update();g.ChapterSelectKeys(keys);
            Check(g.CourseChapter==n,tag+": key "+(n+1)+" queues "+city+" (chapter "+n+"), as its pill does");
            yield return AwaitLoad(tag+": key "+(n+1)+" loads "+city);
        }

        IEnumerator CuritibaAgain()
        {
            Check(g.CourseMode&&g.CourseChapter==0&&!g.InChapter&&g.State==SFState.Menu&&g.DataCount==0&&g.ChapterSelectVisible,"stage 6: the CURITIBA pill opens Curitiba (chapter 0) fresh at its menu");
            MenuText(0,"stage 6");
            Check(g.Selected==0&&g.Player==g.Fernando,"stage 6: no lead is carried into Curitiba (Fernando, as on a fresh launch and through the Won card)");
            Check(g.Hazards.SequenceEqual(new float[]{38,75,103})&&g.HazardHeights.Length==0&&g.HazardHalf.Length==0&&g.FinalBoss==null&&g.CourseCast.Count==0&&Near(g.Checkpoint,3)&&g.CheckpointHeight==0,"stage 6: Curitiba's literal level (hazards 38/75/103, checkpoint x3, no chapter state)");
            yield return RenderedRow(0,"stage 6");
            PressStart(0,"stage 6");
            Finish();
        }

        IEnumerator KillSwitch()
        {
            Check(!g.ChapterSelectVisible,"-sfNoChapters: the course menu offers no chapter select");
            yield return Repaint();
            if(Rendered)
            {
                var ex=Existing(Width,g.CanQuitCourse);var menu=hud.PadMenu;
                Check(!hud.ChapterPillsDrawn&&menu.Count==ex.Length&&Same(menu.Focus,ex[0]),"-sfNoChapters: no pills are drawn; the menu keeps its "+ex.Length+" controls with FERNANDO focused");
                menu.Move(ScreenUp);
                Check(Same(menu.Focus,ex[0]),"-sfNoChapters: controller Up from FERNANDO stays put, as in 0.8.44");
            }
            else Info("-sfNoChapters: no rendered HUD frame, the drawn menu is not checked");
            yield return RefusedSelect(1,"-sfNoChapters: SelectChapter(1) is refused on Curitiba's menu");
            PressStart(0,"-sfNoChapters");
            Finish();
        }

        void Finish()
        {
            if(Stage==Done)return;
            Stage=Done;RemovePads();
            bool pass=log.Count>0&&log.All(l=>!l.StartsWith("FAIL",StringComparison.Ordinal));
            string result="CHAPTER SELECT QA COMPLETE: "+(pass?"PASS":"FAIL");log.Add(result);Debug.Log(result);
            if(Application.platform==RuntimePlatform.WebGLPlayer)return;
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(path);
            File.WriteAllLines(Path.Combine(path,"chapter-select.txt"),log);Application.Quit(pass?0:2);
        }
    }
}
