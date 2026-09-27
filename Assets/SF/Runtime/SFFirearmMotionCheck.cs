#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed class SFFirearmMotionCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();SFGame g;string qa;
        void Check(bool ok,string label){checks.Add((ok?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/FirearmMotion-v0846"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.5f);g.StartGame();yield return new WaitForSeconds(.4f);
            foreach(var a in g.Actors.Where(a=>!a.hero)){a.Teleport(new Vector2(130,0));a.cooldown=999;}
            foreach(var a in new[]{g.Fernando,g.Stefanie})a.invulnerable=999;
            foreach(bool run in new[]{false,true})
            {
                var a=SFLocomotionRig.Solve(.14f,run);var b=SFLocomotionRig.Solve(.04f,run);
                Check(a.planted&&b.planted&&Mathf.Abs(b.contact.x-a.contact.x-SFLocomotionRig.CycleLength(run)*.1f)<.001f,(run?"run":"walk")+" reversed heel contact cancels backward body travel");
            }
            foreach(int hero in new[]{0,1})
            {
                g.Selected=hero;var p=g.Player;p.lane=-.9f;g.Partner.Teleport(new Vector2(110,0));
                var pistol=Resources.Load<SFArt>("SF/"+p.identity+"_sidearm")??Resources.Load<SFArt>("SF/"+p.identity+"_pistol");
                foreach(bool field in new[]{false,true})foreach(bool rifle in new[]{false,true})foreach(bool helmet in new[]{true,false})
                {
                    if(!field&&!helmet)continue;
                    p.Teleport(new Vector2(7,.03f));p.FieldUniform=field;p.Rifle=rifle;p.Armed=true;p.Helmet=helmet;p.action=SFAction.Idle;
                    g.SmokeCommand=new SFCommand{move=Vector2.right,walk=true,aim=true,aimFacing=1};yield return new WaitForSeconds(.5f);
                    var rig=field?p.LocomotionRig:p.CivilianLocomotionRig;
                    string key=(field?"field-":"civil-")+(rifle?"rifle":"pistol")+(field?(helmet?"-helmet":"-bare"):"");
                    Sprite expected=field?Resources.Load<SFArt>("SF/"+p.identity+(rifle?"_field_rifle":"_field_actions")).Frame((helmet?0:4)+(rifle?1:3)):rifle?Resources.Load<SFArt>("SF/"+p.identity+"_civil_rifle").Frame(1):pistol.Frame(2);
                    Check(p.Aiming&&rig!=null&&rig.gameObject.activeSelf&&!p.visual.enabled&&rig.Aimed,p.identity+" "+key+" moving aim uses articulated legs");
                    Check(rig.UpperSprite==expected,p.identity+" "+key+" keeps original aimed face and weapon sprite");
                    g.SetState(SFState.Paused);foreach(var item in g.Pickups)item.visual.enabled=false;
                    for(int frame=0;frame<8;frame++)
                    {rig.Show(frame/8f*SFLocomotionRig.CycleLength(false),false,helmet,1,p.visual.color,p.visual.sortingOrder,rifle?0:2,true);SFGaitCheck.CaptureActor(p,qa,p.identity+"-"+key+"-aim-"+frame.ToString("00"),896);}
                    g.SetState(SFState.Playing);g.SmokeCommand=new SFCommand{move=Vector2.left,walk=true,aim=true,aimFacing=1};yield return new WaitForSeconds(.35f);
                    float x=p.X,phase=rig.Phase;yield return new WaitForSeconds(.12f);
                    Check(p.X<x-.05f&&p.facing==1&&rig.Backpedaling&&Mathf.DeltaAngle(phase*360,rig.Phase*360)<-1,p.identity+" "+key+" backpedals with reversed gait while aim stays right");
                    g.SmokeCommand=new SFCommand{move=Vector2.right,walk=true,aim=true,aimFacing=-1};yield return new WaitForSeconds(.35f);
                    Check(p.facing==-1&&rig.transform.localScale.x==-1&&rig.Backpedaling,p.identity+" "+key+" left-facing aim mirrors whole backpedal rig");
                    g.SmokeCommand=new SFCommand{aim=true,aimFacing=1};yield return new WaitForSeconds(.3f);
                    Check(p.visual.enabled&&!rig.gameObject.activeSelf,p.identity+" "+key+" stopped aim returns to original stance");
                }
                p.Teleport(new Vector2(7,.03f));p.FieldUniform=false;p.Armed=true;p.Rifle=false;p.action=SFAction.Idle;
                foreach(bool run in new[]{false,true})
                {
                    // Keep each speed fixture in the clear section of the lane.
                    p.Teleport(new Vector2(7,.03f));
                    g.SmokeCommand=new SFCommand{move=Vector2.right,walk=!run,run=run};
                    // Game.Update must consume the command before counting physics steps;
                    // catch-up steps immediately after a capture can still have zero input.
                    yield return null;
                    // Wait for actual simulation steps after synchronous gallery captures.
                    for(int step=0;step<25;step++)yield return new WaitForFixedUpdate();
                    yield return null;
                    var rig=p.CivilianLocomotionRig;
                    Debug.Log(p.identity+" ready locomotion: run="+run+" sheet="+p.CurrentSheet+" speed="+p.moveAmount+" grounded="+p.motor.IsGrounded+" active="+rig.gameObject.activeSelf+" rigRun="+rig.Running+" aimed="+rig.Aimed+" torso="+rig.UpperSprite.name+" expected="+pistol.Frame(1).name);
                    Check(p.CurrentSheet=="civil_pistol_rig"&&rig.UpperSprite==pistol.Frame(1)&&rig.Running==run&&!rig.Aimed,p.identity+" civilian pistol "+(run?"run":"walk")+" retains ready torso by default");
                    g.SetState(SFState.Paused);
                    for(int frame=0;frame<8;frame++)
                    {rig.Show(frame/8f*SFLocomotionRig.CycleLength(run),run,true,1,p.visual.color,p.visual.sortingOrder,2);SFGaitCheck.CaptureActor(p,qa,p.identity+"-civil-pistol-"+(run?"run":"walk")+"-"+frame.ToString("00"),896);}
                    g.SetState(SFState.Playing);
                }
                g.SmokeCommand=new SFCommand{crouch=true,aim=true};yield return new WaitForSeconds(.2f);
                Check(p.visual.enabled&&!p.CivilianLocomotionRig.gameObject.activeSelf&&p.CurrentSheet=="cover",p.identity+" pistol kneel restores full cover pose");
                g.SmokeCommand=new SFCommand{shoot=true};yield return null;g.SmokeCommand=default;yield return new WaitForSeconds(.05f);
                Check(p.action==SFAction.Shoot&&p.visual.enabled&&!p.CivilianLocomotionRig.gameObject.activeSelf,p.identity+" pistol fire interrupts gait");
                yield return new WaitForSeconds(.8f);g.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.3f);
                g.MotionStudyPreview=true;yield return null;
                Check(p.CurrentSheet=="pistol_walk"&&p.visual.enabled&&!p.CivilianLocomotionRig.gameObject.activeSelf,p.identity+" F6 keeps rejected study explicitly separate");
                g.MotionStudyPreview=false;yield return null;
                Check(p.CurrentSheet=="civil_pistol_rig"&&!p.visual.enabled,p.identity+" leaving F6 restores accepted default rig");
                g.SmokeCommand=default;
            }
            File.WriteAllLines(Path.Combine(qa,"firearm-motion-checks.txt"),checks);Application.Quit(checks.Any(c=>c.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
