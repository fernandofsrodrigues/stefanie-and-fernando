using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        public void RunClinchQA()
        {
            if(!DriveActive&&GetComponent<SFPairedClinchCheck>()==null)
            {SmokeMode=true;gameObject.AddComponent<SFPairedClinchCheck>();}
        }
    }
    public sealed class SFPairedClinchCheck:MonoBehaviour
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
            Check(g.TryGrapple(p,1.05f,true,enemy),p.identity+" "+facing+" captures matching Latch");
            p.Render(0);enemy.Render(0);
        }
        IEnumerator View(SFActor p,string name)
        {
            // SFGame.Update is disabled throughout this fixture; show the sampled pair without a pause overlay.
            g.SetState(SFState.Playing);p.Render(0);enemy.Render(0);
            g.Camera.orthographicSize=4;g.Camera.transform.position=new Vector3(p.X+p.facing*.45f,-.55f,-10);
            Debug.Log("CLINCH QA VIEW: "+name);
            if(!Web)
            {
                g.Capture("v0846-clinch-"+name);
#if !UNITY_WEBGL
                SFGaitCheck.CaptureActor(p,qa,name,1100);
#endif
            }
            yield return new WaitForSeconds(Web?1.5f:.04f);
            g.SetState(SFState.Playing);
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return new WaitForSeconds(.4f);g.StartGame();
            yield return new WaitForSeconds(.3f);g.enabled=false;enemy=g.Actors.First(a=>a.identity=="latch");
            qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Clinch-v0846"));
            if(!Web)Directory.CreateDirectory(qa);
            foreach(var p in new[]{g.Fernando,g.Stefanie})
            {
                var art=Resources.Load<SFArt>("SF/"+p.identity+"_clinch_latch");
                Check(art!=null&&art.frames.Length==4&&art.frames.All(x=>x!=null)&&art.frames.Distinct().Count()==4,p.identity+" complete paired sheet loads");
                foreach(int facing in new[]{1,-1})
                {
                    Ready(p,facing);
                    Check(p.UsePairedClinch&&p.CurrentSheet=="clinch_latch"&&p.CurrentFrame==0&&!enemy.visual.enabled,p.identity+" "+facing+" exactly one visible pair replaces separate opponent");
                    Check(p.visual.flipX==(facing<0)&&Mathf.Abs(enemy.X-p.X-facing*.85f)<.01f,p.identity+" "+facing+" mirrored artwork retains logical positions");
                    yield return View(p,p.identity+"-"+facing+"-hold");
                    float hp=enemy.health;
                    p.BeginAttack(SFAction.ClinchKnee);Advance(p,.18f);
                    Check(p.CurrentFrame==1&&enemy.health==hp,p.identity+" "+facing+" chambers before dealing damage");
                    yield return View(p,p.identity+"-"+facing+"-chamber");
                    Advance(p,.16f);
                    Check(p.UsePairedClinch&&p.CurrentFrame==2&&p.ClinchHits==1&&enemy.health<hp,p.identity+" "+facing+" visible knee and damage coincide");
                    yield return View(p,p.identity+"-"+facing+"-contact");
                    float contactHealth=enemy.health,clock=p.actionClock;g.SetState(SFState.Paused);
                    yield return new WaitForSeconds(.06f);
                    Check(p.actionClock==clock&&enemy.health==contactHealth&&p.UsePairedClinch,p.identity+" "+facing+" paused contact stays paired without repeat damage");
                    g.SetState(SFState.Playing);Advance(p,.15f);
                    Check(p.CurrentFrame==3&&enemy.health==contactHealth,p.identity+" "+facing+" recovery has no duplicate impact");
                    yield return View(p,p.identity+"-"+facing+"-recovery");
                    Advance(p,.35f);
                    Check(p.action==SFAction.Grapple&&p.CurrentFrame==0&&!enemy.visual.enabled,p.identity+" "+facing+" returns to connected grip");
                    for(int hit=0;hit<2;hit++){p.BeginAttack(SFAction.ClinchKnee);Advance(p,.8f);}
                    Check(p.GripTarget==null&&enemy.HeldBy==null&&enemy.action==SFAction.Knocked&&enemy.visual.enabled&&p.CurrentSheet!="clinch_latch",p.identity+" "+facing+" third knee restores independent knockdown art");
                    Ready(p,facing);p.ReleaseGrip();
                    Check(enemy.visual.enabled&&p.CurrentSheet!="clinch_latch",p.identity+" "+facing+" explicit release restores both immediately");
                }
                Ready(p);p.FieldUniform=true;p.Render(0);
                Check(!p.UsePairedClinch&&enemy.visual.enabled&&p.CurrentSheet!="clinch_latch",p.identity+" military wardrobe never changes into civilian paired art");
                p.Helmet=false;p.Render(0);Check(p.CurrentSheet!="clinch_latch"&&enemy.visual.enabled,p.identity+" bareheaded military variant retains independent rig");
                Ready(p);p.Armed=true;p.Render(0);Check(!p.UsePairedClinch&&enemy.visual.enabled,p.identity+" armed priority restores visible opponent");
                Ready(p);enemy.identity="silk";p.Render(0);Check(!p.UsePairedClinch&&enemy.visual.enabled,p.identity+" other opponent identity never replaced by Latch");
                Ready(p);enemy.boss=true;p.Render(0);Check(!p.UsePairedClinch&&enemy.visual.enabled,p.identity+" boss cannot inherit ordinary paired appearance");
                Ready(p);enemy.HeldBy=g.Partner;p.Render(0);enemy.Render(0);Check(!p.UsePairedClinch&&enemy.visual.enabled,p.identity+" changed ownership cannot hide victim");
                Ready(p);enemy.lane+=.5f;p.Render(0);Check(!p.UsePairedClinch&&enemy.visual.enabled,p.identity+" separated depth invalidates composite");
                Ready(p);enemy.transform.position+=Vector3.up;p.Render(0);Check(!p.UsePairedClinch&&enemy.visual.enabled,p.identity+" separated height invalidates composite");
                Ready(p);enemy.Teleport(new Vector2(17,0));p.Render(0);Check(!p.UsePairedClinch&&enemy.visual.enabled,p.identity+" separated position invalidates composite");
                Ready(p);var cover=new SFCover{footprint=new Rect(15.4f,p.lane-.3f,.1f,.6f),height=3,label="paired QA cover"};g.Covers.Add(cover);p.Render(0);
                Check(!p.UsePairedClinch&&enemy.visual.enabled,p.identity+" new solid cover suppresses composite");g.Covers.Remove(cover);
                Ready(p);p.invulnerable=0;p.Damage(1,enemy,true);
                Check(p.GripTarget==null&&enemy.visual.enabled&&p.CurrentSheet!="clinch_latch",p.identity+" incoming damage restores pair before next actor tick");
                Ready(p);enemy.invulnerable=0;enemy.Damage(2000,p,true);
                Check(!enemy.Alive&&enemy.visual.enabled&&!p.UsePairedClinch,p.identity+" defeat cannot leave victim hidden");
                Ready(p);p.GripTime=.001f;Advance(p,.02f);Check(enemy.visual.enabled&&!p.UsePairedClinch,p.identity+" grip timeout restores both");
                Ready(p);p.Tick(.02f,new SFCommand{guard=true});Check(p.GripTarget==null&&enemy.visual.enabled&&!p.UsePairedClinch,p.identity+" guard escape restores both");
                Ready(p);Check(g.ThrowEnemy(p),p.identity+" paired grip can throw");
                Check(enemy.visual.enabled&&enemy.ThrowClock>0&&!p.UsePairedClinch,p.identity+" throw immediately uses independent thrown body");
                Ready(p);Check(p.TryGroundSequence(),p.identity+" paired grip transitions into six-hit ground sequence");p.Render(0);enemy.Render(0);
                Check(p.GroundSequence&&p.UsePairedGround&&p.CurrentSheet=="takedown_latch"&&!enemy.visual.enabled,p.identity+" ground sequence transfers visible ownership to paired takedown");
                Advance(p,1.95f);Check(p.GroundHits==6,p.identity+" all six existing ground strikes retained");
                Advance(p,.4f);Check(!p.GroundSequence&&enemy.visual.enabled,p.identity+" ground sequence finishes without composite residue");
            }
            foreach(var a in g.Actors){a.ReleaseGrip();a.Render(0);}
            Check(g.Actors.Where(a=>!a.hero).All(a=>a.visual.enabled),"no hidden opponents remain after all pair transitions");
            string result="CLINCH QA COMPLETE: "+checks.Count(x=>x.StartsWith("PASS:"))+" passed, "+checks.Count(x=>x.StartsWith("FAIL:"))+" failed";Debug.Log(result);
            if(!Web){File.WriteAllLines(Path.Combine(qa,"clinch-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);}
            else {g.SetState(SFState.Paused);g.Notify(result);}
        }
    }
}
