using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StefanieAndFernando
{
    public sealed partial class SFHUD : MonoBehaviour
    {
        public SFGame game;
        Texture2D pixel,gradient,vignette;
        Font font;
        Dictionary<string,GUIStyle> styles=new Dictionary<string,GUIStyle>();
        Color ink=new Color(.025f,.045f,.065f,.9f),white=new Color(.91f,.93f,.94f),muted=new Color(.63f,.71f,.73f);
        float width;
        void Init()
        {
            pixel=Texture2D.whiteTexture;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gradient=new Texture2D(256,1,TextureFormat.RGBA32,false);gradient.wrapMode=TextureWrapMode.Clamp;for(int x=0;x<256;x++)gradient.SetPixel(x,0,new Color(.012f,.025f,.04f,Mathf.Lerp(.98f,0,Mathf.SmoothStep(0,1,x/255f))));gradient.Apply();
            vignette=new Texture2D(128,72,TextureFormat.RGBA32,false);vignette.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<72;y++)for(int x=0;x<128;x++){float d=new Vector2((x-63.5f)/80,(y-35.5f)/48).magnitude;vignette.SetPixel(x,y,new Color(0,.015f,.035f,Mathf.Clamp01((d-.35f)*.55f)));}vignette.Apply();
        }
        GUIStyle Style(int size,Color color,bool bold=false,TextAnchor align=TextAnchor.UpperLeft)
        {
            string key=size+"_"+color+"_"+bold+"_"+align;
            if(!styles.TryGetValue(key,out var s)){s=new GUIStyle{font=font,fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,alignment=align,wordWrap=true};s.normal.textColor=color;styles[key]=s;}
            return s;
        }
        void Text(string text,float x,float y,float w,float h,int size,Color color,bool bold=false,TextAnchor align=TextAnchor.UpperLeft)=>GUI.Label(new Rect(x,y,w,h),text,Style(size,color,bold,align));
        void Box(float x,float y,float w,float h,Color color){GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),pixel);GUI.color=Color.white;}
        bool Button(string label,Rect r,bool primary=false,bool selected=false)
        {
            bool pad=game.GamepadActive;
            if(Event.current.type==EventType.Repaint)PadMenu.Add(r);
            bool focused=pad&&PadMenu.IsFocused(r);
            bool hover=r.Contains(Event.current.mousePosition)||focused;
            Box(r.x,r.y,r.width,r.height,primary?game.Gold:hover?new Color(.15f,.25f,.27f,.95f):ink);
            Box(r.x,r.y+r.height-2,r.width,2,selected||primary?game.Gold:new Color(.32f,.42f,.43f));
            Text(label,r.x+18,r.y,r.width-36,r.height,18,primary?new Color(.03f,.05f,.06f):white,true,TextAnchor.MiddleCenter);
            if(focused){Box(r.x-4,r.y-4,r.width+8,3,game.Teal);Box(r.x-4,r.y+r.height+1,r.width+8,3,game.Teal);}
            bool clicked=GUI.Button(r,GUIContent.none,GUIStyle.none);
            return clicked||(pad&&Event.current.type==EventType.Repaint&&PadMenu.Consume(r));
        }
        public void OnGUI()
        {
            if(game==null||game.Player==null)return;
            if(pixel==null)Init();
            float scale=Screen.height/900f;width=Screen.width/scale;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            bool repaint=Event.current.type==EventType.Repaint;
            if(repaint)PadMenu.Begin(game.State+"/"+game.DriveActive+"/"+(game.DriveActive?game.Drive.Phase.ToString():"")+"/"+showAudioCredits+"/"+PadHelpOpen+"/"+game.ShowHelp+"/"+game.TouchControls+"/"+game.GamepadActive);
            try
            {
            GUI.DrawTexture(new Rect(0,0,width,900),vignette);
            if(game.LayoutBlocked){Box(0,0,width,900,ink);Text("ROTATE TO LANDSCAPE",20,320,width-40,100,32,white,true,TextAnchor.MiddleCenter);Text(game.State==SFState.Menu?"Rotate to choose your mission and equipment.":"Your mission is paused. Rotate, then choose Continue.",20,440,width-40,130,22,white,false,TextAnchor.MiddleCenter);return;}
            if(PadHelpOpen){GamepadHelp();return;}
            if(showAudioCredits){AudioCredits();return;}
            if(game.DriveActive){DriveHUD();return;}
            if(game.State==SFState.Menu){Menu();return;}
            FeelCard();
            // Chapters draw the notice box under the world labels: foes on the 4.5 tier put their '!' wind-up and health bar inside its band.
            // Chapter 0's highest deck (2.4) keeps every label below it, so there DrawHUD still draws the notice last, as before.
            if(game.InChapter)NoticeBox();
            DrawWorldLabels(scale);DrawHUD();
            FeelOverlay();
            if(game.CourseMode&&game.ShowHelp&&game.State==SFState.Paused){Help(width/2-260,185);return;}
            if(game.State==SFState.Paused||game.State==SFState.Won||game.State==SFState.Lost)Overlay();
            else if(game.ShowHelp)Help(width/2-260,185);
            else if(game.TouchControls)TouchHUD();
            }finally{if(repaint)PadMenu.End();}
        }
        void Menu(){if(game.CourseMode)CourseMenu();else PartyMenu();}
        void CourseMenu()
        {
            Box(0,0,width,900,new Color(.015f,.03f,.045f,.94f));float x=width/2-350;
            // Chapters 1+ read their menu text from the chapter data. Chapter 0 keeps the literal graded strings.
            Text(game.InChapter?game.Chapter.title:"AFTER THE RAIN",x,94,700,72,46,white,true,TextAnchor.MiddleCenter);
            Text(game.InChapter?game.Chapter.subtitle:"CLASS PLATFORMER / PARTNER ADVENTURE",x,180,700,34,18,game.Gold,true,TextAnchor.MiddleCenter);
            Text(game.InChapter?game.Chapter.briefing:"Recover three pieces of intel, clear the route, and extract together.\nJump over electrical hazards and onto platforms.\nA downed hero needs a partner revive. Both down means defeat.",x,250,700,132,21,white,false,TextAnchor.MiddleCenter);
            if(Button("FERNANDO",new Rect(x,405,340,65),false,game.Selected==0))game.Selected=0;
            if(Button("STEFANIE",new Rect(x+360,405,340,65),false,game.Selected==1))game.Selected=1;
            if(Button(game.InChapter?game.Chapter.startLabel:"START PLATFORMER  >",new Rect(x,493,700,82),true))game.StartGame();
            if(Button("RETURN TO CAMPAIGN",new Rect(x,605,700,65)))game.ChooseCourse(false);
            if(Button(game.TouchControls?"DISABLE TOUCH CONTROLS":"ENABLE TOUCH CONTROLS",new Rect(x,693,340,64)))game.EnableTouch(game.TouchControls?"0":"1");
            if(Button("MUSIC & CREDITS",new Rect(x+360,693,340,64)))OpenAudioCredits();
            CourseQuitButton();
            // C9 chapter pills (SFHUDChapters.cs), drawn after every existing control so their draw order, rects and default focus are unchanged.
            ChapterSelect();
            Text(game.TouchControls?"STICK move / JUMP / INTERACT collect, revive & extract / SWITCH lead / ASSIST":game.GamepadActive?"STICK move / A jump / RT attack / D-pad UP collect & extract / RIGHT assist / LEFT switch":"A/D move / Shift run / Space jump / E collect, revive & extract / Tab switch / V assist",x-100,790,900,70,17,game.Teal,false,TextAnchor.MiddleCenter);
        }
        void DrawHUD()
        {
            Box(0,0,width,123,new Color(.015f,.03f,.045f,.88f));Box(0,122,width,1,new Color(.35f,.55f,.55f,.3f));
            float healthWidth=game.TouchControls&&width<1450?212:292,objectiveWidth=Mathf.Min(600,width-healthWidth*2-160);
            Health(game.Fernando,40,26,game.Selected==0,healthWidth);Health(game.Stefanie,width-40-healthWidth,26,game.Selected==1,healthWidth);
            Text(game.RouteName+"  /  "+(game.CourseMode?(game.InChapter?game.Chapter.title:"AFTER THE RAIN"):"MISSION "+(game.RouteIndex+1)+" / 6"),width/2-objectiveWidth/2,24,objectiveWidth,36,18,game.Gold,true,TextAnchor.MiddleCenter);
            Text(game.CourseMode?(!game.Partner.Alive?game.PartnerDownHint():"INTEL  "+game.DataCount+" / 3     |     EXTRACT TOGETHER"):game.Objective,width/2-objectiveWidth/2,66,objectiveWidth,32,15,white,false,TextAnchor.MiddleCenter);
            Box(width/2-90,105,180,2,new Color(.22f,.32f,.34f));Box(width/2-90,105,180*Mathf.Clamp01(game.Player.X/(game.InChapter?game.Chapter.extractionX:130)),2,game.Teal);
            Box(0,819,width,81,new Color(.015f,.03f,.045f,.86f));
            Text("PARTNER ASSIST",40,838,225,22,13,game.Teal,true);Box(40,867,180,4,new Color(.2f,.27f,.3f));Box(40,867,180*game.Bond/100,4,game.Teal);Box(40+180*.35f-1,864,2,10,white);
            Text(game.AssistCooldown>0?game.AssistCooldown.ToString("0.0")+"s":game.Bond<35?"CHARGING":game.TouchControls?"READY":game.GamepadActive?"D-PAD >":"V  /  READY",236,842,130,30,16,white,true);
            string mode=game.Player.Armed?(game.Player.Rifle?game.Player.RifleName:"SIDEARM")+"  ·  "+game.Player.Magazine.Loaded+" / "+game.Player.Magazine.Reserve:"UNARMED";
            if(game.TouchControls)
            {
                float cx=340,cw=width-520;
                Text(mode,cx,836,cw,27,17,white,true,TextAnchor.MiddleCenter);
                Text("WEAPON: UNARMED > PISTOL > RIFLE   /   RELOAD",cx,867,cw,22,12,muted,false,TextAnchor.MiddleCenter);
            }
            else if(game.GamepadActive)
            {
                Text("Y  "+mode+"    |    RT  "+(game.Player.Armed?"FIRE    LB  AIM":"PUNCH    X  KICK"),width/2-330,835,660,26,14,white,false,TextAnchor.MiddleCenter);
                Text("X RELOAD (ARMED)   ·   LT CROUCH   ·   RB TAKEDOWN   ·   RS GROUND",width/2-345,865,690,24,12,muted,false,TextAnchor.MiddleCenter);
                if(!game.CourseMode||game.State==SFState.Playing)Text("VIEW  CONTROLS",width-380,850,210,24,13,muted,false,TextAnchor.MiddleRight);
            }
            else
            {
                Text(mode+"    |    "+(game.Player.Armed?"LMB FIRE   RMB AIM":"LMB PUNCH   RMB KICK"),width/2-315,835,660,26,14,white,false,TextAnchor.MiddleCenter);
                Text("Q HOLSTER   ·   MMB PISTOL/RIFLE   ·   1/2 SELECT   ·   R RELOAD",width/2-335,865,690,24,12,muted,false,TextAnchor.MiddleCenter);
                if(!game.CourseMode||game.State==SFState.Playing)Text("TAB  SWITCH     H  HELP",width-400,850,230,24,13,muted,false,TextAnchor.MiddleRight); // Course cards draw QUIT GAME here.
            }
            if(game.State==SFState.Playing&&Button("PAUSE",new Rect(width-142,833,112,46)))game.SetState(SFState.Paused);
            bool live=!game.CourseMode||game.State==SFState.Playing; // Course pause/result cards: no gameplay prompts, speech or tutorial text bleeding through the dim overlay.
            if(live)Text(game.Player.CombatLabel,width/2-220,276,440,30,18,game.Gold,true,TextAnchor.MiddleCenter);
            if(game.Chain>1)Text(game.Chain+" TEAM HIT CHAIN",40,143,260,30,21,game.Gold,true);
            Text(game.PartnerIntent,40,183,280,24,12,game.Teal,true);
            if(!game.InChapter)NoticeBox();
            float messageWidth=game.TouchControls?Mathf.Min(670,width-720):670,messageX=game.TouchControls?300+(width-710-messageWidth)/2:width/2-335;
            if(live&&game.SpeechTime>0){Box(messageX,722,messageWidth,85,ink);Text(game.Speaker,messageX+12,730,messageWidth-24,20,12,game.Gold,true);Text(game.Speech,messageX+12,754,messageWidth-24,50,18,white);}
            if(live&&game.Elapsed<14&&game.GamepadActive)Text("LEFT STICK  MOVE   ·   LS HOLD  RUN   ·   A  JUMP   ·   B  EVADE   ·   VIEW  CONTROLS",width/2-460,650,920,34,16,white,false,TextAnchor.MiddleCenter);
            if(live&&game.Elapsed<14&&!game.TouchControls&&!game.GamepadActive)Text("A / D  MOVE     W / S  CHANGE LANE     SPACE  JUMP     CAPS LOCK  WALK     SHIFT  RUN     X  EVADE",width/2-460,650,920,34,16,white,false,TextAnchor.MiddleCenter);
            if(!game.CourseMode)Text(game.CoverHint(),game.TouchControls?messageX:width/2-320,595,game.TouchControls?messageWidth:640,50,14,game.Teal,true,TextAnchor.MiddleCenter);
            string hint=live?game.InteractionHint():"";if(hint.Length>0){float hw=game.TouchControls?messageWidth:400,hx=game.TouchControls?messageX:width/2-200;Box(hx,662,hw,52,ink);Text(hint,hx+10,667,hw-20,42,16,game.Gold,true,TextAnchor.MiddleCenter);}
            if(game.MotionStudyPreview)Text("F6  MOVEMENT STUDIES  /  GAIT UNDER REVIEW",width/2-270,113,540,22,12,game.Gold,false,TextAnchor.MiddleCenter);
            if(!game.TouchControls)Text((game.GamepadActive?"D-PAD DOWN  WALK ":"CAPS LOCK  WALK ")+(game.WalkMode?"ON":"OFF")+(game.GamepadActive?"  /  HOLD LS TO RUN":"  /  HOLD SHIFT TO RUN"),width-480,130,440,24,12,muted,false,TextAnchor.MiddleRight);
            if(game.Player.Aiming)
            {
                var aim=game.Camera.WorldToScreenPoint(game.Player.VisualPosition+Vector3.up*(game.Player.Crouching?1:1.7f)+Vector3.right*game.Player.facing*4);
                float ax=aim.x*900/Screen.height,ay=900-aim.y*900/Screen.height;
                Box(ax-11,ay,7,2,game.Teal);Box(ax+5,ay,7,2,game.Teal);Box(ax,ay-11,2,7,game.Teal);Box(ax,ay+5,2,7,game.Teal);
                Text("PRECISION",ax-60,ay+20,120,22,10,game.Teal,true,TextAnchor.MiddleCenter);
            }
            // Chapters can hold two bosses (Al Anbar's Cantilever and the Architect): the bar shows the nearest qualifying one. Chapter 0 and
            // the campaign keep today's first match.
            var boss=game.InChapter?game.Actors.Where(a=>a.boss&&a.Alive&&(Mathf.Abs(a.X-game.Player.X)<12||a.health<a.maxHealth&&Mathf.Abs(a.X-game.Player.X)<40)).OrderBy(a=>Mathf.Abs(a.X-game.Player.X)).FirstOrDefault()
                :game.Actors.FirstOrDefault(a=>a.boss&&a.Alive&&(Mathf.Abs(a.X-game.Player.X)<12||game.CourseMode&&a.health<a.maxHealth&&Mathf.Abs(a.X-game.Player.X)<40));
            if(boss!=null){Text(SFEnemyProfiles.Title(boss.identity),width/2-160,196,320,26,16,white,true,TextAnchor.MiddleCenter);Box(width/2-210,231,420,5,new Color(.2f,.2f,.22f));Box(width/2-210,231,420*boss.health/boss.maxHealth,5,game.Gold);}
        }
        void NoticeBox(){if(game.NoticeTime>0){Box(width/2-350,140,700,43,ink);Text(game.Notice,width/2-335,147,670,30,16,game.Teal,true,TextAnchor.MiddleCenter);}}
        void Health(SFActor actor,float x,float y,bool lead,float barWidth=292)
        {
            Text(actor.identity.ToUpperInvariant(),x,y,250,30,22,white,true);
            Text(lead?"LEAD":"PARTNER AI",x,y+32,250,22,11,lead?game.Gold:game.Teal,true);
            Box(x,y+61,barWidth,5,new Color(.22f,.27f,.3f));Box(x,y+61,barWidth*actor.health/actor.maxHealth,5,actor.health<30?new Color(.95f,.4f,.3f):game.Teal);
            if(!actor.Alive)Text(game.CourseMode?"DOWN / PARTNER REVIVE":("RECOVERING  "+actor.recovery.ToString("0")+"s"),x,y+72,292,20,11,game.Gold);
        }
        void DrawWorldLabels(float scale)
        {
            // CardLabel (SFHUDFeel) only records a label's rect while a boss card is up, so the card can keep its role line clear; it draws nothing.
            foreach(var actor in game.Actors)
            {
                if(!actor.Alive||actor.hero||Mathf.Abs(actor.X-game.Player.X)>11)continue;
                var screen=game.Camera.WorldToScreenPoint(actor.VisualPosition+Vector3.up*3.12f);float x=screen.x/scale,y=900-screen.y/scale;
                if(y<130)continue;
                CardLabel(x-85,y-38,170,48);
                bool attacking=!actor.ally&&(actor.action==SFAction.Punch||actor.action==SFAction.Shoot);
                if(attacking&&actor.actionClock<actor.attackAt)Text(actor.ranged?"SHOT IN "+(actor.attackAt-actor.actionClock).ToString("0.0")+"s":"!",x-85,y-28,170,30,18,game.Gold,true,TextAnchor.MiddleCenter);
                if(actor.HasEnemyFirearm&&actor.Reloading)
                {
                    Text("RELOADING",x-85,y-38,170,25,16,game.Teal,true,TextAnchor.MiddleCenter);
                    Box(x-38,y-9,76,4,ink);Box(x-38,y-9,76*actor.ReloadProgress,4,game.Teal);
                }
                else if(actor.EnemyOutOfAmmo&&!actor.Restrained)Text("EMPTY",x-65,y-24,130,24,14,muted,true,TextAnchor.MiddleCenter);
                Box(x-30,y+7,60,3,new Color(.1f,.1f,.12f,.85f));Box(x-30,y+7,60*actor.health/actor.maxHealth,3,actor.ally?game.Teal:game.Gold);
                if(actor.ally)Text("ALLY",x-40,y-13,80,18,10,game.Teal,true,TextAnchor.MiddleCenter);
            }
            foreach(var item in game.Pickups)
            {
                if(item.taken||Mathf.Abs(item.x-game.Player.X)>10)continue;
                var s=game.Camera.WorldToScreenPoint(item.visual.transform.position+Vector3.up*.35f);
                CardLabel(s.x/scale-70,900-s.y/scale-15,140,25);
                Text(item.kind=="data"?"INTEL":item.kind=="ammo"?"SUPPLIES":"RECOVERY",s.x/scale-70,900-s.y/scale-15,140,25,11,game.Teal,true,TextAnchor.MiddleCenter);
            }
            // Hazard labels ride on the hazard's own surface (HzH is 0 in chapter 0). Chapters show each hazard's live/idle text.
            for(int i=0;i<game.Hazards.Length;i++)
            {
                float x=game.Hazards[i];
                if(Mathf.Abs(x-game.Player.X)>10)continue;var s=game.Camera.WorldToScreenPoint(new Vector3(x,-1.6f+game.HzH(i),0));
                float hx=Mathf.Clamp(s.x/scale-125,16,width-266),hy=Mathf.Clamp(900-s.y/scale,200,600);
                Box(hx,hy,250,38,ink);CardLabel(hx,hy,250,38);
                Text(game.HazardActive?(game.InChapter?game.Chapter.hazards[i].live:"LIVE CURRENT / JUMP"):(game.InChapter?game.Chapter.hazards[i].idle:"FAULT CYCLING"),hx+8,hy+3,234,32,14,game.HazardActive?game.Gold:muted,true,TextAnchor.MiddleCenter);
            }
            if(game.InChapter){var ch=game.Chapter;if(game.Player.X>ch.extractionX-11){var s=game.Camera.WorldToScreenPoint(new Vector3(ch.extractionX,-1.9f+ch.extractionHeight,0));CardLabel(s.x/scale-100,900-s.y/scale,200,30);Text(ch.extractionLabel,s.x/scale-100,900-s.y/scale,200,30,14,game.Teal,true,TextAnchor.MiddleCenter);}}
            else if(game.CourseMode&&game.Player.X>119){var s=game.Camera.WorldToScreenPoint(new Vector3(130,-1.9f,0));Text("EXTRACTION",s.x/scale-100,900-s.y/scale,200,30,14,game.Teal,true,TextAnchor.MiddleCenter);}
        }
        void Overlay()
        {
            if(!game.CourseMode){PartyOverlay();return;}
            Box(0,0,width,900,new Color(.01f,.025f,.04f,.84f));float x=width/2-285;
            bool paused=game.State==SFState.Paused,won=game.State==SFState.Won;
            Text(paused?"TAKE A BREATH":won?(game.InChapter?game.Chapter.winTitle:"BOTH HOME."):"MISSION FAILED",x,220,570,80,50,white,true,TextAnchor.MiddleCenter);
            Text(paused?"Your partner is waiting.":won?(game.InChapter?game.Chapter.winLine:"Three pieces of intel recovered. A promise kept."):"Both heroes are down.",x,309,570,40,20,game.Teal,false,TextAnchor.MiddleCenter);
            if(!paused)Text("INTEL  "+game.DataCount+"/3     ASSISTS  "+game.Assists+"     TIME  "+Mathf.FloorToInt(game.Elapsed/60)+":"+((int)game.Elapsed%60).ToString("00"),x,373,570,30,16,muted,false,TextAnchor.MiddleCenter);
            if(paused)
            {
                Text("SOUND",x+36,382,150,25,14,muted);game.Audio.SetMasterVolume(GUI.HorizontalSlider(new Rect(x+190,389,285,18),game.Audio.Volume,0,1));
                Text("MUSIC",x+36,405,150,25,14,muted);game.Audio.SetMusicVolume(GUI.HorizontalSlider(new Rect(x+190,412,285,18),game.Audio.MusicVolume,0,.5f));
                game.ShakeEnabled=GUI.Toggle(new Rect(x+36,467,450,30),game.ShakeEnabled,"  Camera impact motion: "+(game.ShakeEnabled?"ON":"OFF"),Style(17,white));
                bool swap=GUI.Toggle(new Rect(x+36,501,470,26),game.SwapThumbs,"  Mouse side buttons: "+(game.SwapThumbs?"SWAPPED":"DEFAULT"),Style(17,white));
                if(swap!=game.SwapThumbs)game.SetThumbSwap(swap);

                if(Button("CONTINUE",new Rect(x+35,539,500,52),true))game.SetState(SFState.Playing);
                if(Button("CONTROLS",new Rect(x+35,602,game.GamepadActive?500:240,48))){if(game.GamepadActive){PadHelpOpen=true;game.Pad.ResetHeld();}else game.ShowHelp=true;}
                if(!game.GamepadActive&&Button(game.TouchControls?"TOUCH: ON":"TOUCH: OFF",new Rect(x+295,602,240,48)))game.EnableTouch(game.TouchControls?"0":"1"); // A controller immediately reclaims input, so touch cannot be toggled while it is active.
            }
            else if(!won){if(Button("RETRY FROM CHECKPOINT",new Rect(x+35,467,500,58),true))game.RetryCheckpoint();if(!game.TouchControls&&!game.GamepadActive)Text("ENTER  retry from checkpoint",x,535,570,25,14,muted,false,TextAnchor.MiddleCenter);}
            // Won: the chapter's primary NEXT button in RETRY's slot, drawn before RESTART CHAPTER so it takes the default controller focus.
            // Curitiba keeps its title, text and both buttons below it; -sfNoChapters hides it. The last chapter (next -1) wraps to Curitiba, so its hint says play again.
            else if(game.CourseHasNext){if(Button(game.Chapter.nextLabel,new Rect(x+35,467,500,58),true))game.AdvanceCourse();if(!game.TouchControls&&!game.GamepadActive)Text(game.Chapter.next<0?"ENTER  play again":"ENTER  continue",x,535,570,25,14,muted,false,TextAnchor.MiddleCenter);}
            if(Button(game.CourseMode?"RESTART CHAPTER":"LOCATION SELECT / RESET",new Rect(x+35,paused?665:610,500,48)))game.Restart();
            if(Button("RETURN TO CAMPAIGN",new Rect(x+35,paused?728:684,500,48)))game.ChooseCourse(false);
            if(paused)Text(game.TouchControls?"Tap CONTINUE to resume":game.GamepadActive?"START / B  resume":"H  controls     /     P or ESC  resume",x,795,570,25,14,muted,false,TextAnchor.MiddleCenter);
            CourseQuitButton(width-372);
        }
        // Windows course Quit needs a confirming second click within 3 s. On cards it sits clear of the in-play PAUSE button (width-142..width-30), so re-clicking PAUSE cannot quit.
        float quitArmedAt=-10;SFState quitArmedIn;
        internal void DisarmCourseQuit()=>quitArmedAt=-10;
        internal bool CourseQuitArmed(float now)=>now-quitArmedAt>=0&&now-quitArmedAt<3&&quitArmedIn==game.State;
        internal bool ConfirmCourseQuit(float now)
        {
            if(!CourseQuitArmed(now)){quitArmedAt=now;quitArmedIn=game.State;return false;}
            return now-quitArmedAt>.35f;
        }
        void CourseQuitButton(float qx=-1)
        {
            if(!game.CanQuitCourse)return;
            if(Button(CourseQuitArmed(Time.unscaledTime)?"CONFIRM QUIT":"QUIT GAME",new Rect(qx<0?width-192:qx,839,170,42))&&ConfirmCourseQuit(Time.unscaledTime))game.QuitCourse();
        }
        void Help(float x,float y)
        {
            Box(x,y,520,game.CourseMode?572:560,new Color(.02f,.045f,.065f,.96f));Text("MOVE AS ONE",x+30,y+26,460,40,28,white,true);
            string[] lines={"A/D + W/S   Move / depth lane","SPACE   Jump     CTRL (hold)   Crouch","Q   Holster / draw     MIDDLE MOUSE   Pistol / rifle","LMB   Punch / fire     RMB   Kick / aim","1   Sidearm     2   Rifle     R   Reload magazine","Approach unarmed: clinch; LMB knee; RMB throw","G / Mouse 4: takedown + six hits; move to rise","E   Take supplies / help nearby partner","V   Partner assist     TAB   Switch lead","SHIFT (hold)   Run     CAPS LOCK   Toggle walk","N   Regional backup     L   Guard     X   Evade","WHEEL   Zoom     HOME   Reset     F11   Fullscreen","ESC   Pause / thumb swap     H   Close help"};
            // Course: E's graded jobs, no backup, fixed zoom, help opens paused, and the goal as a 14th row (last row ends at y+549, inside the 572 course box; Back sits at the top).
            if(game.CourseMode){lines[7]="E   Recover intel / revive partner / extract";lines[10]="L   Guard     X   Evade";lines[11]="F11   Fullscreen     P   Pause / resume";lines[12]="H / P / ESC   Close help (stays paused)";lines=lines.Concat(new[]{game.InChapter?game.Chapter.goalRow:"GOAL   3 intel, stop Vesper, extract together"}).ToArray();}
            if(game.CourseMode&&game.TouchControls)lines=new[]{"LEFT STICK   Move / depth; push farther to run",game.InChapter?game.Chapter.touchJumpRow:"JUMP   Clear platforms and live current","PUNCH / KICK   Close combat","GUARD   Block attacks     EVADE   Dodge","WEAPON   Cycle unarmed / pistol / rifle","FIRE   Shoot     AIM   Steady your weapon","RELOAD   Refill from carried ammunition","INTERACT   Intel / free nearby revive / extract","ASSIST   35 bond; ranged revive within 7 units","SWITCH   Control the other hero","TAKEDOWN   Close grapple and six strikes","GROUND   Start ground combo / rise","PAUSE   Stop play; controls and touch options",game.InChapter?game.Chapter.goalRow:"GOAL   3 intel, stop Vesper, extract together"};
            for(int i=0;i<lines.Length;i++)Text(lines[i],x+30,y+90+i*33,470,30,15,i==7?game.Teal:i==13?game.Gold:white);
            if(game.CourseMode&&Button("BACK TO PAUSE",new Rect(x+300,y+22,190,44),true))game.ShowHelp=false; // Top right: after closing, only the pause card's title text lies under the cursor, not RETURN TO CAMPAIGN.
        }
    }
}
