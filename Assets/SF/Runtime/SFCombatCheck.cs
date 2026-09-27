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
    public sealed class SFCombatCheck:MonoBehaviour
    {
        SFGame g;SFActor enemy;readonly List<string> checks=new List<string>();
        void Check(bool ok,string label){checks.Add((ok?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void Advance(SFActor p,float seconds,SFCommand c=default){for(float t=0;t<seconds-.001f;t+=.02f)p.Tick(Mathf.Min(.02f,seconds-t),c);}
        void Ready(SFActor p)
        {
            p.ReleaseGrip();p.Armed=false;p.ResetCombo();p.action=SFAction.Idle;p.cooldown=0;p.facing=1;p.invulnerable=0;
            p.Teleport(new Vector2(7,0));p.lane=-.85f;
            enemy.Teleport(new Vector2(8.2f,0));enemy.lane=p.lane;enemy.action=SFAction.Idle;enemy.health=enemy.maxHealth=1000;enemy.invulnerable=0;
        }
        void Strike(SFActor p,bool kick=false)
        {
            enemy.invulnerable=0;enemy.action=SFAction.Idle;enemy.Teleport(new Vector2(8.2f,0));enemy.lane=p.lane;
            p.Tick(.02f,new SFCommand{punch=!kick,kick=kick});
            Advance(p,p.attackDuration+.08f);
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return new WaitForSeconds(.5f);g.StartGame();
            foreach(var a in g.Actors){a.Teleport(new Vector2(a.hero?7:125,0));a.cooldown=999;a.lane=-.85f;}
            yield return new WaitForSeconds(.3f);g.SetState(SFState.Paused);
            enemy=g.Actors.First(a=>!a.Friendly&&!a.boss);g.Partner.Teleport(new Vector2(100,0));
            var mag=new SFMagazine();mag.Reset(20,41);for(int i=0;i<7;i++)mag.Fire();mag.Reload();
            Check(mag.Loaded==20&&mag.Reserve==34&&mag.Total==54,"partial reload preserves all unfired rounds");
            mag.Reset(20,3);for(int i=0;i<20;i++)mag.Fire();Check(!mag.Fire()&&mag.Loaded==0,"empty magazine cannot become negative");mag.Reload();
            Check(mag.Loaded==3&&mag.Reserve==0,"short reserve produces a partial magazine");
            foreach(var p in new[]{g.Fernando,g.Stefanie})
            {
                g.Selected=p==g.Fernando?0:1;g.Partner.Teleport(new Vector2(100,0));Ready(p);
                var expected=p.identity=="fernando"?new[]{SFStrike.Jab,SFStrike.Straight,SFStrike.Jab,SFStrike.Straight,SFStrike.Uppercut}:new[]{SFStrike.Jab,SFStrike.Straight,SFStrike.Hook,SFStrike.Roundhouse};
                for(int i=0;i<expected.Length;i++)
                {
                    float hp=enemy.health;Strike(p);
                    Check(p.ComboMove.kind==expected[i]&&p.ActiveComboStep==i&&enemy.health<hp,p.identity+" connected combo step "+(i+1)+" "+expected[i]);
                }
                Check(p.NextComboStep==0&&enemy.action==SFAction.Knocked&&enemy.X>8.2f,p.identity+" finisher knocks down, creates space and resets chain");
                Ready(p);Strike(p);Advance(p,1);Strike(p);Check(p.ActiveComboStep==0,p.identity+" deliberate pause resets to jab");
                Ready(p);enemy.Teleport(new Vector2(12,0));p.Tick(.02f,new SFCommand{punch=true});Advance(p,.5f);
                Check(p.NextComboStep==0,p.identity+" whiff cannot advance a combo");
                Ready(p);Strike(p);p.Damage(1,enemy,true);Check(p.NextComboStep==0,p.identity+" taking damage clears pending combo");
            }
            var f=g.Fernando;g.Selected=0;g.Stefanie.Teleport(new Vector2(100,0));Ready(f);
            for(int i=0;i<4;i++)Strike(f);Strike(f,true);
            Check(f.ComboMove.kind==SFStrike.Roundhouse&&f.ComboMove.finisher,"Fernando can choose roundhouse instead of uppercut on fifth hit");
            Ready(f);f.Tick(.02f,new SFCommand{punch=true});Advance(f,.22f);f.Tick(.02f,new SFCommand{punch=true});Advance(f,.13f);
            Check(f.ActiveComboStep==1&&f.Busy,"late punch tap buffers across recovery without holding the button");
            Ready(f);f.ResetWeapons();f.Armed=f.Rifle=true;f.RifleAmmo.Fire();f.RifleAmmo.Fire();int reserve=f.RifleAmmo.Reserve;
            Check(f.TryReload(),"partly used rifle can start a timed reload");Advance(f,.5f,new SFCommand{shoot=true});
            Check(f.Reloading&&f.RifleAmmo.Loaded==18&&f.RifleAmmo.Reserve==reserve,"reload blocks firing and transfers no ammo early");
            float clock=f.actionClock;yield return new WaitForSeconds(.15f);Check(f.actionClock==clock,"pause freezes the live reload timer");
            f.Tick(.02f,new SFCommand{cycleWeapon=true});Check(!f.Reloading&&!f.Rifle&&f.Armed&&f.RifleAmmo.Loaded==18,"middle switch cancels reload once without refilling old weapon");
            f.Tick(.02f,new SFCommand{toggleArmed=true});Check(!f.Armed&&!f.Rifle,"Q holsters while retaining selected weapon");
            f.Tick(.02f,new SFCommand{toggleArmed=true});Check(f.Armed&&!f.Rifle,"Q draws remembered sidearm");
            f.Tick(.02f,new SFCommand{weapon=2});Check(f.Armed&&f.Rifle,"2 selects signature rifle directly");
            f.TryReload();Advance(f,2.3f);Check(!f.Reloading&&f.RifleAmmo.Loaded==20&&f.RifleAmmo.Reserve==reserve-2,"completed rifle reload transfers exactly missing rounds");
            Advance(f,.2f);f.RifleAmmo.Fire();f.TryReload();f.invulnerable=0;f.Damage(1,enemy,true);Advance(f,3);
            Check(f.RifleAmmo.Loaded==19&&!f.Reloading,"damage interrupts reload without ammo creation");
            int partnerAmmo=g.Stefanie.TotalAmmo,pistol=f.SidearmAmmo.Total;f.action=SFAction.Idle;f.cooldown=0;
            f.Tick(.02f,new SFCommand{shoot=true});Advance(f,.09f);
            Check(f.RifleAmmo.Loaded==18&&g.Stefanie.TotalAmmo==partnerAmmo&&f.SidearmAmmo.Total==pistol,"rifle shot changes only this hero's rifle magazine");
            Advance(f,.5f);f.RifleAmmo.Clear();Check(!f.TryReload(),"no reserve cannot start reload");
            f.RifleAmmo.Supply(12);Check(f.RifleAmmo.Loaded==0&&f.RifleAmmo.Reserve==12,"supplies enter reserve rather than instantly loading gun");
            f.TryReload();Advance(f,2.3f);Check(f.RifleAmmo.Loaded==12&&f.RifleAmmo.Reserve==0,"last twelve rounds load without invented ammunition");
            Ready(f);f.ResetWeapons();f.Armed=f.Rifle=true;g.Selected=1;g.Stefanie.Teleport(new Vector2(10,0));
            for(int i=0;i<20;i++)f.RifleAmmo.Fire();enemy.ranged=true;
            var ai=g.TacticalPartnerCommand();Check(ai.reload,"AI partner requests reload against a visible armed threat");
            f.Tick(.02f,ai);Advance(f,2.3f);Check(f.RifleAmmo.Loaded==20&&f.RifleAmmo.Reserve==40,"AI partner completes reload using its own reserve");
            enemy.ranged=false;g.Selected=0;Ready(f);f.Armed=false;g.Stefanie.Teleport(new Vector2(7.2f,0));g.Stefanie.lane=f.lane;g.Stefanie.facing=1;
            g.SetState(SFState.Playing);Check(g.TryGrapple(f),"combat fixture acquires a live clinch");g.SetState(SFState.Paused);float heldHealth=enemy.health;
            g.Stefanie.ResetCombo();g.ResolveAttack(g.Stefanie,SFAction.Punch);
            Check(enemy.health==heldHealth&&enemy.HeldBy==f,"teammate attack preserves the other hero's clinch");
            ai=g.TacticalPartnerCommand();Check(!ai.punch&&!ai.grapple&&!ai.shoot,"partner selects other threats instead of a held enemy");
            f.ReleaseGrip();g.Stefanie.Teleport(new Vector2(100,0));f.ResetWeapons();f.Armed=f.Rifle=true;
            var supply=g.Pickups.First(p=>p.kind=="ammo");supply.taken=false;f.Teleport(new Vector2(supply.x,supply.height-.8f));f.lane=supply.lane;
            int beforeReserve=f.RifleAmmo.Reserve,beforePistol=f.SidearmAmmo.Total,beforeBuddy=g.Stefanie.TotalAmmo;g.TryCollect(supply,f);
            Check(supply.taken&&f.RifleAmmo.Reserve==beforeReserve+12&&f.SidearmAmmo.Total==beforePistol&&g.Stefanie.TotalAmmo==beforeBuddy,"ammo pickup supplies only the collector's selected weapon reserve");
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Middle));InputSystem.Update();
            var c=SFInput.Read(true,false);Check(c.reload&&c.cycleWeapon&&!c.toggleArmed,"virtual keyboard R and middle mouse map to reload and firearm switch");
            SFInput.Read(false,false);SFInput.Read(true,false); // Warm both context maps as a live frame does.
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F,Key.J));InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            Check(SFInput.Read(true,false).shoot&&SFInput.Read(false,false).punch,"keyboard taps pressed and released within one frame are retained");
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();
            Check(SFInput.Read(true,false).shoot&&SFInput.Read(false,false).punch,"mouse click pressed and released within one frame is retained");
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
            var pad=InputSystem.AddDevice<Gamepad>();pad.MakeCurrent();var mapping=new SFGamepadInput();mapping.Poll();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));InputSystem.Update();mapping.Poll();
            InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();mapping.Poll();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));InputSystem.Update();mapping.Poll();
            c=mapping.Read(true,true,false,.02f);Check(c.reload&&!c.kick,"controller X reloads while armed");
            c=mapping.Read(false,false,false,.02f);Check(c.kick&&!c.reload,"controller X keeps unarmed kick");
            InputSystem.QueueStateEvent(pad,new GamepadState{rightTrigger=1});InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();mapping.Poll();
            Check(mapping.Read(true,true,false,.02f).shoot&&mapping.Read(false,false,false,.02f).punch,"controller quick trigger pull survives a release in the same frame");InputSystem.RemoveDevice(pad);
            var screen=InputSystem.AddDevice<Touchscreen>();screen.MakeCurrent();g.Touch.Reset();
            float width=Screen.width*900f/Screen.height;Vector2 at=SFTouchInput.Control(15,width).center;
            InputSystem.QueueStateEvent(screen,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=new Vector2(at.x,900-at.y)*Screen.height/900f});InputSystem.Update();
            Check(g.Touch.Read(f,width).reload,"touch reload target emits deliberate reload command");InputSystem.RemoveDevice(screen);
            string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Combat-v0846"));Directory.CreateDirectory(qa);
            Ready(f);f.FieldUniform=false;
            for(int i=0;i<4;i++)Strike(f);
            enemy.invulnerable=0;f.Tick(.02f,new SFCommand{punch=true});
            var upper=Resources.Load<SFArt>("SF/fernando_uppercut");
            Check(upper!=null&&upper.frames.Length==8&&upper.frames.All(x=>x!=null),"civilian uppercut imports all eight complete frames");
            Check(SFStrikeAnimation.UppercutFrame(f.attackAt-.001f,f.attackAt,f.attackDuration)==2&&SFStrikeAnimation.UppercutFrame(f.attackAt,f.attackAt,f.attackDuration)==3,"uppercut contact art changes exactly at the damage event");
            float[] times={0,.08f,.16f,.22f,.29f,.35f,.41f,.49f};
            for(int i=0;i<times.Length;i++)
            {
                f.actionClock=times[i];f.Render(0);
                Check(f.CurrentSheet=="uppercut"&&f.CurrentFrame==i,"uppercut timed phase "+i+" selects the authored pose");
                SFGaitCheck.CaptureActor(f,qa,"uppercut-"+i.ToString("00"));
            }
            f.FieldUniform=true;f.Render(0);Check(f.CurrentSheet!="uppercut"&&f.CurrentSheet.StartsWith("field_"),"civilian uppercut never replaces the selected military uniform");
            f.FieldUniform=false;f.action=SFAction.Hurt;f.Render(0);Check(f.CurrentSheet=="reaction","damage pose takes priority over uppercut art");
            f.action=SFAction.Idle;f.Render(0);Check(f.CurrentSheet!="uppercut","uppercut releases its sprite on recovery");
            Ready(f);f.FieldUniform=true;f.Armed=f.Rifle=true;f.ResetWeapons();f.RifleAmmo.Fire();f.TryReload();f.Render(0);
            SFGaitCheck.CaptureActor(f,qa,"rifle-reload-provisional");
            File.WriteAllLines(Path.Combine(qa,"combat-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
