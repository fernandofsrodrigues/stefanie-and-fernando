#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed class SFAerialCheck:MonoBehaviour
    {
        SFGame g;SFActor enemy;string qa;readonly List<string> checks=new List<string>();
        void Check(bool pass,string label){checks.Add((pass?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void Position(SFActor p,float x,float height,float lane=-.85f)
        {p.motor.Body.position=new Vector2(x,height);p.transform.position=new Vector3(x,height,0);p.motor.Body.linearVelocity=Vector2.zero;p.lane=lane;}
        void Reset(SFActor p)
        {
            foreach(var a in g.Actors){a.ReleaseGrip();a.Teleport(new Vector2(120,0));a.action=SFAction.Idle;a.cooldown=999;a.invulnerable=0;a.health=a.maxHealth=1000;a.lane=-.85f;}
            p.Teleport(new Vector2(7,1));p.action=SFAction.Idle;p.cooldown=0;p.Armed=false;p.FieldUniform=false;p.Helmet=true;p.facing=1;p.lane=-.85f;
            Position(enemy,9,0);g.SmokeCommand=default;
        }
        void StartKick(SFActor p)
        {p.Teleport(new Vector2(7,1));p.lane=-.85f;p.cooldown=0;p.action=SFAction.Idle;p.BeginAttack(SFAction.Kick);enemy.invulnerable=0;enemy.action=SFAction.Idle;enemy.health=1000;Position(enemy,9,0);}
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Aerial-v0846"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.4f);g.StartGame();yield return new WaitForSeconds(.2f);g.SetState(SFState.Paused);
            enemy=g.Actors.First(a=>!a.Friendly&&!a.boss);
            Check(SFAerialKick.Sweep(new Vector3(0,0,1),new Vector3(6,0,1),1,new Vector3(4,0,0),out var contact)&&contact>0&&contact<1,"sweep catches a target crossed between widely spaced samples");
            Check(SFAerialKick.Sweep(new Vector3(6,0,1),new Vector3(0,0,1),-1,new Vector3(2,0,0),out _),"left-facing sweep has matching reach");
            Check(!SFAerialKick.Sweep(Vector3.up,Vector3.up,1,new Vector3(-1,1,0),out _),"rear target is outside forward contact volume");
            Check(!SFAerialKick.Sweep(new Vector3(0,0,1),new Vector3(5,0,1),1,new Vector3(3,1,0),out _),"sweep excludes a different depth lane");
            Check(!SFAerialKick.Sweep(new Vector3(0,0,3),new Vector3(5,0,3),1,new Vector3(3,0,0),out _),"high overflight cannot hit a ground opponent");
            Check(!SFAerialKick.Sweep(Vector3.zero,Vector3.right,1,new Vector3(5,0,0),out _),"sweep stops at bounded forward reach");
            Check(SFAerialKick.Frame(.119f)==1&&SFAerialKick.Frame(.12f)==2&&SFAerialKick.Frame(.379f)==2&&SFAerialKick.Frame(.38f)==3,"extension pose exactly covers the contact window");
            Check(SFAerialKick.Frame(2)==3&&SFAerialKick.Frame(2,.16f)==4&&SFAerialKick.Frame(2,.05f)==5,"descent holds retraction until a real landing selects recovery poses");
            foreach(var p in new[]{g.Fernando,g.Stefanie})
            {
                // A taller warmup spawn survives physics catch-up after synchronous PNG capture work.
                g.Selected=p==g.Fernando?0:1;Reset(p);p.Teleport(new Vector2(7,4));g.SetState(SFState.Playing);yield return new WaitForSeconds(.08f);g.SetState(SFState.Paused);
                StartKick(p);Check(p.FlyingKick&&p.AerialUsed,p.identity+" begins one committed airborne kick; grounded="+p.motor.IsGrounded+" height="+p.Height);
                p.Tick(.10f,default);Check(enemy.health==1000,p.identity+" chamber phase deals no early damage");
                p.Tick(.03f,default);float after=enemy.health;
                Check(after<1000&&enemy.action==SFAction.Knocked&&enemy.X>9,p.identity+" extended foot connects, knocks down and makes space");
                enemy.invulnerable=0;Position(enemy,9,0);p.Tick(.15f,default);
                Check(enemy.health==after&&p.AerialHits.Count==1,p.identity+" same enemy takes damage only once per flying kick");
                StartKick(p);Position(enemy,13,0);p.Tick(.14f,default);Position(enemy,9,0);p.Tick(.08f,default);
                Check(enemy.health<1000,p.identity+" late arrival during extension can still connect");
                StartKick(p);Position(enemy,13,0);p.Tick(.39f,default);Position(enemy,9,0);p.Tick(.06f,default);
                Check(enemy.health==1000,p.identity+" retraction cannot deal a late hit");
                StartKick(p);Position(enemy,12,0);p.Tick(.10f,default);Position(p,13,1);p.Tick(.30f,default);
                Check(enemy.health<1000,p.identity+" active fraction of a large tick sweeps instead of tunnelling");
                StartKick(p);Position(enemy,6,0);p.Tick(.2f,default);Check(enemy.health==1000,p.identity+" no hit behind launch facing");
                StartKick(p);Position(enemy,9,0,.3f);p.Tick(.2f,default);Check(enemy.health==1000,p.identity+" no hit across depth lanes");
                StartKick(p);
                p.Teleport(new Vector2(7,3));p.lane=-.85f;p.action=SFAction.Idle;p.BeginAttack(SFAction.Kick);enemy.health=1000;enemy.invulnerable=0;p.Tick(.2f,default);
                Check(enemy.health==1000,p.identity+" elevation protects an opponent below the feet");
                StartKick(p);var wall=new SFCover{footprint=new Rect(8,-1.3f,.4f,.9f),height=4,label="QA wall"};g.Covers.Add(wall);p.Tick(.2f,default);
                Check(enemy.health==1000,p.identity+" solid cover blocks airborne contact");g.Covers.Remove(wall);
                StartKick(p);var friend=g.Partner;Position(friend,8,0);float friendlyHealth=friend.health;p.Tick(.2f,default);
                Check(friend.health==friendlyHealth,p.identity+" flying kick does not damage teammate");friend.Teleport(new Vector2(120,0));
                StartKick(p);enemy.HeldBy=friend;friend.GripTarget=enemy;p.Tick(.2f,default);
                Check(enemy.health==1000,p.identity+" respects teammate-owned clinch");friend.ReleaseGrip();
                StartKick(p);p.invulnerable=0;p.Damage(1,enemy,true);p.Tick(.2f,default);
                Check(enemy.health==1000&&!p.FlyingKick,p.identity+" damage interrupts the kick without granting invulnerability");
                StartKick(p);p.Tick(.1f,default);p.BeginAttack(SFAction.Kick);
                Check(Mathf.Abs(p.actionClock-.1f)<.001f,p.identity+" repeat input cannot restart startup in the same jump");
                p.Tick(.55f,default);p.cooldown=0;p.BeginAttack(SFAction.Kick);
                Check(p.FlyingKick&&p.actionClock>.6f,p.identity+" descent cannot be cancelled into a second airborne kick");
                StartKick(p);p.Tick(.15f,new SFCommand{move=new Vector2(-1,1)});
                Check(p.facing==1&&Mathf.Abs(p.lane+.85f)<.001f,p.identity+" facing and lane stay committed during the strike");
                p.Teleport(new Vector2(7,1));Check(!p.AerialUsed&&p.AerialHits.Count==0,p.identity+" checkpoint teleport clears stale sweep state");
                float[] times={0,.07f,.13f,.39f};
                for(int outfit=0;outfit<3;outfit++)
                {
                    p.FieldUniform=outfit>0;p.Helmet=outfit!=2;
                    string label=outfit==0?"civil":outfit==1?"field-helmet":"field-bare";
                    string expected=outfit==0?"aerial":outfit==1?"field_aerial":"field_aerial_bare";
                    foreach(int direction in new[]{1,-1})
                    {
                        p.facing=direction;StartKick(p);
                        for(int i=0;i<times.Length;i++)
                        {
                            p.actionClock=times[i];p.Render(0);
                            Check(p.CurrentSheet==expected&&p.CurrentFrame==i&&p.visual.flipX==(direction<0),p.identity+" "+label+" direction "+direction+" aerial phase "+i);
                            if(direction==1)SFGaitCheck.CaptureActor(p,qa,p.identity+"-"+label+"-"+i,1120);
                        }
                        p.Tick(.01f,new SFCommand{toggleArmed=true,cycleWeapon=true,reload=true,move=Vector2.up});p.Render(0);
                        Check(p.FlyingKick&&!p.Armed&&p.CurrentSheet==expected&&Mathf.Abs(p.lane+.85f)<.001f,p.identity+" "+label+" aerial commitment wins over weapon, reload and depth input "+direction);
                    }
                }
            }
            foreach(var player in new[]{g.Fernando,g.Stefanie})
            for(int outfit=0;outfit<3;outfit++)
            {
                g.Selected=player==g.Fernando?0:1;Reset(player);player.FieldUniform=outfit>0;player.Helmet=outfit!=2;
                string label=outfit==0?"civil":outfit==1?"field-helmet":"field-bare";
                string expected=outfit==0?"aerial":outfit==1?"field_aerial":"field_aerial_bare";
                string id=player.identity+" "+label;
                Position(player,7,0);Position(enemy,120,0);g.SetState(SFState.Playing);
                yield return new WaitForSeconds(.15f);g.SmokeCommand=new SFCommand{move=Vector2.right,run=true,jump=true};yield return new WaitForSeconds(.14f);
                g.SmokeCommand=new SFCommand{kick=true};yield return null;g.SmokeCommand=default;float start=player.X;yield return new WaitForSeconds(.25f);
                Check(player.X-start>.75f&&player.FlyingKick,id+" live physics retains kick travel after move release");
                float clock=player.actionClock;g.SetState(SFState.Paused);yield return new WaitForSeconds(.1f);
                Check(player.actionClock==clock,id+" pause freezes active aerial timing");g.SetState(SFState.Playing);
                float timeout=0;while(player.AerialLandingRemaining<=0&&timeout<1.5f){timeout+=Time.deltaTime;yield return null;}
                Check(player.AerialLandingRemaining>0&&!player.AerialUsed,id+" physical landing begins recovery and unlocks next jump");
                g.SetState(SFState.Paused);player.Render(0);
                Check(player.CurrentSheet==expected&&player.CurrentFrame==4,id+" actual first landing frame keeps outfit and helmet selection");
                SFGaitCheck.CaptureActor(player,qa,player.identity+"-"+label+"-4",1120);
                player.cooldown=0;player.Tick(.02f,new SFCommand{punch=true,move=Vector2.right,jump=true});
                Check(player.action!=SFAction.Punch&&player.AerialLandingRemaining>0,id+" landing recovery blocks immediate attack and jump");
                player.Tick(Mathf.Max(0,player.AerialLandingRemaining-.045f),default);player.Render(0);
                Check(player.CurrentSheet==expected&&player.CurrentFrame==5,id+" late landing recovery selects guard phase");
                SFGaitCheck.CaptureActor(player,qa,player.identity+"-"+label+"-5",1120);
                player.Tick(.2f,default);player.cooldown=0;player.Tick(.02f,new SFCommand{punch=true});
                Check(player.action==SFAction.Punch,id+" ground combo available after landing recovery");
                player.action=SFAction.Idle;player.cooldown=0;player.Armed=true;player.Rifle=true;player.ResetWeapons();player.Magazine.Fire();
                Check(player.TryReload(),id+" rifle can reload after aerial recovery");player.Render(0);
                Check(player.CurrentSheet==(outfit==0?"civil_rifle":"field_rifle_reload")&&player.Reloading,id+" reload presentation overrides stale aerial state");
                player.action=SFAction.Idle;player.cooldown=0;player.BeginAttack(SFAction.Shoot);player.Render(0);
                Check(player.CurrentSheet==(outfit==0?"civil_rifle":"field_rifle")&&player.action==SFAction.Shoot,id+" firearm action overrides stale aerial state");
                player.invulnerable=0;player.Damage(1,enemy,true);player.Render(0);
                Check(player.action==SFAction.Hurt&&player.CurrentSheet!=expected,id+" hurt presentation overrides aerial art");
            }
            File.WriteAllLines(Path.Combine(qa,"aerial-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
