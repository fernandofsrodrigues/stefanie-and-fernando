using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        public void RunTakedownQA()
        {
            if(!DriveActive&&GetComponent<SFPairedTakedownCheck>()==null)
            {SmokeMode=true;gameObject.AddComponent<SFPairedTakedownCheck>();}
        }
    }
    public sealed class SFPairedTakedownCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();
        SFGame g;SFActor enemy;string qa;
        bool Web=>Application.platform==RuntimePlatform.WebGLPlayer;
        void Check(bool ok,string name){checks.Add((ok?"PASS: ":"FAIL: ")+name);Debug.Log(checks.Last());}
        void Advance(SFActor p,float seconds,SFCommand c=default)
        {
            for(float t=0;t<seconds-.000001f;)
            {float dt=Mathf.Min(.02f,seconds-t);p.Tick(dt,c);enemy.Tick(dt,default);t+=dt;}
        }
        void Ready(SFActor p,int facing=1)
        {
            g.SetState(SFState.Playing);g.Selected=p==g.Fernando?0:1;
            foreach(var a in g.Actors){a.ReleaseGrip();a.HeldBy=null;if(a!=p&&a!=enemy){a.Teleport(new Vector2(120,0));a.cooldown=999;}}
            p.FieldUniform=false;p.Armed=p.Rifle=false;p.action=SFAction.Idle;p.health=1000;p.cooldown=p.invulnerable=0;p.ResetCombo();
            p.Teleport(new Vector2(15,0));p.lane=-.85f;p.facing=facing;
            enemy.identity="latch";enemy.boss=false;enemy.health=enemy.maxHealth=1000;enemy.invulnerable=enemy.cooldown=0;enemy.action=SFAction.Idle;
            enemy.Teleport(new Vector2(15+facing*.95f,0));enemy.lane=p.lane;
            Advance(p,.65f);p.cooldown=0;
            Check(p.TryGroundSequence(),p.identity+" "+facing+" enters takedown from close free movement");
            p.Render(0);enemy.Render(0);
        }
        IEnumerator View(SFActor p,string name)
        {
            // Freeze only this QA fixture while sampling; actor clocks advance explicitly between views.
            p.Render(0);enemy.Render(0);g.Camera.orthographicSize=4;g.Camera.transform.position=new Vector3(p.X+p.facing*.45f,-.55f,-10);
            Debug.Log("TAKEDOWN QA VIEW: "+name);
            if(!Web)
            {
                g.Capture("v0846-takedown-"+name);
#if !UNITY_WEBGL
                SFGaitCheck.CaptureActor(p,qa,name,1100);
#endif
            }
            yield return new WaitForSeconds(Web?1.4f:.04f);
        }
        void Restored(SFActor p,string reason)
        {
            Check(!p.GroundSequence&&p.GripTarget==null&&enemy.HeldBy==null&&enemy.visual.enabled&&!p.UsePairedBody&&p.CurrentSheet!="takedown_latch"&&p.CurrentSheet!="ground_straight_latch",p.identity+" "+reason+" restores two independent bodies immediately");
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return new WaitForSeconds(.4f);g.StartGame();
            yield return new WaitForSeconds(.3f);g.enabled=false;enemy=g.Actors.First(a=>a.identity=="latch");
            qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Takedown-v0846"));if(!Web)Directory.CreateDirectory(qa);
            foreach(var p in new[]{g.Fernando,g.Stefanie})
            {
                var sequence=Resources.Load<SFArt>("SF/"+p.identity+"_takedown_latch");
                var straight=Resources.Load<SFArt>("SF/"+p.identity+"_ground_straight_latch");
                Check(sequence!=null&&sequence.frames.Length==6&&sequence.frames.All(s=>s!=null)&&straight!=null&&straight.frames.Length==1,p.identity+" complete takedown and separate opposite-arm sprite load");
                foreach(int facing in new[]{1,-1})
                {
                    Ready(p,facing);float hp=enemy.health,damage=g.OutgoingDamage(p,7,SFAction.GroundStrike);
                    Check(p.UsePairedGround&&p.CurrentSheet=="takedown_latch"&&p.CurrentFrame==0&&!enemy.visual.enabled,p.identity+" "+facing+" connected double-leg entry replaces separate victim");
                    Check(p.visual.flipX==(facing<0)&&Mathf.Abs(enemy.X-p.X-facing*.85f)<.01f,p.identity+" "+facing+" mirrored pair preserves logical contact");
                    yield return View(p,p.identity+"-"+facing+"-entry");
                    Advance(p,.18f);Check(p.CurrentFrame==1&&enemy.health==hp,p.identity+" "+facing+" landing precedes damage");
                    if(facing==1)yield return View(p,p.identity+"-landing");
                    Advance(p,.16f);Check(p.CurrentFrame==2&&p.GroundHits==0,p.identity+" "+facing+" mount guard precedes first jab");
                    if(facing==1)yield return View(p,p.identity+"-mount");
                    for(int beat=0;beat<6;beat++)
                    {
                        Advance(p,SFGroundAnimation.FirstHit+beat*SFGroundAnimation.Interval-p.actionClock+.001f);
                        bool jab=beat%2==0;
                        Check(p.GroundHits==beat+1&&Mathf.Abs(enemy.health-(hp-damage*(beat+1)))<.01f,p.identity+" "+facing+" beat "+beat+" deals exactly one timed impact");
                        Check(p.CurrentSheet==(jab?"takedown_latch":"ground_straight_latch")&&p.CurrentFrame==(jab?3:0)&&!enemy.visual.enabled,p.identity+" "+facing+" beat "+beat+" uses distinct "+(jab?"jab":"straight")+" arm without duplicate victim");
                        if(beat<2)yield return View(p,p.identity+"-"+facing+(jab?"-jab":"-straight"));
                        float hitHealth=enemy.health;Advance(p,.16f);
                        Check(enemy.health==hitHealth&&p.CurrentSheet=="takedown_latch"&&p.CurrentFrame==(beat==5?5:2),p.identity+" "+facing+" beat "+beat+" recovery has no duplicate hit");
                    }
                    Check(p.GroundHits==6&&p.CurrentFrame==5&&p.GroundSequence,p.identity+" "+facing+" rises only after sixth contact");
                    if(facing==1)yield return View(p,p.identity+"-rise");
                    Advance(p,.4f);Restored(p,"completed sequence "+facing);
                    Advance(p,.3f,new SFCommand{move=Vector2.right});Check(!p.Busy&&p.GripTarget==null,p.identity+" "+facing+" can resume free movement");
                }
                foreach(var escape in new[]{new SFCommand{move=Vector2.left},new SFCommand{dash=true},new SFCommand{guard=true},new SFCommand{weapon=2}})
                {
                    Ready(p);Advance(p,.31f);p.Tick(.01f,escape);Restored(p,"escape input");Check(p.GroundHits==0,p.identity+" early escape applies no queued punch");
                }
                Ready(p);Advance(p,.76f);int hits=p.GroundHits;p.Tick(.01f,new SFCommand{jump=true});Restored(p,"mid-combo jump escape");
                Advance(p,.5f);Check(p.GroundHits==hits,p.identity+" canceled combo cannot deal deferred hits");
                Ready(p);Advance(p,.56f);p.invulnerable=0;p.Damage(1,enemy,true);Restored(p,"incoming damage");
                Ready(p);enemy.health=1;Advance(p,.52f);Restored(p,"opponent defeat");
                Ready(p);p.Teleport(new Vector2(10,0));Restored(p,"checkpoint teleport");
                Ready(p);enemy.lane+=.7f;Advance(p,.02f);Restored(p,"depth separation");
                Ready(p);enemy.Teleport(new Vector2(enemy.X,1));Advance(p,.02f);Restored(p,"height separation");
                Ready(p);var cover=new SFCover{footprint=new Rect(15.4f,p.lane-.3f,.1f,.6f),height=3,label="ground QA cover"};g.Covers.Add(cover);Advance(p,.02f);Restored(p,"new cover");g.Covers.Remove(cover);
                Ready(p);p.FieldUniform=true;p.Render(0);Check(!p.UsePairedGround&&p.CurrentSheet=="field_ground"&&enemy.visual.enabled,p.identity+" military outfit retains its own ground art");
                p.Helmet=false;p.Render(0);Check(!p.UsePairedGround&&p.CurrentFrame>=3&&enemy.visual.enabled,p.identity+" bareheaded military art retained");
                Ready(p);enemy.identity="silk";p.Render(0);Check(!p.UsePairedGround&&p.CurrentSheet=="ground_combo"&&enemy.visual.enabled,p.identity+" other opponent keeps independent identity");
                Ready(p);enemy.HeldBy=g.Partner;g.Partner.GripTarget=enemy;p.Render(0);Check(!p.UsePairedGround&&enemy.visual.enabled,p.identity+" mismatched owner cannot hide target");Advance(p,.02f);
                Check(!p.GroundSequence&&p.GripTarget==null&&enemy.HeldBy==g.Partner&&g.Partner.GripTarget==enemy,p.identity+" stale owner cancels itself without releasing another hero's grip");g.Partner.ReleaseGrip(true);
                Ready(p);p.ReleaseGrip();p.cooldown=0;Check(g.TryGrapple(p,1.1f,true,enemy),p.identity+" ordinary clinch setup");
                Check(!g.TryGrapple(p,1.1f,true)&&p.GripTarget==enemy&&enemy.HeldBy==p,p.identity+" repeated capture cannot orphan an existing owned opponent");p.ReleaseGrip();
            }
            foreach(var a in g.Actors){a.ReleaseGrip();a.Render(0);}
            Check(g.Actors.Where(a=>!a.hero).All(a=>a.visual.enabled),"all opponents remain visible after takedown transitions");
            string result="TAKEDOWN QA COMPLETE: "+checks.Count(x=>x.StartsWith("PASS:"))+" passed, "+checks.Count(x=>x.StartsWith("FAIL:"))+" failed";Debug.Log(result);
            if(!Web){File.WriteAllLines(Path.Combine(qa,"takedown-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);}
            else {g.SetState(SFState.Paused);g.Notify(result);}
        }
    }
}
