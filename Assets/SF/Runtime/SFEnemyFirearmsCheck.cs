using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        public void RunEnemyFirearmsQA()
        {
            if(!DriveActive&&GetComponent<SFEnemyFirearmsCheck>()==null)
            {SmokeMode=true;gameObject.AddComponent<SFEnemyFirearmsCheck>();}
        }
    }
    public sealed class SFEnemyFirearmsCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();SFGame g;string qa;
        bool Web=>Application.platform==RuntimePlatform.WebGLPlayer;
        void Check(bool ok,string label){checks.Add((ok?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void Advance(SFActor a,float seconds,SFCommand c=default)
        {for(float t=0;t<seconds-.000001f;){float dt=Mathf.Min(.02f,seconds-t);a.Tick(dt,c);t+=dt;}}
        void Ready(SFActor a,int facing=1)
        {
            g.SetState(SFState.Playing);g.Selected=0;
            foreach(var other in g.Actors)
            {other.ReleaseGrip();other.HeldBy=null;if(other!=a&&other!=g.Player){other.Teleport(new Vector2(125,0));other.cooldown=999;}}
            g.Player.health=1000;g.Player.action=SFAction.Idle;g.Player.cooldown=0;g.Player.invulnerable=0;g.Player.Armed=false;
            g.Player.Teleport(new Vector2(15+facing*5,0));g.Player.lane=-.85f;g.Player.facing=-facing;
            a.health=a.maxHealth;a.invulnerable=a.cooldown=0;a.action=SFAction.Idle;a.Crouching=a.Aiming=false;a.Armed=true;
            a.Teleport(new Vector2(15,0));a.lane=-.85f;a.facing=facing;a.moveAmount=0;
            a.EnemyAmmo.Reset(a.identity=="keel"?6:30,a.identity=="keel"?12:60);Advance(a,.6f);a.cooldown=0;
        }
        void Empty(SFActor a){while(a.EnemyAmmo.Fire()){}a.action=SFAction.Idle;a.cooldown=0;}
        IEnumerator View(SFActor a,string name)
        {
            a.Render(0);g.Camera.orthographicSize=4;g.Camera.transform.position=new Vector3(a.X,-.55f,-10);
            Debug.Log("ENEMY FIREARMS QA VIEW: "+name);
            if(!Web)
            {
                g.Capture("v0846-enemy-reload-"+name);
#if !UNITY_WEBGL
                SFGaitCheck.CaptureActor(a,qa,name,1000);
#endif
            }
            yield return new WaitForSeconds(Web?1.4f:.05f);
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return new WaitForSeconds(.4f);g.StartGame();yield return new WaitForSeconds(.3f);g.enabled=false;
            qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/EnemyFirearms-v0846"));if(!Web)Directory.CreateDirectory(qa);
            var enemies=new[]{g.Actors.First(a=>a.identity=="keel"),g.Actors.First(a=>a.identity=="ratchet")};
            foreach(var a in enemies)
            {
                string id=a.identity;int capacity=id=="keel"?6:30,reserve=id=="keel"?12:60;
                Check(a.HasEnemyFirearm&&a.EnemyAmmo.Capacity==capacity&&a.EnemyAmmo.Total==capacity+reserve,id+" starts with its finite weapon profile");
                var art=Resources.Load<SFArt>("SF/"+id+"_reload");Check(art!=null&&art.frames.Length==4&&art.frames.All(s=>s!=null),id+" four reload key poses load");
                foreach(int facing in new[]{1,-1})
                {
                    Ready(a,facing);Check(!a.TryEnemyReload(),id+" full load cannot trigger cosmetic reload "+facing);Empty(a);
                    var command=g.EnemyCommand(a);a.Tick(.01f,command);
                    Check(a.Reloading&&a.EnemyAmmo.Loaded==0&&a.CurrentSheet=="reload",id+" AI starts empty reload without firing "+facing);
                    int heroAmmo=g.Player.TotalAmmo,partnerAmmo=g.Partner.TotalAmmo,otherAmmo=enemies.First(e=>e!=a).EnemyAmmo.Total;
                    foreach(float phase in new[]{.05f,.35f,.6f,.9f})
                    {
                        Advance(a,a.attackDuration*phase-a.actionClock);int index=SFReloadAnimation.Frame(a.actionClock,a.attackDuration);
                        Check(a.Reloading&&a.CurrentSheet=="reload"&&a.CurrentFrame==index&&a.visual.flipX==(facing<0),id+" phase "+index+" uses correct pose and facing "+facing);
                        Check(a.EnemyAmmo.Loaded==0&&a.EnemyAmmo.Reserve==reserve,id+" phase "+index+" transfers no ammunition early "+facing);
                        if(facing==1)yield return View(a,id+"-phase-"+index);
                    }
                    float clock=a.actionClock,lane=a.lane;int face=a.facing;a.BeginAttack(SFAction.Shoot);g.Player.Teleport(new Vector2(a.X-facing*5,0));g.EnemyCommand(a);
                    Advance(a,.01f,new SFCommand{move=Vector2.up,reload=true,shoot=true,punch=true,cycleWeapon=true});
                    Check(a.Reloading&&a.actionClock>clock&&a.lane==lane&&a.facing==face,id+" reload cannot fire, restart, rotate or move depth "+facing);
                    Advance(a,a.attackDuration-a.actionClock+.02f);
                    Check(!a.Reloading&&a.EnemyAmmo.Loaded==capacity&&a.EnemyAmmo.Reserve==reserve-capacity&&a.CurrentSheet!="reload",id+" completion transfers one load exactly "+facing);
                    Advance(a,.3f);Check(a.EnemyAmmo.Total==reserve,id+" completion cannot repeat "+facing);
                    Check(g.Player.TotalAmmo==heroAmmo&&g.Partner.TotalAmmo==partnerAmmo&&enemies.First(e=>e!=a).EnemyAmmo.Total==otherAmmo,id+" reload does not borrow team or other enemy ammo "+facing);
                }
                Ready(a);a.BeginAttack(SFAction.Shoot);Advance(a,a.attackAt-.02f);
                Check(a.EnemyAmmo.Loaded==capacity,id+" windup retains round until contact");
                Advance(a,.04f);Check(a.EnemyAmmo.Loaded==capacity-1,id+" actual shot consumes one round");Advance(a,.1f);
                Check(a.EnemyAmmo.Loaded==capacity-1,id+" repeated contact ticks cannot duplicate shot debit");
                Ready(a);a.BeginAttack(SFAction.Shoot);Advance(a,.2f);a.Damage(1,g.Player,true);Advance(a,2);
                Check(a.EnemyAmmo.Loaded==capacity,id+" interrupted windup consumes no round");
                Ready(a);g.Player.lane=1.05f;a.BeginAttack(SFAction.Shoot);Advance(a,1.2f);
                Check(a.EnemyAmmo.Loaded==capacity-1,id+" a missed shot still costs a round");
                Ready(a);var cover=new SFCover{footprint=new Rect(17,-1.1f,.8f,.5f),height=3,label="enemy QA cover"};g.Covers.Add(cover);
                float hp=g.Player.health;a.BeginAttack(SFAction.Shoot);Advance(a,1.2f);
                Check(a.EnemyAmmo.Loaded==capacity-1&&g.Player.health==hp&&g.LastShotEndpoint.x<18,id+" cover absorbs shot while ammunition is spent");g.Covers.Remove(cover);
                Ready(a);Empty(a);a.TryEnemyReload();Advance(a,.8f);a.Damage(1,g.Player,true);a.Render(0);Advance(a,5);
                Check(!a.Reloading&&a.CurrentSheet!="reload"&&a.EnemyAmmo.Loaded==0&&a.EnemyAmmo.Reserve==reserve,id+" hurt cancels reload with no deferred transfer");
                a.cooldown=0;Check(a.TryEnemyReload(),id+" recovery can start a fresh reload");Advance(a,.4f);a.KnockDown();a.Render(0);Advance(a,5);
                Check(!a.Reloading&&a.EnemyAmmo.Loaded==0&&a.EnemyAmmo.Reserve==reserve,id+" knockdown cancels reload");
                Ready(a);Empty(a);a.TryEnemyReload();g.Player.Teleport(new Vector2(a.X-.95f,0));g.Player.lane=a.lane;g.Player.facing=1;
                Check(g.TryGrapple(g.Player,1.1f,true,a),id+" reload gives a close-approach clinch opening");a.Render(0);
                Check(a.action==SFAction.Held&&a.CurrentSheet!="reload"&&a.EnemyAmmo.Loaded==0,id+" clinch immediately owns presentation and cancels reload");g.Player.ReleaseGrip();Advance(a,5);
                Check(a.EnemyAmmo.Loaded==0&&a.EnemyAmmo.Reserve==reserve,id+" released clinch has no hidden reload completion");
                Ready(a);Empty(a);a.TryEnemyReload();Advance(a,.8f);a.health=1;a.invulnerable=0;a.Damage(5,g.Player,true);Advance(a,5);
                Check(!a.Alive&&!a.Reloading&&a.EnemyAmmo.Loaded==0&&a.EnemyAmmo.Reserve==reserve,id+" defeat cannot complete reload");
                Ready(a);a.EnemyAmmo.Clear();a.EnemyAmmo.Supply(2);Check(a.TryEnemyReload(),id+" short reserve can start a reload");Advance(a,a.attackDuration+.02f);
                Check(a.EnemyAmmo.Loaded==2&&a.EnemyAmmo.Reserve==0,id+" short reserve yields exactly two rounds");
                Ready(a);a.EnemyAmmo.Clear();a.BeginAttack(SFAction.Shoot);var emptyCommand=g.EnemyCommand(a);a.Tick(.02f,emptyCommand);
                Check(!a.Reloading&&a.action!=SFAction.Shoot&&a.EnemyOutOfAmmo&&emptyCommand.move.x<0,id+" exhausted shooter withdraws and cannot invent shots or reloads");
                Check(!a.TryEnemyReload()&&a.EnemyAmmo.Total==0,id+" zero reserve cannot reload");
                Ready(a);for(int i=0;i<3;i++)a.EnemyAmmo.Fire();var shelter=g.Covers[0];
                a.Teleport(new Vector2(shelter.footprint.xMax+.6f,0));a.lane=shelter.footprint.center.y;a.CoverChoice=shelter;a.CoverClock=0;
                g.Player.Teleport(new Vector2(shelter.footprint.xMin-2.5f,0));g.Player.lane=a.lane;g.EnemyCommand(a);
                Check(a.Reloading,id+" sheltered AI can reload after three shots");Advance(a,a.attackDuration+.02f);
                Check(a.EnemyAmmo.Loaded==capacity&&a.EnemyAmmo.Reserve==reserve-3,id+" tactical reload retains rounds already loaded");
                Ready(a);Empty(a);a.TryEnemyReload();Advance(a,.6f);float pausedClock=a.actionClock;g.SetState(SFState.Paused);g.enabled=true;yield return new WaitForSeconds(.15f);g.enabled=false;
                Check(a.Reloading&&a.actionClock==pausedClock&&a.EnemyAmmo.Loaded==0,id+" real game pause freezes enemy reload");
                g.RetryCheckpoint();Check(a.Reloading&&a.actionClock==pausedClock&&a.EnemyAmmo.Total==reserve,id+" checkpoint retry preserves opponent ammunition and reload progress");
                g.SetState(SFState.Menu);Check(g.StartDrive(),id+" vehicle sequence starts with preserved foot state");yield return new WaitForSeconds(.05f);g.EndDrive();
                Check(a.Reloading&&a.actionClock==pausedClock&&a.EnemyAmmo.Total==reserve,id+" vehicle entry and exit preserve enemy reload and rounds");
            }
            Check(!g.Fernando.HasEnemyFirearm&&!g.Stefanie.HasEnemyFirearm&&g.Actors.Where(a=>!a.ranged).All(a=>!a.HasEnemyFirearm),"hero and unarmed profiles do not enter enemy firearm accounting");
            var first=enemies[0];Ready(first);first.EnemyAmmo.Fire();var before=first.EnemyAmmo.Total;var other=g.Actors.First(a=>a.identity=="keel"&&a!=first);
            Check(other.EnemyAmmo.Total==18&&first.EnemyAmmo.Total==before,"same-identity enemies own separate round counts");
            string result="ENEMY FIREARMS QA COMPLETE: "+checks.Count(x=>x.StartsWith("PASS:"))+" passed, "+checks.Count(x=>x.StartsWith("FAIL:"))+" failed";Debug.Log(result);
            if(!Web){File.WriteAllLines(Path.Combine(qa,"enemy-firearms-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);}
            else {g.SetState(SFState.Paused);g.Notify(result);}
        }
    }
}
