#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed class SFRoundhouseCheck:MonoBehaviour
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
        void Finisher(SFActor p,bool kick)
        {
            Ready(p);
            for(int i=0;i<SFCombo.Length(p.identity)-1;i++){Target(p);p.Tick(.01f,new SFCommand{punch=true});Advance(p,p.attackDuration+.08f);}
            Target(p);p.Tick(.01f,new SFCommand{punch=!kick,kick=kick});
        }
        bool Roundhouse(SFActor p)=>p.CurrentSheet.Contains("roundhouse");
        void Capture(SFActor p,string name)
        {
            var camera=new GameObject("Roundhouse QA camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.85f;
            camera.transform.position=new Vector3(p.X,p.Height-3+p.lane+1.45f,-10);camera.clearFlags=CameraClearFlags.SolidColor;
            var texture=new RenderTexture(960,720,24);camera.targetTexture=texture;camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;
            var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(qa,name+".png"),pixels.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Destroy(texture);Destroy(pixels);Destroy(camera.gameObject);
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Roundhouse-v0846"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.4f);g.StartGame();
            foreach(var a in g.Actors){a.Teleport(new Vector2(a.hero?7:125,0));a.cooldown=999;a.lane=-.85f;}
            yield return new WaitForSeconds(.3f);g.SetState(SFState.Paused);enemy=g.Actors.First(a=>!a.Friendly&&!a.boss);
            foreach(var p in new[]{g.Fernando,g.Stefanie})
            {
                (p==g.Fernando?g.Stefanie:g.Fernando).Teleport(new Vector2(110,0));g.Selected=p==g.Fernando?0:1;
                var move=SFCombo.Get(p.identity,SFCombo.Length(p.identity)-1,true);
                Check(move.kind==SFStrike.Roundhouse&&move.contact==.30f&&move.duration==.66f&&move.damage==36&&move.reach==2.55f&&move.finisher,p.identity+" retains roundhouse combat balance");
                Check(SFStrikeAnimation.EightPoseFrame(move.contact-.001f,move.contact,move.duration)==2&&SFStrikeAnimation.EightPoseFrame(move.contact,move.contact,move.duration)==3,p.identity+" extension begins exactly at damage contact");
                foreach(int outfit in new[]{0,1,2})
                {
                    p.FieldUniform=outfit!=0;p.Helmet=outfit!=2;string sheet=outfit==0?"roundhouse":outfit==1?"field_roundhouse_helmet":"field_roundhouse_bare";
                    string id=p.identity+"-"+sheet;var art=Resources.Load<SFArt>("SF/"+p.identity+"_"+sheet);
                    Check(art!=null&&art.frames.Length==8&&art.frames.All(f=>f!=null),id+" has eight complete authored sprites");
                    bool kick=p==g.Fernando;Finisher(p,kick);
                    Check(p.ComboAttack&&p.ComboMove.kind==SFStrike.Roundhouse&&p.ActiveComboStep==SFCombo.Length(p.identity)-1,id+" reached through the hero's connected combo inputs");
                    float hp=enemy.health;Advance(p,move.contact-.005f);p.Render(0);
                    Check(enemy.health==hp&&p.CurrentSheet==sheet&&p.CurrentFrame==2,id+" chamber cannot damage before contact");
                    Advance(p,.01f);p.Render(0);
                    Check(enemy.health<hp&&p.NextComboStep==0&&enemy.action==SFAction.Knocked&&p.CurrentSheet==sheet&&p.CurrentFrame==3,id+" extension delivers the knockdown and resets the chain");
                    float after=enemy.health;enemy.invulnerable=0;Advance(p,.09f);p.Render(0);
                    Check(enemy.health==after&&p.CurrentFrame==4,id+" follow-through does not duplicate damage");
                    foreach(int face in new[]{1,-1})
                    {
                        p.facing=face;
                        for(int phase=0;phase<8;phase++)
                        {
                            p.actionClock=phase<3?(phase+.2f)*p.attackAt/3:p.attackAt+(phase-3+.2f)*(p.attackDuration-p.attackAt)/5;p.Render(0);
                            Check(p.CurrentSheet==sheet&&p.CurrentFrame==phase&&p.visual.flipX==(face<0)&&p.visual.sprite==art.frames[phase],id+" phase "+phase+" facing "+face);
                            if(face>0){enemy.Teleport(new Vector2(125,0));Capture(p,id+"-"+phase);}
                        }
                    }
                    Ready(p);p.Tick(.01f,new SFCommand{kick=true});p.Render(0);
                    Check(!p.ComboAttack&&!p.AerialAttack&&p.CurrentSheet==sheet&&p.attackAt==.32f&&p.attackDuration==.7f,id+" ordinary kick uses its own ground timing");
                    hp=enemy.health;Advance(p,.315f);p.Render(0);Check(enemy.health==hp&&p.CurrentFrame==2,id+" ordinary kick cannot damage during chamber");
                    Advance(p,.01f);p.Render(0);Check(enemy.health<hp&&p.CurrentFrame==3,id+" ordinary kick synchronizes extension and damage");
                    Finisher(p,kick);p.Helmet=!p.Helmet;p.Render(0);Check(p.CurrentSheet==(outfit==0?"roundhouse":p.Helmet?"field_roundhouse_helmet":"field_roundhouse_bare"),id+" changed headgear retains the appropriate costume");p.Helmet=outfit!=2;
                    p.FieldUniform=false;p.Render(0);Check(p.CurrentSheet=="roundhouse",id+" civilian clothes select the original civilian sheet");p.FieldUniform=outfit!=0;
                    Finisher(p,kick);p.invulnerable=0;p.Damage(1,enemy,true);p.Render(0);Check(!Roundhouse(p)&&!p.ComboAttack,id+" hurt cancels the ground kick override");
                    Finisher(p,kick);p.Tick(.01f,new SFCommand{weapon=2});p.Render(0);Check(!p.Armed&&p.CurrentSheet==sheet,id+" committed kick defers weapon changes");
                    Advance(p,move.duration+.08f);p.Tick(.01f,new SFCommand{weapon=2});p.Render(0);Check(p.Armed&&p.Rifle&&!Roundhouse(p),id+" recovery permits a rifle switch");
                    p.action=SFAction.Idle;p.cooldown=0;p.RifleAmmo.Fire();p.TryReload();p.Render(0);Check(p.Reloading&&!Roundhouse(p),id+" rifle reload retains its own art");
                    Ready(p);p.BeginAttack(SFAction.Kick);p.AerialAttack=true;p.Render(0);Check(!Roundhouse(p)&&p.CurrentSheet==(outfit==0?"aerial":p.Helmet?"field_aerial":"field_aerial_bare"),id+" aerial selection takes priority over the grounded override");
                }
                Finisher(p,true);enemy.Teleport(new Vector2(125,0));Advance(p,.8f);Check(enemy.health==1000&&p.NextComboStep==0,p.identity+" out-of-range finisher cannot hit and its combo window expires");
                Finisher(p,true);enemy.lane=p.lane+2;Advance(p,.4f);Check(enemy.health==1000,p.identity+" finisher cannot hit an unrelated depth lane");
                Finisher(p,false);p.Render(0);Check(p.ComboMove.kind==(p==g.Fernando?SFStrike.Uppercut:SFStrike.Roundhouse),p.identity+" punch finisher retains the personal sequence");
                Finisher(p,true);p.KnockDown();p.Render(0);Check(!Roundhouse(p)&&!p.ComboAttack,p.identity+" knockdown takes visual priority");
            }
            File.WriteAllLines(Path.Combine(qa,"roundhouse-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
