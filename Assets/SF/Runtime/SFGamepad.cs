using UnityEngine;
using UnityEngine.InputSystem;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        public readonly SFGamepadInput Pad=new SFGamepadInput();
        public bool GamepadActive=>Pad.Active;
        public void RunGamepadQA(){if(Debug.isDebugBuild&&GetComponent<SFGamepadCheck>()==null){SmokeMode=true;EnableTouch("0");gameObject.AddComponent<SFGamepadCheck>();}}
        internal void ProcessGamepad()
        {
            bool wasActive=Pad.Active;
            if(Pad.Poll()){PauseForInterruption();Notify("CONTROLLER DISCONNECTED  /  MISSION PAUSED");}
            if(wasActive&&!Pad.Active&&Touchscreen.current!=null&&Touchscreen.current.primaryTouch.press.wasPressedThisFrame)EnableTouch("1");
            if(!Pad.Active)return;
            if(TouchControls)EnableTouch("0");
            if(Pad.WaitingForNeutral)return;
            var p=Pad.Device;
            if(hud.AudioCreditsOpen)
            {
                if(p.buttonEast.wasPressedThisFrame||p.startButton.wasPressedThisFrame){hud.CloseAudioCredits();Pad.ResetHeld();return;}
                hud.TickGamepadMenu();return;
            }
            if(hud.PadHelpOpen)
            {
                if(p.buttonEast.wasPressedThisFrame||p.startButton.wasPressedThisFrame||p.selectButton.wasPressedThisFrame){hud.PadHelpOpen=false;Pad.ResetHeld();return;}
                hud.TickGamepadMenu();return;
            }
            if(p.selectButton.wasPressedThisFrame)
            {
                if(State==SFState.Playing)SetState(SFState.Paused);
                if(!DriveActive)hud.PadHelpOpen=true;Pad.ResetHeld();return;
            }
            if(p.startButton.wasPressedThisFrame)
            {
                if(State==SFState.Playing)SetState(SFState.Paused);
                else if(State==SFState.Paused)SetState(SFState.Playing);
                else if(State==SFState.Menu)StartGame();
                // Course Won card: Start matches the primary NEXT button. CourseHasNext is false in the campaign and under -sfNoChapters.
                else if(State==SFState.Won&&CourseHasNext)AdvanceCourse();
                return;
            }
            if(State!=SFState.Playing||DriveMenuActive)
            {
                if(State==SFState.Paused&&p.buttonEast.wasPressedThisFrame){SetState(SFState.Playing);return;}
                hud.TickGamepadMenu();
            }
        }
    }
    public sealed partial class SFHUD
    {
        public readonly SFMenuNavigation PadMenu=new SFMenuNavigation();
        public bool PadHelpOpen;
        Vector2 menuDirection;float menuRepeat;
        internal void TickGamepadMenu()
        {
            var p=game.Pad.Device;Vector2 at=p.dpad.ReadValue();if(at.sqrMagnitude<.1f)at=p.leftStick.ReadValue();
            Vector2 direction=at.magnitude<.5f?Vector2.zero:Mathf.Abs(at.x)>Mathf.Abs(at.y)?new Vector2(Mathf.Sign(at.x),0):new Vector2(0,-Mathf.Sign(at.y));
            if(direction==Vector2.zero){menuDirection=direction;menuRepeat=0;}
            else if(direction!=menuDirection||Time.unscaledTime>=menuRepeat)
            {
                bool first=direction!=menuDirection;PadMenu.Move(direction);menuDirection=direction;menuRepeat=Time.unscaledTime+(first?.4f:.14f);
            }
            if(p.buttonSouth.wasPressedThisFrame)PadMenu.Activate();
        }
        void GamepadHelp()
        {
            Box(0,0,width,900,ink);float x=width/2-470;
            Text("CONTROLLER  /  MOVE AS ONE",x,68,940,55,34,white,true);
            string[] lines={"LEFT STICK   Move / depth     |     Hold LS click to run","RT   Punch / knee / fire     |     X (left face)   Kick / throw / armed reload","A (bottom face)   Jump     |     B (right face)   Evade","Y (top face)   Cycle unarmed > pistol > rifle","LB   Guard / aim     |     LT   Crouch     |     RB   Takedown","RIGHT STICK   Aim left/right; up/down zoom     |     RS click   Ground","D-PAD UP   Interact     |     LB + UP   Call backup","D-PAD LEFT   Switch lead     |     RIGHT   Partner assist","D-PAD DOWN   Toggle walk     |     START / MENU   Pause","VIEW / SELECT   Open this guide (pauses the mission)","Menus: stick / D-pad to focus, A to select, B to return.","MR762A1: release RT, then press again for each shot."};
            if(game.CourseMode){lines[5]="RIGHT STICK   Aim left/right     |     RS click   Ground";lines[6]="D-PAD UP   Recover intel / revive partner / extract";} // No backup or zoom in the course.
            for(int i=0;i<lines.Length;i++)Text(lines[i],x,151+i*43,940,38,20,i==11?game.Teal:white);
            if(Button("BACK",new Rect(x,game.CourseMode?782:746,940,game.CourseMode?54:62),true)){PadHelpOpen=false;game.Pad.ResetHeld();} // Course: below the pause card's RETURN TO CAMPAIGN (ends y776), so a double-click cannot leave the course.
            Text("Standard Xbox positions shown. Other controller labels may differ.",x,game.CourseMode?848:837,940,30,16,muted);
        }
    }
}
