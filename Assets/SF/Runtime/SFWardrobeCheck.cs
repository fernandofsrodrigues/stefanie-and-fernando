#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // Exercise movement through real input commands, then capture the same runtime geometry.
    public sealed class SFWardrobeCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>(); SFGame g;string qa;
        void Check(bool ok,string name){checks.Add((ok?"PASS: ":"FAIL: ")+name);Debug.Log(checks.Last());}
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Wardrobe-v0846"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.5f);g.StartGame();yield return new WaitForSeconds(.4f);
            foreach(var a in g.Actors.Where(a=>!a.hero)){a.Teleport(new Vector2(130,0));a.cooldown=999;}
            foreach(var a in new[]{g.Fernando,g.Stefanie})a.invulnerable=999;
            foreach(int hero in new[]{0,1})
            {
                g.Selected=hero;var p=g.Player;g.Partner.Teleport(new Vector2(110,0));
                foreach(int mode in new[]{1,2,3})foreach(bool helmet in new[]{true,false})
                {
                    if(mode==3&&!helmet)continue;
                    p.Teleport(new Vector2(7,-.9f));p.action=SFAction.Idle;p.Armed=mode!=1;p.Rifle=mode==3;p.FieldUniform=mode!=3;p.Helmet=helmet;
                    string key=(mode==3?"civil-rifle":mode==1?"field-unarmed":"field-pistol")+(mode==3?"":helmet?"-helmet":"-bare");
                    string sheet=mode==3?"civil_rifle_rig":mode==1?"field_unarmed_rig":"field_pistol_rig";
                    g.SmokeCommand=new SFCommand{move=Vector2.right,walk=true};yield return new WaitForSeconds(.5f);
                    var rig=mode==3?p.CivilianLocomotionRig:p.LocomotionRig;
                    Check(rig!=null&&rig.gameObject.activeSelf&&!p.visual.enabled&&p.CurrentSheet==sheet,p.identity+" "+key+" enters matching rig via movement");
                    float before=rig.Phase;yield return new WaitForSeconds(.15f);
                    Check(Mathf.Abs(before-rig.Phase)>.02f&&!rig.Running,p.identity+" "+key+" walk advances with travel");
                    g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};yield return new WaitForSeconds(.35f);
                    Check(rig.Running&&rig.BodyMode==(mode==3?0:mode),p.identity+" "+key+" runs with matching torso");
                    g.SetState(SFState.Paused);foreach(var item in g.Pickups)item.visual.enabled=false;
                    foreach(bool run in new[]{false,true})for(int frame=0;frame<8;frame++)
                    {
                        rig.Show(frame/8f*SFLocomotionRig.CycleLength(run),run,helmet,1,p.visual.color,p.visual.sortingOrder,mode==3?0:mode);
                        SFGaitCheck.CaptureActor(p,qa,p.identity+"-"+key+"-"+(run?"run":"walk")+"-"+frame.ToString("00"));
                    }
                    g.SmokeCommand=default;g.SetState(SFState.Playing);yield return new WaitForSeconds(.25f);
                    Check(p.visual.enabled&&!rig.gameObject.activeSelf,p.identity+" "+key+" stops without leftover rig parts");
                }
                p.Teleport(new Vector2(7,-.9f));p.FieldUniform=true;p.Armed=false;p.action=SFAction.Idle;
                g.SmokeCommand=new SFCommand{move=Vector2.right,walk=true};yield return new WaitForSeconds(.3f);
                g.SmokeCommand=new SFCommand{move=Vector2.right,weapon=1};yield return null;
                g.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.12f);
                Check(p.Armed&&!p.Rifle&&p.CurrentSheet=="field_pistol_rig",p.identity+" weapon-1 switches moving unarmed to pistol");
                g.SmokeCommand=new SFCommand{move=Vector2.right,weapon=2};yield return null;
                g.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.12f);
                Check(p.Armed&&p.Rifle&&p.CurrentSheet=="field_rifle_rig",p.identity+" weapon-2 switches moving pistol to signature rifle");
                g.SmokeCommand=new SFCommand{move=Vector2.right,toggleArmed=true};yield return null;
                g.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.12f);
                Check(!p.Armed&&p.CurrentSheet=="field_unarmed_rig",p.identity+" armed-toggle switches moving rifle to empty hands");
                g.SmokeCommand=new SFCommand{move=Vector2.right,punch=true};yield return null;
                g.SmokeCommand=default;yield return new WaitForSeconds(.05f);
                Check(p.action==SFAction.Punch&&p.visual.enabled&&!p.LocomotionRig.gameObject.activeSelf,p.identity+" punch owns pose after moving unarmed");
                yield return new WaitForSeconds(.8f);
                p.FieldUniform=false;p.Armed=p.Rifle=true;
                g.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.35f);
                g.SmokeCommand=new SFCommand{move=Vector2.right,aim=true};yield return new WaitForSeconds(.15f);
                Check(p.Aiming&&!p.visual.enabled&&p.CurrentSheet=="civil_rifle_rig"&&p.CivilianLocomotionRig.Aimed,p.identity+" civilian moving aim uses original rifle torso with active legs");
                g.SmokeCommand=new SFCommand{shoot=true};yield return null;g.SmokeCommand=default;yield return new WaitForSeconds(.05f);
                Check(p.action==SFAction.Shoot&&p.visual.enabled&&!p.CivilianLocomotionRig.gameObject.activeSelf,p.identity+" civilian rifle shot owns pose");
                yield return new WaitForSeconds(.8f);
                g.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.3f);
                g.SmokeCommand=new SFCommand{move=Vector2.right,jump=true};yield return null;yield return new WaitForFixedUpdate();
                g.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.18f);
                Check(!p.motor.IsGrounded&&p.visual.enabled&&!p.CivilianLocomotionRig.gameObject.activeSelf,p.identity+" civilian jump hides grounded legs");
                g.SmokeCommand=default;yield return new WaitForSeconds(1.3f);
                p.Armed=false;p.Render(0);Check(p.visual.enabled&&!p.LocomotionRig.gameObject.activeSelf&&!p.CivilianLocomotionRig.gameObject.activeSelf,p.identity+" civilian unarmed keeps its existing animation");
            }
            File.WriteAllLines(Path.Combine(qa,"wardrobe-checks.txt"),checks);Application.Quit(checks.Any(c=>c.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
