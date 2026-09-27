#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StefanieAndFernando
{
    public sealed class SFTouchCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();
        SFGame g;Touchscreen screen;float width;int contact=10;
        void Check(bool pass,string label){checks.Add((pass?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        Vector2 Pixel(Vector2 p)=>new Vector2(p.x,900-p.y)*Screen.height/900f;
        void Touch(int id,UnityEngine.InputSystem.TouchPhase phase,Vector2 point)=>InputSystem.QueueStateEvent(screen,new TouchState{touchId=id,phase=phase,position=Pixel(point)});
        SFCommand At(int control)
        {
            Touch(++contact,UnityEngine.InputSystem.TouchPhase.Began,SFTouchInput.Control(control,width).center);InputSystem.Update();return g.Touch.Read(g.Player,width);
        }
        void Release()
        {
            foreach(var t in screen.touches.Where(t=>t.press.isPressed).ToArray())InputSystem.QueueStateEvent(screen,new TouchState{touchId=t.touchId.ReadValue(),phase=UnityEngine.InputSystem.TouchPhase.Ended,position=t.position.ReadValue()});
            InputSystem.Update();g.Touch.Read(g.Player,width);
        }
        IEnumerator Tap(int control)
        {
            int id=++contact;Touch(id,UnityEngine.InputSystem.TouchPhase.Began,SFTouchInput.Control(control,width).center);yield return null;yield return null;
            Touch(id,UnityEngine.InputSystem.TouchPhase.Ended,SFTouchInput.Control(control,width).center);yield return null;yield return null;
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return new WaitForSeconds(.4f);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            screen=InputSystem.AddDevice<Touchscreen>();screen.MakeCurrent();width=Screen.width*900f/Screen.height;
            g.EnableTouch("1");g.StartGame();g.SetState(SFState.Paused);
            foreach(float testWidth in new[]{1100f,1200f,1600f,2100f})
            {
                var rects=Enumerable.Range(0,SFTouchInput.ControlCount).Select(i=>SFTouchInput.Control(i,testWidth)).ToArray();
                Check(rects.All(r=>r.xMin>=20&&r.xMax<=testWidth-20&&r.yMin>=200&&r.yMax<819)&&!rects.Any(r=>r.Overlaps(new Rect(20,560,280,245)))&&!rects.Where((r,i)=>rects.Skip(i+1).Any(r.Overlaps)).Any(),"touch targets remain separate from each other, stick and HUD at width "+testWidth);
            }
            g.Player.Armed=false;Check(At(0).punch,"unarmed primary touch punches");Release();
            g.Player.Armed=true;Check(At(0).shoot,"armed primary touch fires");Release();
            Check(At(1).kick,"secondary touch requests kick or clinch throw");Release();
            Check(At(2).aim,"armed defensive touch aims");Release();
            g.Player.Armed=false;Check(At(2).guard,"unarmed defensive touch guards");Release();
            Check(At(3).jump,"jump touch maps to jump");Release();
            Check(At(4).dash,"evade touch maps to dash");Release();
            Check(At(5).grapple,"clinch touch maps to grapple");Release();
            Check(At(6).weapon==1,"weapon touch draws pistol from unarmed");Release();
            g.Player.Armed=true;g.Player.Rifle=false;Check(At(6).weapon==2,"weapon touch selects rifle from pistol");Release();
            g.Player.Rifle=true;Check(At(6).toggleArmed,"weapon touch holsters rifle");Release();
            Check(At(7).crouch,"crouch touch holds crouch");Release();
            Check(At(8).swap,"switch touch changes lead");Release();
            Check(At(9).assist,"assist touch requests assist");Release();
            Check(At(10).zoom==1,"ZOOM + maps closer");Release();
            Check(At(11).zoom==-1,"ZOOM - maps farther");Release();
            Check(At(12).ground,"ground touch requests ground action");Release();
            Check(At(13).interact,"interact touch requests contextual action");Release();
            Check(At(14).backup,"backup touch requests regional support");Release();
            At(14);InputSystem.Update();Check(!g.Touch.Read(g.Player,width).backup,"holding backup does not repeatedly request it");Release();
            Touch(++contact,UnityEngine.InputSystem.TouchPhase.Began,SFTouchInput.StickCenter);InputSystem.Update();g.Touch.Read(g.Player,width);
            int stick=contact;Touch(stick,UnityEngine.InputSystem.TouchPhase.Moved,SFTouchInput.Control(0,width).center);InputSystem.Update();var c=g.Touch.Read(g.Player,width);
            Check(c.move.x>.8f&&!c.shoot,"joystick finger dragged onto FIRE retains joystick ownership");
            At(0);Touch(stick,UnityEngine.InputSystem.TouchPhase.Canceled,Vector2.zero);InputSystem.Update();c=g.Touch.Read(g.Player,width);
            Check(c.move==Vector2.zero&&c.shoot&&g.Touch.Held(0),"canceling stick preserves held action and its highlight");
            g.Touch.Reset();InputSystem.Update();c=g.Touch.Read(g.Player,width);
            Check(!c.shoot&&!g.Touch.Held(0)&&c.move==Vector2.zero,"reset denies still-held contacts until a fresh press");Release();
            // Integrated checks run the normal Update with real queued touchscreen events.
            foreach(var a in g.Actors.Where(a=>!a.hero)){a.Teleport(new Vector2(130,0));a.cooldown=999;}
            g.Player.Teleport(new Vector2(8,0));g.Partner.Teleport(new Vector2(7,0));g.Player.lane=g.Partner.lane=-.9f;
            g.Player.invulnerable=g.Partner.invulnerable=100;g.Player.action=SFAction.Idle;g.Player.cooldown=0;
            g.SetState(SFState.Playing);g.SmokeMode=false;
            g.Partner.health=0;g.Partner.recovery=8;g.Partner.action=SFAction.Down;
            Check(g.InteractionHint().StartsWith("INTERACT"),"nearby revive hint uses touch action name");
            yield return Tap(13);Check(g.Partner.Alive,"touch INTERACT revives nearby partner through game Update");
            int calls=g.BackupCalls;yield return Tap(14);Check(g.BackupCalls==calls+1&&g.BackupArrival>0,"touch BACKUP starts actual support arrival");
            int selected=g.Selected;yield return Tap(8);Check(g.Selected!=selected,"touch SWITCH changes active hero through game Update");
            g.Zoom=4.7f;yield return Tap(10);float near=g.Zoom;yield return Tap(11);Check(near<4.7f&&Mathf.Abs(g.Zoom-4.7f)<.001f,"both touch zoom controls change and restore camera distance");
            g.Player.Armed=false;g.Player.action=SFAction.Idle;g.Player.cooldown=0;
            yield return Tap(6);Check(g.Player.Armed&&!g.Player.Rifle,"touch WEAPON actually equips pistol");
            yield return Tap(6);Check(g.Player.Armed&&g.Player.Rifle,"second touch WEAPON actually equips rifle");
            yield return Tap(6);Check(!g.Player.Armed,"third touch WEAPON returns to unarmed");
            At(0);g.PauseForInterruption();float elapsed=g.Elapsed,x=g.Player.X;yield return new WaitForSeconds(.2f);
            Check(g.State==SFState.Paused&&Mathf.Approximately(elapsed,g.Elapsed)&&Mathf.Abs(x-g.Player.X)<.01f,"foreground interruption pauses mission time and physics");
            g.SetState(SFState.Playing);yield return null;c=g.Touch.Read(g.Player,width);
            Check(!c.punch&&!g.Touch.Held(0),"resume cannot reactivate the pre-interruption held attack");Release();
            g.SetState(SFState.Menu);g.PauseForInterruption();Check(g.State==SFState.Menu,"interruption leaves title state intact");
            g.SmokeMode=true;InputSystem.RemoveDevice(screen);
            string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(qa);File.WriteAllLines(Path.Combine(qa,"touch-v072-checks.txt"),checks);
            Application.Quit(checks.Any(c=>c.StartsWith("FAIL"))?1:0);
        }
    }
}
#endif
