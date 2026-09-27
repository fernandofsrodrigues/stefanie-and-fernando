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
    public sealed class SFPartyCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();SFGame g;string qa;
        void Check(bool pass,string label){checks.Add((pass?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void Isolate()
        {
            foreach(var a in g.Actors.Where(a=>!a.hero)){a.ReleaseGrip();a.Teleport(new Vector2(130,0));a.action=SFAction.Idle;a.cooldown=100;a.invulnerable=0;a.health=a.maxHealth;}
            g.Partner.Teleport(new Vector2(110,0));g.Player.ReleaseGrip();g.Player.action=SFAction.Idle;g.Player.cooldown=0;g.Player.invulnerable=100;
            g.SmokeCommand=default;
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(qa,"v06-title.png"));
            Check(Screen.width>=1280&&Screen.height>=720,"native display initialized: "+Screen.width+"x"+Screen.height);
            foreach(string hero in new[]{"fernando","stefanie"})foreach(string kind in new[]{"cover","aerial","field_aerial","field_actions","field_utility","field_ground"})
            {var art=Resources.Load<SFArt>("SF/"+hero+"_"+kind);Check(art!=null&&art.frames.All(f=>f!=null),hero+" "+kind+" loads complete");}
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,120)}.WithButton(MouseButton.Middle));InputSystem.Update();
            var input=SFInput.Read(false,false);Check(input.cycleWeapon&&!input.toggleArmed&&input.zoom>0,"middle mouse and wheel up mapped from input device");
            InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,-120)});InputSystem.Update();
            Check(!SFInput.Read(false,false).toggleArmed&&SFInput.Read(false,false).zoom<0,"middle release and wheel down are independent");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q,Key.LeftShift,Key.CapsLock));InputSystem.Update();input=SFInput.Read(false,false);
            Check(input.toggleArmed&&input.run&&input.toggleWalk,"Q, Shift and Caps Lock remain mapped");
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
            for(int route=0;route<6;route++){g.SelectRoute(route);yield return null;Check(g.HostilesRemaining==20&&g.Actors.Count(a=>a.boss)==1,"mission "+route+" contains twenty opponents and one boss");}
            g.SelectRoute(0);yield return null;
            g.Loadouts[0].gear=1;g.Loadouts[1].gear=2;g.StartGame();yield return new WaitForSeconds(.35f);
            Check(g.Fernando.health==150&&g.Stefanie.health==120&&g.Stefanie.RifleAmmo.Reserve==80&&g.Fernando.RifleAmmo.Reserve==60,"armor and reserve kits apply to selected heroes");
            g.AdjustZoom(100);Check(g.Zoom==3.9f,"zoom in clamps at readable close view");g.AdjustZoom(-100);Check(g.Zoom==5.625f,"zoom out clamps inside plate framing");
            Isolate();var p=g.Player;var enemy=g.Actors.First(a=>!a.Friendly&&!a.ranged&&!a.boss);
            p.Teleport(new Vector2(8,0));p.lane=-.85f;p.facing=1;p.Armed=false;
            enemy.Teleport(new Vector2(9,0));enemy.lane=p.lane;yield return new WaitForSeconds(.15f);
            Check(g.TryGrapple(p),"nearby opponent enters a close clinch");
            float before=enemy.health;g.ResolveAttack(p,SFAction.ClinchKnee);
            Check(enemy.health<before&&enemy.HeldBy==p&&p.GripTarget==enemy,"knee damages without detaching paired actors");
            var struck=g.Actors.First(a=>!a.Friendly&&a!=enemy&&!a.boss);struck.Teleport(new Vector2(11,0));struck.lane=p.lane;before=struck.health;
            Check(g.ThrowEnemy(p)&&p.GripTarget==null&&enemy.HeldBy==null,"throw releases both sides of clinch");
            yield return new WaitForSeconds(.65f);Check(enemy.X>10&&struck.health<before,"thrown body travels and hits another hostile");
            Check(struck.action==SFAction.Knocked,"throw collision knocks opponent down");
            Isolate();p.Teleport(new Vector2(8,0));p.lane=-.85f;p.facing=1;p.invulnerable=0;
            enemy.Teleport(new Vector2(9,0));enemy.lane=p.lane;enemy.action=SFAction.Punch;
            p.action=SFAction.Guard;p.GuardTime=.08f;before=p.health;
            p.Damage(20,enemy);Check(p.health==before&&p.CounterTime>0&&enemy.action==SFAction.Hurt,"timed front guard parries melee and opens counter");
            p.action=SFAction.Idle;p.cooldown=0;p.invulnerable=100;enemy.invulnerable=0;before=enemy.health;g.ResolveAttack(p,SFAction.Punch);
            Check(before-enemy.health>30&&p.CounterTime==0,"counter bonus is spent on next melee hit");
            p.Teleport(new Vector2(8,0));g.SmokeCommand=new SFCommand{jump=true};yield return new WaitForSeconds(.08f);g.SmokeCommand=default;yield return new WaitForSeconds(.1f);
            p.cooldown=0;p.BeginAttack(SFAction.Kick);yield return new WaitForSeconds(.1f);
            Check(p.AerialAttack&&p.CurrentSheet=="aerial","airborne kick selects the new civilian action sheet");g.Capture("v06-aerial");
            yield return new WaitForSeconds(1);
            foreach(var hero in new[]{g.Fernando,g.Stefanie})
            {
                hero.FieldUniform=true;hero.action=SFAction.Idle;hero.Armed=false;
                // Idle is also the locomotion action. Freeze the pose fixture explicitly;
                // the AI companion may otherwise still be travelling toward its formation slot.
                hero.moveAmount=0;hero.motor.Body.linearVelocity=Vector2.zero;
                hero.Helmet=true;hero.Render(0);Check(hero.CurrentSheet=="field_actions"&&hero.CurrentFrame==0,hero.identity+" helmeted military idle stays in outfit");
                hero.Helmet=false;hero.Render(0);Check(hero.CurrentFrame==4,hero.identity+" helmet off uses separate head artwork");
                hero.action=SFAction.Punch;hero.attackAt=.16f;hero.actionClock=.2f;hero.Render(0);Check(hero.CurrentFrame==5,hero.identity+" bare-head punch retains headgear choice");
                hero.action=SFAction.Idle;hero.Armed=hero.Rifle=hero.Crouching=true;hero.Render(0);
                Check(hero.CurrentSheet=="field_rifle"&&hero.CurrentFrame==7,hero.identity+" bare-head kneeling rifle uses rifle pose");
                hero.Armed=false;hero.Render(0);Check(hero.CurrentSheet=="field_ground"&&hero.CurrentFrame==3,hero.identity+" unarmed military crouch uses empty hands");
                hero.Crouching=false;hero.action=SFAction.Knocked;hero.Render(0);Check(hero.CurrentSheet=="field_ground"&&hero.CurrentFrame==5,hero.identity+" bare-head knockdown uses grounded artwork");
                hero.action=SFAction.Idle;hero.FieldUniform=false;hero.Helmet=true;hero.Rifle=false;
            }
            Isolate();p.Teleport(new Vector2(8,0));p.lane=-.85f;g.Partner.Teleport(new Vector2(9,0));g.Partner.lane=-.85f;g.Partner.Armed=false;
            var gunner=g.Actors.First(a=>!a.Friendly&&a.ranged);gunner.Teleport(new Vector2(13,0));gunner.lane=-.85f;gunner.cooldown=0;
            p.invulnerable=0;float leaderHealth=p.health;before=gunner.health;yield return new WaitForSeconds(.85f);
            Check(g.Partner.Armed&&gunner.health<before,"partner arms and interrupts visible hostile gunner");
            Check(p.health==leaderHealth,"partner intercepts before hostile shot damages leader without invulnerability");
            Isolate();p.Teleport(new Vector2(27,0));g.Partner.Teleport(new Vector2(29,0));g.Partner.lane=0;g.Partner.health=20;
            var med=g.Pickups.First(a=>a.kind=="medical");yield return new WaitForSeconds(1.5f);
            Check(med.taken&&g.Partner.health>20,"partner seeks and collects nearby medical supply");
            Check(!g.Partner.Armed,"partner holsters once armed threat memory expires");
            g.Partner.health=g.Partner.maxHealth;p.health=p.maxHealth;
            var food=g.Pickups.First(a=>a.kind=="food");g.Partner.Teleport(new Vector2(food.x,0));g.Partner.lane=food.lane;g.TryCollect(food,g.Partner);
            Check(!food.taken,"full-health team conserves healing pickup");
            foreach(var a in g.Actors.Where(a=>!a.Friendly)){a.health=0;a.action=SFAction.Down;}
            p.Teleport(new Vector2(128,0));g.Partner.Teleport(new Vector2(126,0));yield return new WaitForSeconds(.3f);
            Check(g.State==SFState.Won&&(g.CompletedMissions&1)!=0,"clear route and reach exit together completes mission");
            ScreenCapture.CaptureScreenshot(Path.Combine(qa,"v06-mission-clear.png"));yield return new WaitForSeconds(.15f);
            g.ReturnToLoadout(1);yield return null;
            Check(g.State==SFState.Menu&&g.RouteIndex==1&&g.HostilesRemaining==20&&g.KOs==0,"next mission rebuilds cast and returns to equipment selection");
            g.Loadouts[0].outfit=g.Loadouts[1].outfit=1;g.StartGame();yield return new WaitForSeconds(.2f);
            Check(g.Fernando.FieldUniform&&g.Stefanie.FieldUniform,"new mission applies both military outfit selections");
            g.Capture("v06-military");ScreenCapture.CaptureScreenshot(Path.Combine(qa,"v06-gameplay.png"));yield return new WaitForSeconds(.2f);
            File.WriteAllLines(Path.Combine(qa,"party-checks.txt"),checks);
            Application.Quit(checks.Any(c=>c.StartsWith("FAIL"))?2:0);
        }
    }
}
#endif
