#if !UNITY_WEBGL
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
    // Opt-in executable integration test. Virtual devices are removed after checking the public input map.
    public sealed class SFControlsCheck:MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        void Check(bool pass,string name){results.Add((pass?"PASS: ":"FAIL: ")+name);Debug.Log(results.Last());}
        IEnumerator Start()
        {
            var game=GetComponent<SFGame>();yield return null;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl,Key.Space,Key.Q,Key.E));InputSystem.Update();
            var command=SFInput.Read(false,false);
            Check(command.crouch&&command.jump&&command.toggleArmed&&command.interact,"Ctrl / Space / Q / E mapped from a keyboard device");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left).WithButton(MouseButton.Right).WithButton(MouseButton.Back));InputSystem.Update();
            command=SFInput.Read(false,false);Check(command.punch&&command.kick&&command.grapple&&!command.shoot&&!command.aim,"unarmed mouse primary / secondary / lower thumb");
            command=SFInput.Read(true,false);Check(command.shoot&&command.aim&&!command.punch&&!command.kick,"same mouse buttons route to fire and aim when armed");
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Forward));InputSystem.Update();
            Check(SFInput.Read(false,false).ground&&SFInput.Read(false,true).grapple,"upper thumb and swap preference");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.N,Key.LeftAlt));InputSystem.Update();
            Check(SFInput.Read(false,false).backup&&SFInput.Read(false,false).walk,"N calls backup and Alt holds walk");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift,Key.CapsLock));InputSystem.Update();
            command=SFInput.Read(false,false);Check(command.run&&command.toggleWalk&&!command.dash,"Shift holds run; Caps Lock press toggles walk; Shift does not evade");
            InputSystem.Update();command=SFInput.Read(false,false);Check(command.run&&!command.toggleWalk,"held Caps Lock does not repeatedly toggle walking");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightShift,Key.X));InputSystem.Update();
            command=SFInput.Read(false,false);Check(command.run&&command.dash,"right Shift also runs and X triggers evade");
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
            game.StartGame();yield return new WaitForSeconds(.4f);
            var player=game.Player;game.Partner.Teleport(new Vector2(115,0));game.Partner.cooldown=999;
            game.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.4f);
            float jog=Mathf.Abs(player.motor.Body.linearVelocity.x);
            command=new SFCommand{toggleWalk=true};game.ApplyGait(ref command);
            Check(game.WalkMode&&command.walk,"Caps Lock movement command latches walking on");
            player.Teleport(new Vector2(3,0));game.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.4f);
            float walk=Mathf.Abs(player.motor.Body.linearVelocity.x);
            game.SmokeCommand=new SFCommand{move=Vector2.right,run=true};yield return new WaitForSeconds(.4f);
            float run=Mathf.Abs(player.motor.Body.linearVelocity.x);
            Check(walk>2.3f&&walk<2.7f&&jog>3.5f&&jog<4f&&run>5f,"walk / default jog / held Shift run produce distinct motor speeds");
            game.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.4f);
            Check(game.WalkMode&&Mathf.Abs(player.motor.Body.linearVelocity.x)<2.7f,"releasing Shift restores toggled walking");
            game.SmokeCommand=new SFCommand{move=Vector2.right,crouch=true,run=true};yield return new WaitForSeconds(.3f);
            Check(player.Crouching&&Mathf.Abs(player.motor.Body.linearVelocity.x)<2.4f,"Shift does not bypass crouch speed restriction");
            game.SmokeCommand=default;game.SetState(SFState.Paused);yield return new WaitForSeconds(.1f);
            Check(game.WalkMode,"pause preserves walking preference");game.SetState(SFState.Playing);
            command=new SFCommand{toggleWalk=true};game.ApplyGait(ref command);
            Check(!game.WalkMode&&!command.walk,"second Caps Lock command returns to default jog");
            player.Teleport(new Vector2(3,0));
            game.SmokeCommand=new SFCommand{crouch=true,move=Vector2.right};float start=player.X;
            yield return new WaitForSeconds(.4f);
            Check(player.Crouching&&player.Capsule.size.y<1.3f&&player.X-start>.3f&&player.X-start<1.3f,"crouch lowers collision capsule and reduces travel speed");
            game.SmokeCommand=new SFCommand{toggleArmed=true};yield return null;game.SmokeCommand=default;yield return null;
            Check(player.Armed&&!player.Crouching&&player.Capsule.size.y>2,"Q arms hero and releasing Ctrl restores capsule");
            game.SmokeCommand=new SFCommand{aim=true,aimFacing=-1};yield return null;yield return null;
            Check(player.Aiming&&player.facing==-1,"precision aim tracks facing independently of movement");
            game.SmokeCommand=default;yield return null;
            var enemy=game.Actors.First(a=>!a.hero);enemy.Teleport(new Vector2(player.X+7,0));enemy.lane=player.lane;enemy.cooldown=5;
            player.facing=1;int ammo=game.Ammo;float hp=enemy.health;
            game.SmokeCommand=new SFCommand{shoot=true};yield return new WaitForSeconds(.23f);game.SmokeCommand=default;
            Check(game.Ammo==ammo-1&&enemy.health<hp,"one firearm action consumes ammunition and damages aligned enemy");
            yield return new WaitForSeconds(.5f);
            game.SmokeCommand=new SFCommand{weapon=2,aim=true};yield return null;yield return null;game.SmokeCommand=default;
            Check(player.Rifle&&player.Armed,"rifle selection uses the armed mode");
            enemy.Teleport(new Vector2(player.X+10,0));enemy.lane=player.lane;enemy.invulnerable=0;enemy.cooldown=5;hp=enemy.health;
            game.SmokeCommand=new SFCommand{shoot=true,aim=true,aimFacing=1};yield return new WaitForSeconds(.15f);game.SmokeCommand=default;
            Check(enemy.health<hp,"rifle precision shot reaches beyond sidearm base range");
            yield return new WaitForSeconds(.5f);
            var target=game.Actors.First(a=>!a.hero&&a.Alive&&!a.boss);target.health=target.maxHealth=1000;target.Teleport(new Vector2(player.X+4,0));target.lane=player.lane;target.cooldown=10;
            Check(!game.TryGrapple(player),"grapple rejects out-of-range opponents");
            target.Teleport(new Vector2(player.X+1.1f,0));target.lane=player.lane;yield return new WaitForSeconds(.08f);
            Check(game.TryGrapple(player)&&target.HeldBy==player&&target.action==SFAction.Held,"close grapple pairs the actors and restrains the target");
            game.SmokeCommand=new SFCommand{ground=true};yield return new WaitForSeconds(.08f);game.SmokeCommand=default;
            Check(player.GripTarget==null&&target.HeldBy==null&&target.action==SFAction.Knocked,"ground input releases grapple into a takedown");
            yield return new WaitForSeconds(.9f);target.health=64;target.invulnerable=0;target.KnockDown();hp=target.health;
            game.SmokeCommand=new SFCommand{ground=true};yield return new WaitForSeconds(.42f);game.SmokeCommand=default;
            Check(target.health<hp,"ground strike damages a knocked-down opponent");
            yield return new WaitForSeconds(.9f);
            var item=game.Pickups.First(p=>p.kind=="food");player.Teleport(new Vector2(item.x,0));player.lane=item.lane;
            yield return new WaitForSeconds(.15f);Check(!item.taken,"walking over supplies does not consume them automatically");
            player.health=70;Check(game.TryInteract()&&item.taken,"E-context interaction collects nearby supplies");
            game.SetState(SFState.Paused);float pausedX=player.X;yield return new WaitForSeconds(.15f);
            Check(Mathf.Abs(pausedX-player.X)<.01f,"pause freezes the new control states");
            string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(qa);File.WriteAllLines(Path.Combine(qa,"controls-checks.txt"),results);
            Application.Quit(results.Any(s=>s.StartsWith("FAIL"))?2:0);
        }
    }
}
#endif
