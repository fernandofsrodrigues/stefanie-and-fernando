#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed class SFBoxingCheck:MonoBehaviour
    {
        SFGame g;SFActor enemy;string qa;readonly List<string> checks=new List<string>();
        void Check(bool ok,string label){checks.Add((ok?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void Advance(SFActor p,float seconds){for(float t=0;t<seconds-.001f;t+=.005f)p.Tick(Mathf.Min(.005f,seconds-t),default);}
        void Target(SFActor p){enemy.Teleport(new Vector2(p.X+1.2f*p.facing,0));enemy.lane=p.lane;enemy.action=SFAction.Idle;enemy.health=enemy.maxHealth=1000;enemy.invulnerable=0;enemy.cooldown=999;}
        void Ready(SFActor p)
        {
            p.ReleaseGrip();p.Teleport(new Vector2(7,0));p.lane=-.85f;p.Armed=false;p.Rifle=false;p.ResetCombo();p.action=SFAction.Idle;p.cooldown=0;p.invulnerable=0;p.health=p.maxHealth;
            p.Crouching=p.Aiming=false;p.facing=1;Target(p);p.Tick(.01f,default);
        }
        void StartStrike(SFActor p,int step)
        {
            Ready(p);
            for(int i=0;i<step;i++){Target(p);p.Tick(.01f,new SFCommand{punch=true});Advance(p,p.attackDuration+.08f);}
            Target(p);p.Tick(.01f,new SFCommand{punch=true});
        }
        bool Boxing(SFActor p)=>p.CurrentSheet=="field_jab"||p.CurrentSheet=="field_straight";
        void Capture(SFActor p,string name)
        {
            var camera=new GameObject("Boxing QA camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.85f;
            camera.transform.position=new Vector3(p.X,p.Height-3+p.lane+1.45f,-10);camera.clearFlags=CameraClearFlags.SolidColor;
            var texture=new RenderTexture(960,720,24);camera.targetTexture=texture;camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;
            var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(qa,name+".png"),pixels.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Destroy(texture);Destroy(pixels);Destroy(camera.gameObject);
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Boxing-v0846"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.4f);g.StartGame();
            foreach(var a in g.Actors){a.Teleport(new Vector2(a.hero?7:125,0));a.cooldown=999;a.lane=-.85f;}
            yield return new WaitForSeconds(.3f);g.SetState(SFState.Paused);enemy=g.Actors.First(a=>!a.Friendly&&!a.boss);
            foreach(var p in new[]{g.Fernando,g.Stefanie})
            {
                (p==g.Fernando?g.Stefanie:g.Fernando).Teleport(new Vector2(110,0));g.Selected=p==g.Fernando?0:1;
                foreach(bool helmet in new[]{true,false})foreach(int step in new[]{0,1})
                {
                    p.FieldUniform=true;p.Helmet=helmet;var move=SFCombo.Get(p.identity,step);string sheet=step==0?"field_jab":"field_straight";
                    string id=p.identity+"-"+sheet+"-"+(helmet?"helmet":"bare");int row=helmet?0:3;var art=Resources.Load<SFArt>("SF/"+p.identity+"_"+sheet);
                    Check(art!=null&&art.frames.Length==6&&art.frames.All(f=>f!=null),id+" has six complete authored sprites");
                    Check(SFStrikeAnimation.ThreePoseFrame(move.contact-.001f,move.contact,move.duration)==0&&SFStrikeAnimation.ThreePoseFrame(move.contact,move.contact,move.duration)==1,id+" contact frame starts exactly at the hit event");
                    StartStrike(p,step);Check(p.ComboAttack&&p.ComboMove.kind==move.kind&&p.ActiveComboStep==step,id+" reached through connected combo input");
                    float hp=enemy.health;Advance(p,move.contact-.005f);p.Render(0);
                    Check(enemy.health==hp&&p.CurrentSheet==sheet&&p.CurrentFrame==row,id+" preparation cannot damage early");
                    Advance(p,.01f);p.Render(0);
                    Check(enemy.health<hp&&p.NextComboStep==step+1&&p.CurrentSheet==sheet&&p.CurrentFrame==row+1,id+" hit event selects contact and advances the chain");
                    float after=enemy.health;enemy.invulnerable=0;Advance(p,.09f);p.Render(0);
                    Check(enemy.health==after&&p.CurrentFrame==row+2,id+" recovery does not duplicate damage");
                    foreach(int face in new[]{1,-1})
                    {
                        p.facing=face;
                        for(int phase=0;phase<3;phase++)
                        {
                            p.actionClock=phase==0?move.contact*.5f:phase==1?move.contact+.002f:move.contact+(move.duration-move.contact)*.7f;p.Render(0);
                            Check(p.CurrentSheet==sheet&&p.CurrentFrame==row+phase&&p.visual.flipX==(face<0)&&p.visual.sprite==art.frames[row+phase],id+" phase "+phase+" facing "+face);
                            if(face>0){enemy.Teleport(new Vector2(125,0));Capture(p,id+"-"+phase);}
                        }
                    }
                    StartStrike(p,step);p.Helmet=!helmet;p.Render(0);Check(p.CurrentFrame==(!helmet?0:3)&&p.CurrentSheet==sheet,id+" changed helmet selects the matching row");p.Helmet=helmet;
                    p.FieldUniform=false;p.Render(0);Check(p.CurrentSheet=="strike",id+" civilian costume retains its own strike sheet");p.FieldUniform=true;
                    StartStrike(p,step);p.invulnerable=0;p.Damage(1,enemy,true);p.Render(0);Check(!Boxing(p)&&!p.ComboAttack,id+" hurt cancels the boxing override");
                    StartStrike(p,step);p.Tick(.01f,new SFCommand{weapon=2});p.Render(0);Check(!p.Armed&&p.CurrentSheet==sheet,id+" committed strike defers weapon changes");
                    Advance(p,move.duration+.08f);p.Tick(.01f,new SFCommand{weapon=2});p.Render(0);Check(p.Armed&&p.Rifle&&!Boxing(p),id+" completed strike permits rifle selection");
                    p.action=SFAction.Idle;p.cooldown=0;p.RifleAmmo.Fire();p.TryReload();p.Render(0);Check(p.Reloading&&!Boxing(p),id+" reload has weapon art priority");
                    StartStrike(p,step);Advance(p,move.duration+.08f);Target(p);p.Tick(.01f,new SFCommand{punch=true});p.Render(0);
                    var next=SFCombo.Get(p.identity,step+1);Check(p.ComboMove.kind==next.kind&&(next.kind==SFStrike.Hook?p.CurrentSheet.Contains("hook"):p.CurrentSheet==(next.kind==SFStrike.Jab?"field_jab":"field_straight")),id+" continues into the correct next authored move");
                }
                p.FieldUniform=true;p.Helmet=true;StartStrike(p,0);enemy.Teleport(new Vector2(125,0));Advance(p,.4f);
                p.Tick(.01f,new SFCommand{punch=true});p.Render(0);Check(p.ActiveComboStep==0&&p.CurrentSheet=="field_jab",p.identity+" missed jab restarts the jab instead of granting a straight");
                StartStrike(p,1);Advance(p,1.3f);Target(p);p.Tick(.01f,new SFCommand{punch=true});p.Render(0);Check(p.ActiveComboStep==0&&p.CurrentSheet=="field_jab",p.identity+" expired combo returns to jab");
                StartStrike(p,1);p.KnockDown();p.Render(0);Check(!Boxing(p)&&!p.ComboAttack,p.identity+" knockdown takes visual priority");
                Ready(p);p.BeginAttack(SFAction.ClinchKnee);p.Render(0);Check(!Boxing(p),p.identity+" clinch knee keeps its paired action pose");
                Ready(p);p.Armed=true;p.Rifle=true;p.BeginAttack(SFAction.Shoot);p.Render(0);Check(!Boxing(p),p.identity+" firing keeps signature rifle art");
            }
            File.WriteAllLines(Path.Combine(qa,"boxing-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
