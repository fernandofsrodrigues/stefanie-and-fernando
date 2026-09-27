using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StefanieAndFernando
{
    // Virtual-device integration coverage; it does not claim a physical controller test.
    public sealed class SFGamepadCheck:MonoBehaviour
    {
        readonly List<string> results=new List<string>();SFGame g;SFHUD hud;Gamepad pad;Mouse mouse;Keyboard keyboard;string qa;
        void Check(bool ok,string name){results.Add((ok?"PASS: ":"FAIL: ")+name);Debug.Log(results.Last());}
        void Queue(GamepadState state){InputSystem.QueueStateEvent(pad,state);InputSystem.Update();g.ProcessGamepad();}
        IEnumerator Frame(GamepadState state){Queue(state);yield return new WaitForSeconds(.12f);}
        bool RenderedUI=>Application.platform==RuntimePlatform.WebGLPlayer;
        IEnumerator Capture(string name){Debug.Log("PAD QA VIEW: "+name);yield return new WaitForSeconds(.2f);}
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();hud=GetComponent<SFHUD>();yield return null;
            qa=RenderedUI?"/sf-qa":Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Gamepad-v0846"));Directory.CreateDirectory(qa);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();pad=InputSystem.AddDevice<Gamepad>();
            keyboard.MakeCurrent();mouse.MakeCurrent();pad.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(0,0)});
            yield return Frame(default);
            Debug.Log("Pad diagnostics: neutral="+!g.Pad.WaitingForNeutral+" active="+g.GamepadActive+" controls="+hud.PadMenu.Count+" focus="+hud.PadMenu.Focus);
            Check(!g.GamepadActive,"idle connected controller leaves keyboard mode unchanged");
            var nav=new SFMenuNavigation();var left=new Rect(0,0,100,40);var right=new Rect(120,0,100,40);
            nav.Begin("keyboard");nav.Add(left);nav.Add(right);nav.End();nav.Begin("pad");nav.Add(left);nav.Add(right);nav.End();nav.Move(Vector2.right);nav.Activate();
            Check(nav.IsFocused(right)&&nav.Consume(right),"switching input context restores focus even when menu rectangles are unchanged");
            Check(!nav.Consume(right),"one menu confirmation is consumed only once across GUI events");
            nav.Activate();nav.Begin("pause");nav.Add(right);nav.End();Check(!nav.Consume(right),"pending confirmation cannot leak into another menu context");
            yield return Frame(new GamepadState().WithButton(GamepadButton.South));
            Check(g.GamepadActive&&g.Pad.WaitingForNeutral&&g.State==SFState.Menu,"first controller contact switches prompts without accidental menu activation");
            yield return Frame(default);
            Check(!g.Pad.WaitingForNeutral,"neutral release arms controller");
            if(RenderedUI)Check(hud.PadMenu.Count>=18,"rendered loadout discovers all controller menu controls");
            yield return Frame(new GamepadState().WithButton(GamepadButton.DpadRight));yield return Frame(default);
            yield return Frame(new GamepadState().WithButton(GamepadButton.South));
            if(RenderedUI)Check(g.Selected==1&&g.State==SFState.Menu,"D-pad and A select Stefanie in the actual rendered menu");
            else g.Selected=1;
            yield return Frame(default);
            yield return Frame(new GamepadState().WithButton(GamepadButton.DpadDown));yield return Frame(default);
            yield return Frame(new GamepadState().WithButton(GamepadButton.South));
            if(RenderedUI)Check(g.Loadouts[1].outfit==1,"spatial focus reaches Stefanie's outfit control");
            else g.Loadouts[1].outfit=1;
            yield return Frame(default);yield return Capture("loadout-focus");
            yield return Frame(new GamepadState().WithButton(GamepadButton.Select));
            Check(hud.PadHelpOpen&&g.State==SFState.Menu,"View opens controller guide from loadout");
            yield return Frame(default);yield return Capture("controller-guide");
            yield return Frame(new GamepadState().WithButton(GamepadButton.East));
            Check(!hud.PadHelpOpen&&g.State==SFState.Menu,"B returns from guide without starting a mission");
            yield return Frame(default);yield return Frame(new GamepadState().WithButton(GamepadButton.Start));
            Check(g.State==SFState.Playing&&g.Player.identity=="stefanie"&&g.Player.FieldUniform,"Start launches selected loadout with no mouse");
            Check(g.Pad.WaitingForNeutral&&g.ReadCommand().move==Vector2.zero,"mission transition suppresses held controls until neutral");
            yield return Frame(default);
            Queue(new GamepadState{leftStick=new Vector2(.04f,-.03f)});
            Check(g.ReadCommand().move==Vector2.zero,"small stick drift produces no movement");
            Queue(new GamepadState{leftStick=new Vector2(.65f,.5f)});
            var c=g.ReadCommand();Check(c.move.x>.4f&&c.move.x<1&&c.move.y>.25f&&c.move.magnitude<=1,"analog stick preserves partial diagonal movement and lane input");
            Queue(default);Queue(new GamepadState{leftStick=Vector2.right}.WithButton(GamepadButton.LeftStick));
            c=g.ReadCommand();Check(c.run&&c.move.x>.99f,"left-stick click holds running");
            var jog=c;jog.run=false;g.SmokeCommand=jog;yield return null;yield return null;
            for(int tick=0;tick<20;tick++)yield return new WaitForFixedUpdate();
            float jogSpeed=g.Player.motor.Body.linearVelocity.x;g.SmokeCommand=c;yield return null;yield return null;
            for(int tick=0;tick<20;tick++)yield return new WaitForFixedUpdate();
            Debug.Log("Pad motor diagnostics: state="+g.State+" action="+g.Player.action+" speed="+g.Player.motor.Body.linearVelocity.x+" run="+c.run+" move="+c.move+" ground="+g.Player.motor.IsGrounded+" help="+hud.PadHelpOpen+" selected="+g.Selected);
            Check(g.Player.motor.Body.linearVelocity.x>4.7f&&g.Player.motor.Body.linearVelocity.x>jogSpeed+1,"controller run command drives the live movement motor faster than jog");g.SmokeCommand=default;
            Queue(default);Queue(new GamepadState().WithButton(GamepadButton.DpadDown));c=g.ReadCommand();g.ApplyGait(ref c);
            Check(g.WalkMode&&c.walk,"D-pad down toggles walk mode");
            InputSystem.Update();g.ProcessGamepad();Check(!g.ReadCommand().toggleWalk,"holding D-pad down cannot repeatedly toggle walking");
            Queue(default);Queue(new GamepadState().WithButton(GamepadButton.North));c=g.ReadCommand();g.Player.Tick(.016f,c);
            Check(g.Player.Armed&&!g.Player.Rifle,"Y equips sidearm from unarmed");
            Queue(default);Queue(new GamepadState().WithButton(GamepadButton.North));g.Player.Tick(.016f,g.ReadCommand());
            Check(g.Player.Armed&&g.Player.Rifle,"second Y equips hero rifle");
            Queue(default);Queue(new GamepadState().WithButton(GamepadButton.North));g.Player.Tick(.016f,g.ReadCommand());
            Check(!g.Player.Armed,"third Y returns to unarmed");
            Queue(default);Queue(new GamepadState{rightTrigger=1}.WithButton(GamepadButton.West).WithButton(GamepadButton.LeftShoulder));c=g.ReadCommand();
            Check(c.punch&&c.kick&&c.guard&&!c.shoot&&!c.aim,"unarmed RT / X / LB route to punch, kick and guard");
            c=g.Pad.Read(true,true,true,.016f);Check(c.punch&&c.kick&&!c.shoot,"held enemy routes primary and kick to knee / throw even if previously armed");
            g.Player.Armed=true;Queue(default);Queue(new GamepadState{rightTrigger=1,rightStick=new Vector2(-1,0)}.WithButton(GamepadButton.LeftShoulder));c=g.ReadCommand();
            Check(c.shoot&&c.aim&&c.aimFacing==-1&&!c.punch,"armed RT fires and right stick aims left independently of parked mouse cursor");
            Queue(default);Queue(new GamepadState{rightStick=Vector2.right});Check(g.ReadCommand().aimFacing==1,"right stick reverses precision aim to the right");
            Queue(new GamepadState{rightStick=Vector2.up});float z=g.Zoom;g.AdjustZoom(g.Pad.Read(true,true,false,.05f).zoom);
            Check(g.Zoom<z,"right stick up zooms toward the heroes");
            Queue(default);Queue(new GamepadState{leftTrigger=1}.WithButton(GamepadButton.South).WithButton(GamepadButton.East).WithButton(GamepadButton.RightShoulder).WithButton(GamepadButton.RightStick));c=g.ReadCommand();
            Check(c.crouch&&c.jump&&c.dash&&c.grapple&&c.ground,"LT / A / B / RB / RS map crouch, jump, evade, clinch and ground");
            Queue(default);Queue(new GamepadState().WithButton(GamepadButton.DpadUp));c=g.ReadCommand();Check(c.interact&&!c.backup,"D-pad up interacts without calling backup");
            Queue(default);Queue(new GamepadState().WithButton(GamepadButton.DpadUp).WithButton(GamepadButton.LeftShoulder));c=g.ReadCommand();Check(c.backup&&!c.interact,"LB plus D-pad up calls backup without collecting supplies");
            Queue(default);Queue(new GamepadState().WithButton(GamepadButton.DpadLeft).WithButton(GamepadButton.DpadRight));c=g.ReadCommand();Check(c.swap&&c.assist,"left and right D-pad map lead switch and partner assist");
            Queue(default);Queue(new GamepadState{rightTrigger=1}.WithButton(GamepadButton.Start));
            Check(g.State==SFState.Paused&&g.ReadCommand().shoot==false,"Start pauses and suppresses a held fire trigger");
            yield return Frame(default);yield return Capture("pause-focus");
            float volume=g.Audio.Volume;yield return Frame(new GamepadState().WithButton(GamepadButton.South));
            if(RenderedUI)Check(g.State==SFState.Paused&&g.Audio.Volume<volume,"A activates focused volume control on pause screen");g.Audio.SetMasterVolume(volume);
            yield return Frame(default);yield return Frame(new GamepadState().WithButton(GamepadButton.Start));
            Check(g.State==SFState.Playing,"second deliberate Start resumes");
            Queue(new GamepadState{rightTrigger=1});Check(g.Pad.WaitingForNeutral&&!g.ReadCommand().shoot,"held trigger cannot fire immediately after resume");
            Queue(default);Queue(new GamepadState{rightTrigger=1});Check(g.ReadCommand().shoot,"neutral release then trigger press restores fire");
            Queue(default);Queue(new GamepadState().WithButton(GamepadButton.Select));
            Check(hud.PadHelpOpen&&g.State==SFState.Paused,"View guide automatically pauses active mission");
            yield return Frame(default);yield return Frame(new GamepadState().WithButton(GamepadButton.East));
            Check(!hud.PadHelpOpen&&g.State==SFState.Paused,"closing guide leaves mission paused");
            yield return Frame(default);yield return Frame(new GamepadState().WithButton(GamepadButton.Start));yield return Frame(default);
            g.SmokeCommand=g.ReadCommand();InputSystem.RemoveDevice(pad);g.ProcessGamepad();
            Check(g.State==SFState.Paused&&!g.GamepadActive,"unplugging active controller pauses and returns keyboard access");
            pad=InputSystem.AddDevice<Gamepad>();pad.MakeCurrent();yield return Frame(new GamepadState{rightTrigger=1});
            Check(g.State==SFState.Paused&&g.Pad.WaitingForNeutral,"reconnection with held trigger cannot resume or fire");
            yield return Frame(default);yield return Frame(new GamepadState().WithButton(GamepadButton.Start));yield return Frame(default);
            Check(g.State==SFState.Playing&&g.GamepadActive,"reconnected controller explicitly resumes after neutral");
            Queue(new GamepadState{leftStick=Vector2.right});InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));InputSystem.Update();g.ProcessGamepad();
            Check(!g.GamepadActive&&g.ReadCommand().move.x==-1,"keyboard takeover routes its own movement instead of merging a held controller stick");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();g.ProcessGamepad();Check(!g.GamepadActive,"held controller does not immediately steal keyboard control back");
            Queue(default);Queue(new GamepadState{leftStick=Vector2.right});Check(g.GamepadActive,"new movement after stick neutral can retake controller control");
            InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,120)});InputSystem.Update();g.ProcessGamepad();
            Check(!g.GamepadActive&&g.ReadCommand().zoom>0,"mouse wheel can take control back and zoom without a mouse click");
            InputSystem.QueueStateEvent(mouse,new MouseState());Queue(default);Queue(new GamepadState{leftStick=Vector2.right});
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Back));InputSystem.Update();g.ProcessGamepad();
            Check(!g.GamepadActive,"mouse thumb button can take control back");
            InputSystem.QueueStateEvent(mouse,new MouseState());Queue(default);Queue(new GamepadState{leftStick=Vector2.right});
            var touch=InputSystem.AddDevice<Touchscreen>();touch.MakeCurrent();
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=21,phase=UnityEngine.InputSystem.TouchPhase.Began,position=new Vector2(200,200)});InputSystem.Update();g.ProcessGamepad();
            Check(!g.GamepadActive&&g.TouchControls,"touch contact restores touch controls after controller use");
            InputSystem.QueueStateEvent(touch,new TouchState{touchId=21,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=new Vector2(200,200)});InputSystem.Update();Queue(default);Queue(new GamepadState{leftStick=Vector2.right});
            Check(g.GamepadActive&&!g.TouchControls,"fresh controller movement returns from touch mode without stale contacts");InputSystem.RemoveDevice(touch);
            g.PauseForInterruption();Check(g.State==SFState.Paused&&g.Pad.WaitingForNeutral,"focus interruption clears controller held state");
            Queue(default);hud.OpenAudioCredits();yield return Frame(default);yield return Capture("audio-focus");
            yield return Frame(new GamepadState().WithButton(GamepadButton.East));Check(!hud.AudioCreditsOpen&&g.State==SFState.Paused,"B closes audio credits without resuming behind them");
            InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
            File.WriteAllLines(Path.Combine(qa,"gamepad-checks.txt"),results);
            Debug.Log("PAD QA COMPLETE: "+results.Count+" checks; failures="+results.Count(s=>s.StartsWith("FAIL"))+"; renderedUI="+RenderedUI);
            if(RenderedUI){g.SmokeCommand=default;g.SetState(SFState.Paused);hud.PadHelpOpen=true;}
            else Application.Quit(results.Any(s=>s.StartsWith("FAIL"))?2:0);
        }
    }
}
