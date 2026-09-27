#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed class SFReloadCheck:MonoBehaviour
    {
        SFGame g;SFActor enemy;string qa;readonly List<string> checks=new List<string>();
        void Check(bool ok,string label){checks.Add((ok?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void Advance(SFActor p,float seconds,SFCommand command=default)
        {for(float t=0;t<seconds-.0001f;t+=.01f)p.Tick(Mathf.Min(.01f,seconds-t),command);}
        void Ready(SFActor p)
        {
            p.ReleaseGrip();p.action=SFAction.Idle;p.cooldown=0;p.health=p.maxHealth;p.invulnerable=0;p.Armed=p.Rifle=true;
            p.ResetWeapons();for(int i=0;i<7;i++)p.RifleAmmo.Fire();p.Teleport(new Vector2(7,0));p.lane=-.85f;p.Crouching=p.Aiming=false;
            p.Tick(.01f,default);g.SmokeCommand=default;
        }
        void Begin(SFActor p){p.Tick(.01f,new SFCommand{reload=true});p.Render(0);}
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Reload-v0846"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.4f);g.StartGame();
            foreach(var a in g.Actors){a.Teleport(new Vector2(a.hero?7:125,0));a.cooldown=999;a.lane=-.85f;}
            yield return new WaitForSeconds(.3f);g.SetState(SFState.Paused);enemy=g.Actors.First(a=>!a.Friendly&&!a.boss);
            foreach(var item in g.Pickups)item.visual.enabled=false;
            Check(SFReloadAnimation.Frame(-1,2)==0&&SFReloadAnimation.Frame(99,2)==3,"reload art clamps before and beyond its clock");
            Check(SFReloadAnimation.Frame(.399f,2)==0&&SFReloadAnimation.Frame(.40f,2)==1,"magazine removal starts at one fifth of reload duration");
            Check(SFReloadAnimation.Frame(.959f,2)==1&&SFReloadAnimation.Frame(.96f,2)==2,"magazine seating follows the removal phase");
            Check(SFReloadAnimation.Frame(1.559f,2)==2&&SFReloadAnimation.Frame(1.56f,2)==3,"ready recovery occupies the final reload segment");
            Check(SFReloadAnimation.Frame(1,0)==3,"invalid zero duration cannot divide by zero");
            foreach(var p in new[]{g.Fernando,g.Stefanie})
            {
                g.Selected=p==g.Fernando?0:1;g.Partner.Teleport(new Vector2(110,0));
                var art=Resources.Load<SFArt>("SF/"+p.identity+"_field_rifle_reload");
                Check(art!=null&&art.frames.Length==8&&art.frames.All(f=>f!=null),p.identity+" imports both four-phase headgear rows");
                foreach(bool helmet in new[]{true,false})
                {
                    p.FieldUniform=true;p.Helmet=helmet;string id=p.identity+" "+(helmet?"helmet":"bare");
                    foreach(int face in new[]{1,-1})
                    {
                        Ready(p);p.facing=face;int reserve=p.RifleAmmo.Reserve,total=p.RifleAmmo.Total,sidearm=p.SidearmAmmo.Total,buddy=g.Partner.TotalAmmo;
                        Begin(p);float duration=p.attackDuration;
                        Check(p.Reloading&&p.CurrentSheet=="field_rifle_reload"&&p.visual.enabled,id+" begins authored reload through the shared input command "+face);
                        Check(Mathf.Abs(duration-(p.identity=="fernando"?2.15f:2.4f))<.001f,id+" preserves its signature rifle duration "+face);
                        float[] phases={.05f,.35f,.60f,.90f};
                        for(int i=0;i<phases.Length;i++)
                        {
                            Advance(p,duration*phases[i]-p.actionClock);p.Render(0);
                            Check(p.CurrentSheet=="field_rifle_reload"&&p.CurrentFrame==(helmet?0:4)+i&&p.visual.flipX==(face<0),id+" phase "+i+" facing "+face);
                            Check(p.Reloading&&p.RifleAmmo.Loaded==13&&p.RifleAmmo.Reserve==reserve&&p.SidearmAmmo.Total==sidearm&&g.Partner.TotalAmmo==buddy,id+" phase "+i+" transfers no ammunition early "+face);
                            if(face>0)SFGaitCheck.CaptureActor(p,qa,p.identity+"-"+(helmet?"helmet":"bare")+"-"+i,1120);
                        }
                        float lane=p.lane,clock=p.actionClock;
                        p.Tick(.01f,new SFCommand{reload=true,shoot=true,punch=true,kick=true,move=Vector2.up,jump=true,aim=true});p.Render(0);
                        Check(p.Reloading&&p.actionClock>clock&&p.RifleAmmo.Loaded==13&&p.lane==lane&&p.CurrentSheet=="field_rifle_reload",id+" repeated reload and combat/depth input cannot restart or replace reload "+face);
                        Advance(p,duration-p.actionClock+.02f);p.Render(0);
                        Check(!p.Reloading&&p.RifleAmmo.Loaded==20&&p.RifleAmmo.Reserve==reserve-7&&p.RifleAmmo.Total==total&&p.CurrentSheet=="field_rifle",id+" completion loads only missing rounds and returns to rifle pose "+face);
                    }
                    var cancels=new[]{new SFCommand{toggleArmed=true},new SFCommand{cycleWeapon=true},new SFCommand{weapon=1}};
                    for(int i=0;i<cancels.Length;i++)
                    {
                        Ready(p);Begin(p);Advance(p,p.attackDuration*.4f);int total=p.RifleAmmo.Total;
                        p.Tick(.01f,cancels[i]);Advance(p,3);p.Render(0);
                        Check(!p.Reloading&&p.RifleAmmo.Loaded==13&&p.RifleAmmo.Total==total&&p.CurrentSheet!="field_rifle_reload"&&(i==0?!p.Armed:p.Armed&&!p.Rifle),id+" weapon cancellation "+i+" removes reload art without free ammunition");
                    }
                    Ready(p);Begin(p);Advance(p,.5f);p.Damage(1,enemy,true);p.Render(0);
                    Check(p.action==SFAction.Hurt&&p.CurrentSheet!="field_rifle_reload",id+" hurt immediately interrupts reload presentation");
                    Advance(p,3);Check(p.RifleAmmo.Loaded==13&&!p.Reloading,id+" interrupted reload cannot complete later");
                    Ready(p);Begin(p);p.KnockDown();p.Render(0);
                    Check(p.action==SFAction.Knocked&&p.CurrentSheet!="field_rifle_reload"&&p.RifleAmmo.Loaded==13,id+" knockdown owns presentation without refilling");
                    Ready(p);Begin(p);Advance(p,.5f);float frozen=p.actionClock;yield return new WaitForSeconds(.12f);
                    Check(p.actionClock==frozen&&p.Reloading,id+" pause freezes reload animation and magazine accounting");
                    p.Helmet=!helmet;p.Render(0);
                    Check(p.CurrentFrame==(!helmet?0:4)+SFReloadAnimation.Frame(p.actionClock,p.attackDuration),id+" current helmet selection controls the reload row");p.Helmet=helmet;
                    Ready(p);p.ResetWeapons();Check(!p.TryReload(),id+" full magazine cannot enter a cosmetic reload");
                    p.RifleAmmo.Clear();Check(!p.TryReload(),id+" empty reserve cannot begin reload");
                    p.RifleAmmo.Supply(3);Begin(p);Advance(p,p.attackDuration+.02f);
                    Check(p.RifleAmmo.Loaded==3&&p.RifleAmmo.Reserve==0&&!p.Reloading,id+" short reserve finishes with exactly three rounds");
                    Ready(p);p.Rifle=false;p.SidearmAmmo.Fire();Begin(p);p.Render(0);
                    Check(p.Reloading&&p.CurrentSheet!="field_rifle_reload",id+" pistol reload cannot borrow rifle art");
                    Ready(p);p.FieldUniform=false;Begin(p);p.Render(0);
                    Check(p.Reloading&&p.CurrentSheet=="civil_rifle",id+" civilian rifle preserves its existing outfit fallback");p.FieldUniform=true;
                }
                Ready(p);p.FieldUniform=true;p.Helmet=true;for(int i=0;i<13;i++)p.RifleAmmo.Fire();
                g.Selected=p==g.Fernando?1:0;g.Player.Teleport(new Vector2(10,0));enemy.Teleport(new Vector2(9,0));enemy.ranged=true;enemy.lane=p.lane;
                var ai=g.TacticalPartnerCommand();p.Tick(.01f,ai);p.Render(0);
                Check(ai.reload&&p.Reloading&&p.CurrentSheet=="field_rifle_reload",p.identity+" teammate AI uses the same authored reload state");
                Advance(p,p.attackDuration+.02f);Check(p.RifleAmmo.Loaded==20&&p.RifleAmmo.Reserve==40,p.identity+" AI reload consumes only its own reserve");
                enemy.ranged=false;enemy.Teleport(new Vector2(125,0));
            }
            File.WriteAllLines(Path.Combine(qa,"reload-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
