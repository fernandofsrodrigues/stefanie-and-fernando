#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed class SFHookCheck:MonoBehaviour
    {
        SFGame g;SFActor enemy;string qa;readonly List<string> checks=new List<string>();
        void Check(bool ok,string label){checks.Add((ok?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void Advance(SFActor p,float seconds){for(float t=0;t<seconds-.001f;t+=.01f)p.Tick(Mathf.Min(.01f,seconds-t),default);}
        void Ready(SFActor p)
        {
            p.ReleaseGrip();p.Teleport(new Vector2(7,0));p.lane=-.85f;p.Armed=false;p.Rifle=false;p.ResetCombo();p.action=SFAction.Idle;p.cooldown=0;p.invulnerable=0;p.health=p.maxHealth;
            p.Crouching=p.Aiming=false;p.facing=1;
            enemy.Teleport(new Vector2(8.2f,0));enemy.lane=p.lane;enemy.action=SFAction.Idle;enemy.health=enemy.maxHealth=1000;enemy.invulnerable=0;enemy.cooldown=999;
            p.Tick(.01f,default);
        }
        void Strike(SFActor p)
        {
            enemy.invulnerable=0;enemy.action=SFAction.Idle;enemy.Teleport(new Vector2(8.2f,0));enemy.lane=p.lane;
            p.Tick(.01f,new SFCommand{punch=true});Advance(p,p.attackDuration+.08f);
        }
        void Hook(SFActor p){Ready(p);Strike(p);Strike(p);enemy.invulnerable=0;p.Tick(.01f,new SFCommand{punch=true});}
        void Capture(SFActor p,string name)
        {
            var camera=new GameObject("Hook QA camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.85f;
            camera.transform.position=new Vector3(p.X,p.Height-3+p.lane+1.45f,-10);camera.clearFlags=CameraClearFlags.SolidColor;
            var texture=new RenderTexture(960,720,24);camera.targetTexture=texture;camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;
            var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(qa,name+".png"),pixels.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Destroy(texture);Destroy(pixels);Destroy(camera.gameObject);
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Hook-v0846"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.4f);g.StartGame();
            foreach(var a in g.Actors){a.Teleport(new Vector2(a.hero?7:125,0));a.cooldown=999;a.lane=-.85f;}
            yield return new WaitForSeconds(.3f);g.SetState(SFState.Paused);g.Selected=1;g.Fernando.Teleport(new Vector2(110,0));
            enemy=g.Actors.First(a=>!a.Friendly&&!a.boss);var p=g.Stefanie;
            var move=SFCombo.Get("stefanie",2);
            Check(move.kind==SFStrike.Hook&&move.contact==.17f&&move.damage==24&&move.reach==1.65f,"hook retains the authored third-strike combat balance");
            Check(SFStrikeAnimation.EightPoseFrame(move.contact-.001f,move.contact,move.duration)==2&&SFStrikeAnimation.EightPoseFrame(move.contact,move.contact,move.duration)==3,"hook contact art aligns with the damage event");
            foreach(int outfit in new[]{0,1,2})
            {
                p.FieldUniform=outfit!=0;p.Helmet=outfit!=2;string sheet=outfit==0?"hook":outfit==1?"field_hook_helmet":"field_hook_bare";
                var art=Resources.Load<SFArt>("SF/stefanie_"+sheet);
                Check(art!=null&&art.frames.Length==8&&art.frames.All(f=>f!=null),sheet+" imports eight complete sprites");
                Hook(p);Check(p.ComboAttack&&p.ComboMove.kind==SFStrike.Hook&&p.ActiveComboStep==2,sheet+" reached by two connected strikes");
                float hp=enemy.health;Advance(p,.15f);p.Render(0);
                Check(enemy.health==hp&&p.CurrentSheet==sheet&&p.CurrentFrame==2,sheet+" preparation cannot damage early");
                Advance(p,.02f);p.Render(0);
                Check(enemy.health<hp&&p.NextComboStep==3&&p.CurrentSheet==sheet&&p.CurrentFrame==3,sheet+" contact deals damage and queues the roundhouse");
                float after=enemy.health;enemy.invulnerable=0;Advance(p,.1f);Check(enemy.health==after,sheet+" recovery does not duplicate hook damage");
                foreach(int face in new[]{1,-1})
                {
                    p.facing=face;
                    for(int i=0;i<8;i++)
                    {
                        p.actionClock=i<3?(i+.2f)*p.attackAt/3:p.attackAt+(i-3+.2f)*(p.attackDuration-p.attackAt)/5;
                        p.Render(0);
                        Check(p.CurrentSheet==sheet&&p.CurrentFrame==i&&p.visual.flipX==(face<0),sheet+" phase "+i+" facing "+face);
                        if(face>0)Capture(p,sheet+"-"+i);
                    }
                }
                Hook(p);Advance(p,.48f);p.Tick(.01f,new SFCommand{punch=true});p.Render(0);
                Check(p.ComboMove.kind==SFStrike.Roundhouse&&!p.CurrentSheet.Contains("hook"),sheet+" releases into the finisher");
                Hook(p);p.invulnerable=0;p.Damage(1,enemy,true);p.Render(0);
                Check(!p.CurrentSheet.Contains("hook")&&!p.ComboAttack,sheet+" damage takes priority and clears the combo");
                Hook(p);p.Tick(.01f,new SFCommand{weapon=2});p.Render(0);Check(!p.Armed&&p.CurrentSheet==sheet,sheet+" active strike stays committed before weapon switch");
                Advance(p,.48f);p.Tick(.01f,new SFCommand{weapon=2});p.Render(0);Check(!p.CurrentSheet.Contains("hook")&&p.Armed&&p.Rifle,sheet+" recovered weapon change removes hook override");
                p.action=SFAction.Idle;p.cooldown=0;p.RifleAmmo.Fire();p.TryReload();p.Render(0);Check(p.Reloading&&!p.CurrentSheet.Contains("hook"),sheet+" reload keeps its weapon pose");
            }
            p.FieldUniform=false;Ready(p);p.Tick(.01f,new SFCommand{punch=true});p.Render(0);Check(p.CurrentSheet=="strike","first jab retains its straight-strike sheet");
            Advance(p,.5f);p.Tick(.01f,new SFCommand{punch=true});p.Render(0);Check(p.CurrentSheet=="strike","second straight retains its own sheet");
            Hook(p);p.FieldUniform=true;p.Helmet=false;p.Render(0);Check(p.CurrentSheet=="field_hook_bare","outfit change chooses matching bare-head field hook");
            p.Helmet=true;p.Render(0);Check(p.CurrentSheet=="field_hook_helmet","helmet change chooses matching helmet hook");
            p.FieldUniform=false;p.Render(0);Check(p.CurrentSheet=="hook","civilian clothes ignore the military helmet preference");
            Ready(g.Fernando);g.Fernando.FieldUniform=false;Strike(g.Fernando);Strike(g.Fernando);g.Fernando.Tick(.01f,new SFCommand{punch=true});g.Fernando.Render(0);
            Check(g.Fernando.ComboMove.kind==SFStrike.Jab&&!g.Fernando.CurrentSheet.Contains("hook"),"Fernando third strike remains his jab");
            File.WriteAllLines(Path.Combine(qa,"hook-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
