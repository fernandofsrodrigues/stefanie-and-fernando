using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StefanieAndFernando
{
    // Each contact owns its original control until release; fingers cannot steal the stick.
    public sealed class SFTouchInput
    {
        readonly Dictionary<int,int> owners=new Dictionary<int,int>();
        readonly HashSet<int> live=new HashSet<int>();
        readonly List<int> released=new List<int>();
        public const int ControlCount=16;
        public Vector2 Stick {get;private set;}
        public bool Held(int control)=>owners.ContainsValue(control);
        public static Vector2 StickCenter=>new Vector2(150,694);
        public static Rect Control(int id,float width)
        {
            if(id<9)return new Rect(width-142-(id%3)*123,691-(id/3)*113,108,94);
            if(id==15)return new Rect(width-142,352,108,94);
            if(id==9)return new Rect(52,413,196,61);
            if(id==10)return new Rect(40,274,92,60);
            if(id==11)return new Rect(144,274,92,60);
            if(id==12)return new Rect(52,486,196,61);
            if(id==13)return new Rect(52,346,196,61);
            return new Rect(52,206,196,61);
        }
        public void Reset(){owners.Clear();live.Clear();Stick=Vector2.zero;}
        public SFCommand Read(SFActor actor,float width,bool driving=false)
        {
            SFCommand c=default;Stick=Vector2.zero;live.Clear();
            var screen=Touchscreen.current;if(screen==null){Reset();return c;}
            foreach(var touch in screen.touches)
            {
                bool began=touch.press.wasPressedThisFrame;
                if(!touch.press.isPressed&&!began)continue;
                int id=touch.touchId.ReadValue();live.Add(id);
                Vector2 at=touch.position.ReadValue()*900f/Screen.height;at.y=900-at.y;
                if(began&&!owners.ContainsKey(id))
                {
                    int owner=-1;
                    if(new Rect(20,560,280,245).Contains(at)&&!owners.ContainsValue(-2))owner=-2;
                    else for(int b=0;b<ControlCount;b++)if((!driving||b==0||b==2||b==6||b==8||b==13||b==15)&&((b!=14&&b!=10&&b!=11)||!actor.game.CourseMode)&&Control(b,width).Contains(at)){owner=b;break;}
                    owners[id]=owner;
                }
                if(!owners.TryGetValue(id,out int key))continue;
                if(key==-2)
                {
                    Stick=Vector2.ClampMagnitude((at-StickCenter)/90f,1);
                    if(Stick.magnitude>.16f){c.move=new Vector2(Stick.x,-Stick.y);c.run=Stick.magnitude>.82f;}
                }
                else if(driving)
                {
                    if(key==0)c.shoot=true;else if(key==2)c.crouch=true;
                    else if(key==6)c.cycleWeapon=began;else if(key==8)c.swap=began;
                    else if(key==13)c.interact=began;else if(key==15)c.reload=began;
                }
                else if(key==0){if(actor.Armed)c.shoot=true;else c.punch=true;}
                else if(key==1)c.kick=began;
                else if(key==2){if(actor.Armed)c.aim=true;else c.guard=true;}
                else if(key==3)c.jump=began;
                else if(key==4)c.dash=began;
                else if(key==5)c.grapple=began;
                else if(key==6&&began){if(actor.Armed&&actor.Rifle)c.toggleArmed=true;else c.weapon=actor.Armed?2:1;}
                else if(key==7)c.crouch=true;
                else if(key==8)c.swap=began;
                else if(key==9)c.assist=began;
                else if(key==10&&began)c.zoom=1;
                else if(key==11&&began)c.zoom=-1;
                else if(key==12)c.ground=began;
                else if(key==13)c.interact=began;
                else if(key==14)c.backup=began;
                else if(key==15)c.reload=began;
            }
            released.Clear();foreach(var id in owners.Keys)if(!live.Contains(id))released.Add(id);
            foreach(int id in released)owners.Remove(id);
            return c;
        }
    }
    public sealed partial class SFGame
    {
        static bool? selectedTouch;
        public bool TouchControls;
        internal void RestoreTouchPreference(){TouchControls=selectedTouch??false;Touch.Reset();}
        public void InitializeTouch(string value){if(!selectedTouch.HasValue)EnableTouch(value);}
        internal void RefreshControlTakeover()
        {
            // Menus own their clicks. Switching layouts on press would change the
            // toggle's value before GUI.Button receives its release.
            if(!TouchControls||GUIUtility.hotControl!=0)return;
            // The rotate screen has no interactive controls. Let a physical input
            // dismiss it while preserving the paused mission, including on desktops.
            if(!LayoutBlocked&&(State!=SFState.Playing||DriveMenuActive||ShowHelp))return;
            var k=Keyboard.current;var mouse=Mouse.current;
            bool physicalKey=k!=null&&k.anyKey.wasPressedThisFrame&&!k.f7Key.wasPressedThisFrame;
            bool contact=Touchscreen.current!=null&&Touchscreen.current.primaryTouch.press.isPressed;
            // A completed GUI click can still report wasPressedThisFrame after resume.
            // Only a currently held physical press may take the layout over.
            bool pointer=!contact&&mouse!=null&&((mouse.leftButton.wasPressedThisFrame&&mouse.leftButton.isPressed)||(mouse.rightButton.wasPressedThisFrame&&mouse.rightButton.isPressed)||(mouse.middleButton.wasPressedThisFrame&&mouse.middleButton.isPressed));
            if(physicalKey||pointer)EnableTouch("0");
        }
        public bool LayoutBlocked {get;private set;}
        public readonly SFTouchInput Touch=new SFTouchInput();
        public void EnableTouch(string value){TouchControls=value!="0";selectedTouch=TouchControls;Touch.Reset();RefreshTouchLayout(Screen.width,Screen.height);}
        internal void RefreshTouchLayout(int pixelWidth,int pixelHeight)
        {
            bool blocked=TouchControls&&pixelHeight>0&&pixelWidth*900f/pixelHeight<1100;
            if(blocked!=LayoutBlocked)Touch.Reset();
            LayoutBlocked=blocked;
            if(blocked&&State==SFState.Playing)SetState(SFState.Paused);
            // Returning to landscape leaves the game paused until the player explicitly resumes.
        }
    }
    public sealed partial class SFHUD
    {
        void TouchHUD()
        {
            var at=SFTouchInput.StickCenter;Box(at.x-100,at.y-100,200,200,new Color(.015f,.06f,.07f,.62f));
            Box(at.x-28+game.Touch.Stick.x*70,at.y-28+game.Touch.Stick.y*70,56,56,new Color(.37f,.86f,.83f,.7f));
            Text("MOVE / PUSH TO RUN",22,793,260,20,13,white,true,TextAnchor.MiddleCenter);
            string[] labels={game.Player.GroundSequence?"COMBO":game.Player.GripTarget!=null?"KNEE":game.Player.Armed?"FIRE":"PUNCH",game.Player.GripTarget!=null?"THROW":"KICK",game.Player.Armed?"AIM":"GUARD","JUMP","EVADE","TAKEDOWN","WEAPON","CROUCH","SWITCH","ASSIST","ZOOM +","ZOOM -",game.Player.GroundSequence?"RISE":"GROUND","INTERACT","BACKUP","RELOAD"};
            for(int i=0;i<labels.Length;i++)
            {
                if((i==14||i==10||i==11)&&game.CourseMode)continue; // Backup and zoom are not part of the course.
                var r=SFTouchInput.Control(i,width);bool held=game.Touch.Held(i);
                Box(r.x,r.y,r.width,r.height,held?new Color(.12f,.36f,.37f,.95f):new Color(.015f,.045f,.065f,.9f));
                Box(r.x,r.y+r.height-3,r.width,3,held?game.Gold:game.Teal);
                Text(labels[i],r.x+3,r.y,r.width-6,r.height,i==10||i==11?14:16,white,true,TextAnchor.MiddleCenter);
            }
        }
    }
}
