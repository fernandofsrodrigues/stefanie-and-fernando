#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed class SFGaitCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();SFGame g;string qa;
        void Check(bool pass,string label){checks.Add((pass?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void CheckBlendSolver()
        {
            var blend=new SFGaitBlend();
            Check(blend.Step(false,0)==0,"gait blend starts at the current walking pose");
            Check(blend.Step(true,0)==0,"zero-time render cannot advance gait blending");
            Check(Mathf.Abs(blend.Step(true,.11f)-.5f)<.0001f,"walk-to-run uses a gradual intermediate pose");
            Check(blend.Step(true,.11f)==1,"walk-to-run reaches full running in 220 ms");
            Check(Mathf.Abs(blend.Step(false,.13f)-.5f)<.0001f,"run-to-walk uses a gradual intermediate pose");
            Check(blend.Step(false,.13f)==0,"run-to-walk reaches full walking in 260 ms");
            blend.Step(true,.08f);float before=blend.Weight;
            Check(blend.Step(false,.01f)<before&&blend.Weight>0,"rapid speed reversal continues smoothly from the current blend");
            var split=new SFGaitBlend();split.Step(false,0);blend.Reset();blend.Step(false,0);
            for(int i=0;i<7;i++)split.Step(true,.017f);
            Check(Mathf.Abs(split.Weight-blend.Step(true,7*.017f))<.00001f,"gait blend depends on elapsed time rather than frame count");
            blend.Reset();Check(blend.Step(true,0)==1,"re-entry after a suspended action selects its current speed without stale blend");
            bool bones=true,soles=true,closing=true,aim=true;
            for(int w=0;w<=20;w++)for(int i=0;i<200;i++)
            {
                float weight=w/20f,phase=i/200f;var pose=SFLocomotionRig.Solve(phase,weight);
                bones&=Mathf.Abs(Vector2.Distance(pose.hip,pose.knee)-.60f)<.002f&&Mathf.Abs(Vector2.Distance(pose.knee,pose.ankle)-.64f)<.002f;
                float heel=SFLocomotionRig.FootPoint(pose,SFLocomotionRig.Heel).y,toe=SFLocomotionRig.FootPoint(pose,SFLocomotionRig.Toe).y;
                soles&=heel>=-.0001f&&toe>=-.0001f&&(!pose.planted||Mathf.Min(heel,toe)<.0001f);
                closing&=Vector2.Distance(SFLocomotionRig.Solve(.99999f,weight).ankle,SFLocomotionRig.Solve(0,weight).ankle)<.001f;
                aim&=Mathf.Abs(SFLocomotionRig.BodyLean(phase,weight,true,false))<=.281f;
            }
            Check(bones,"4200 blended gait samples retain both bone lengths");
            Check(soles,"4200 blended gait samples keep soles above ground and support contacts planted");
            Check(closing,"all 21 intermediate gaits close without a foot jump");
            Check(aim,"precision aiming suppresses torso sway at all intermediate speeds");
        }
        void CheckRigTransition(SFLocomotionRig rig,SFActor actor,bool civil)
        {
            string label=actor.identity+(civil?" civilian":" field");float distance=0;
            rig.Suspend(distance);rig.Advance(distance,false,actor.Helmet,1,Color.white,actor.visual.sortingOrder);
            float phase=rig.Phase;rig.Advance(distance,true,actor.Helmet,1,Color.white,actor.visual.sortingOrder,0,false,false,.055f);
            Check(rig.RunWeight>0&&rig.RunWeight<1&&Mathf.Abs(rig.Phase-phase)<.0001f,label+" blends speed without cycling stationary feet");
            rig.Suspend(distance);rig.Advance(distance,false,actor.Helmet,1,Color.white,actor.visual.sortingOrder);
            bool gradual=true;float previous=0;
            for(int stage=0;stage<2;stage++)for(int frame=0;frame<9;frame++)
            {
                bool run=stage==0;float dt=frame==0?0:.04f;distance+=dt*(run?4.8f:2.4f);
                rig.Advance(distance,run,actor.Helmet,1,Color.white,actor.visual.sortingOrder,0,false,false,dt);
                if(frame>0)gradual&=Mathf.Abs(rig.RunWeight-previous)<.29f;
                previous=rig.RunWeight;
                CaptureActor(actor,qa,"transition-"+(civil?"civil-":"field-")+actor.identity+"-"+(run?"accelerate-":"decelerate-")+frame.ToString("00"));
            }
            Check(gradual&&rig.RunWeight==0,label+" rendered acceleration and deceleration use intermediate poses");
            rig.Advance(distance+.1f,true,actor.Helmet,-1,Color.white,actor.visual.sortingOrder,0,true,true,.11f);
            Check(rig.RunWeight>0&&rig.RunWeight<1&&Mathf.Abs(rig.BodyLeanDegrees)<.3f&&rig.Backpedaling,label+" aimed mirrored backpedaling keeps restrained sway while blending");
            phase=rig.Phase;rig.Suspend(distance+2);rig.Advance(distance+2,false,actor.Helmet,1,Color.white,actor.visual.sortingOrder,0,false,false,.016f);
            Check(rig.RunWeight==0&&Mathf.Abs(rig.Phase-phase)<.0001f,label+" action re-entry absorbs hidden travel and resets stale running weight");
        }
        void CheckLimbArt(SFLocomotionRig rig,SFActor actor,bool civil)
        {
            string label=actor.identity+(civil?" civilian":" field");int frame=actor.identity=="stefanie"?1:0;
            var inside=Resources.Load<SFArt>("SF/"+(civil?"civil":"field")+"_inner_leg_parts");
            Check(rig.NearLegSprite!=rig.FarLegSprite&&rig.NearLegSprite.texture!=rig.FarLegSprite.texture&&rig.FarLegSprite==inside.Frame(frame),label+" draws independent inner and outer artwork");
            var saved=new Dictionary<string,Vector3>();
            bool attached=true,separate=false;
            for(int i=0;i<16;i++)
            {
                rig.Show(i/16f*SFLocomotionRig.CycleLength(true),true,true,1,Color.white,actor.visual.sortingOrder,0);
                foreach(string name in new[]{"Near leg","Far leg"})
                {
                    var mesh=rig.transform.Find(name).GetComponent<MeshFilter>().sharedMesh;
                    var vertices=mesh.vertices;var hip=name=="Near leg"?rig.Near.hip:rig.Far.hip;
                    // Top-row vertices must follow one pelvis frame over the whole stride,
                    // despite each thigh having a different phase and flexion.
                    var top=Quaternion.Euler(0,0,-rig.BodyLeanDegrees)*(vertices[vertices.Length-7]-(Vector3)hip);
                    if(i==0)saved[name]=top;else attached&=Vector3.Distance(top,saved[name])<.0001f;
                }
                separate|=Vector2.Distance(rig.Near.knee,rig.Far.knee)>.1f;
            }
            Check(attached&&separate,label+" both waist rims stay attached while thighs swing independently");
            CheckRigTransition(rig,actor,civil);
        }
        internal static void CaptureActor(SFActor actor,string directory,string name,int width=640)
        {
            var camera=new GameObject("Gait QA camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.58f;
            camera.transform.position=new Vector3(actor.X,actor.Height-3+actor.lane+1.38f,-10);camera.backgroundColor=new Color(.10f,.14f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            var texture=new RenderTexture(width,720,24);camera.targetTexture=texture;camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;
            var pixels=new Texture2D(width,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Destroy(texture);Destroy(pixels);Destroy(camera.gameObject);
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Gait-v082"));Directory.CreateDirectory(qa);yield return new WaitForSeconds(.5f);
            CheckBlendSolver();
            foreach(bool run in new[]{false,true})
            {
                var contact=SFLocomotionRig.Solve(0,run);var opposite=SFLocomotionRig.Solve(.5f,run);
                Check(contact.ankle.x>opposite.ankle.x&&SFLocomotionRig.Solve(.5f,run).ankle.x<SFLocomotionRig.Solve(1,run).ankle.x,(run?"run":"walk")+" reverses near/far foot order after half a cycle");
                var a=SFLocomotionRig.Solve(.04f,run);var b=SFLocomotionRig.Solve(.14f,run);
                Check(a.planted&&b.planted&&Mathf.Abs(b.contact.x-a.contact.x+SFLocomotionRig.CycleLength(run)*.1f)<.001f,(run?"run":"walk")+" heel contact cancels forward body travel while ankle rolls");
                bool sound=true,flight=false,solesClear=true,aimSteady=true;float maxLift=0;
                for(int i=0;i<200;i++)
                {
                    var p=SFLocomotionRig.Solve(i/200f,run);var other=SFLocomotionRig.Solve(i/200f+.5f,run);
                    sound&=Mathf.Abs(Vector2.Distance(p.hip,p.knee)-.60f)<.002f&&Mathf.Abs(Vector2.Distance(p.knee,p.ankle)-.64f)<.002f&&p.ankle.y>=.214f;
                    var heel=SFLocomotionRig.FootPoint(p,SFLocomotionRig.Heel);var toe=SFLocomotionRig.FootPoint(p,SFLocomotionRig.Toe);
                    solesClear&=heel.y>=-.0001f&&toe.y>=-.0001f&&(!p.planted||Mathf.Min(heel.y,toe.y)<.0001f);
                    aimSteady&=Mathf.Abs(SFLocomotionRig.BodyLean(i/200f,run,true,false))<=.281f;
                    flight|=!p.planted&&!other.planted;maxLift=Mathf.Max(maxLift,p.clearance);
                }
                Check(sound,(run?"run":"walk")+" keeps bone lengths and soles above ground over 200 phases");
                Check(flight==run,(run?"run":"walk")+" has appropriate support or flight intervals");
                Check(Vector2.Distance(SFLocomotionRig.Solve(.99999f,run).ankle,contact.ankle)<.001f,(run?"run":"walk")+" loop closes without a foot jump");
                Check(maxLift>(run?.40f:.17f)&&maxLift<(run?.43f:.19f),(run?"run":"walk")+" uses its authored swing clearance");
                float stance=run?.4f:.6f;
                Check(contact.footAngle>10&&Mathf.Abs(SFLocomotionRig.Solve(stance*.5f,run).footAngle)<.001f&&SFLocomotionRig.Solve(stance*.98f,run).footAngle<-18,(run?"run":"walk")+" progresses heel contact, flat support and toe departure");
                Check(solesClear,(run?"run":"walk")+" heel and toe anchors never pass below ground across 200 phases");
                a=SFLocomotionRig.Solve(stance*.85f,run);b=SFLocomotionRig.Solve(stance*.95f,run);
                Check(a.planted&&b.planted&&Mathf.Abs(b.contact.x-a.contact.x+SFLocomotionRig.CycleLength(run)*stance*.1f)<.001f,(run?"run":"walk")+" toe departure retains a stationary world contact");
                Check(aimSteady&&Mathf.Abs(SFLocomotionRig.BodyLean(.125f,run,false,false))>.5f,(run?"run":"walk")+" precision aim suppresses torso sway without changing gait");
            }
            foreach(var l in g.Loadouts){l.outfit=1;l.startRifle=true;}g.StartGame();yield return new WaitForSeconds(.4f);
            foreach(var a in g.Actors.Where(a=>!a.hero)){a.Teleport(new Vector2(130,0));a.cooldown=999;}
            foreach(var a in new[]{g.Fernando,g.Stefanie})a.invulnerable=100;
            for(int hero=0;hero<2;hero++)
            {
                g.Selected=hero;var p=g.Player;g.Partner.Teleport(new Vector2(100,0));p.Teleport(new Vector2(7,-.9f));p.Armed=p.Rifle=p.FieldUniform=true;p.action=SFAction.Idle;
                g.SmokeCommand=new SFCommand{move=Vector2.right,walk=true};yield return new WaitForSeconds(.55f);
                Check(p.LocomotionRig!=null&&p.LocomotionRig.gameObject.activeSelf&&!p.visual.enabled&&p.CurrentSheet=="field_rifle_rig",p.identity+" walk uses the rig in actual gameplay");
                float phase=p.LocomotionRig.Phase;yield return new WaitForSeconds(.12f);
                Check(Mathf.Abs(p.LocomotionRig.Phase-phase)>.02f&&!p.LocomotionRig.Running,p.identity+" walking phase advances with real movement");
                phase=p.LocomotionRig.Phase;float speed=p.moveAmount;p.moveAmount=.95f;p.Render(0);bool continuous=Mathf.Abs(p.LocomotionRig.Phase-phase)<.0001f;
                p.moveAmount=speed;p.Render(0);Check(continuous&&Mathf.Abs(p.LocomotionRig.Phase-phase)<.0001f,p.identity+" changing gait at fixed distance preserves phase");
                var group=p.LocomotionRig.GetComponent<UnityEngine.Rendering.SortingGroup>();
                Check(group!=null&&group.sortingOrder==p.visual.sortingOrder,p.identity+" body and both legs sort together against other actors");
                g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};yield return new WaitForSeconds(.3f);
                Check(p.LocomotionRig.Running,p.identity+" Shift-speed movement selects separate running motion");
                g.SetState(SFState.Paused);
                var rig=p.LocomotionRig;
                CheckLimbArt(rig,p,false);
                rig.Show(.125f*SFLocomotionRig.CycleLength(true),true,p.Helmet,1,p.visual.color,p.visual.sortingOrder);
                Vector3 expectedPivot=rig.transform.TransformPoint(new Vector3(rig.transform.Find("Near leg").localPosition.x,rig.Near.hip.y,0));
                Check(Vector3.Distance(rig.BodyPivotWorld,expectedPivot)<.0001f&&rig.BodyLeanDegrees<-2,p.identity+" running torso rotates around the attached pelvis without translation drift");
                rig.Show(.125f*SFLocomotionRig.CycleLength(false),false,p.Helmet,-1,p.visual.color,p.visual.sortingOrder,0,true,true);
                expectedPivot=rig.transform.TransformPoint(new Vector3(rig.transform.Find("Near leg").localPosition.x,rig.Near.hip.y,0));
                Check(Vector3.Distance(rig.BodyPivotWorld,expectedPivot)<.0001f&&Mathf.Abs(rig.BodyLeanDegrees)<.3f&&rig.Backpedaling,p.identity+" mirrored aimed backpedal keeps pelvis attached and muzzle sway restrained");
                foreach(var item in g.Pickups)item.visual.enabled=false;
                foreach(bool helmet in new[]{true,false})foreach(bool run in new[]{false,true})
                {
                    for(int frame=0;frame<16;frame++)
                    {
                        p.LocomotionRig.Show(frame/16f*SFLocomotionRig.CycleLength(run),run,helmet,1,p.visual.color,p.visual.sortingOrder);
                        CaptureActor(p,qa,p.identity+"-"+(helmet?"helmet":"bare")+"-"+(run?"run":"walk")+"-"+frame.ToString("00"));
                    }
                }
                p.facing=-1;p.Render(0);Check(p.LocomotionRig.transform.localScale.x==-1,p.identity+" left movement mirrors the entire rig together");
                p.Aiming=true;p.Render(0);Check(p.LocomotionRig.gameObject.activeSelf&&p.LocomotionRig.Aimed&&p.LocomotionRig.UpperSprite==Resources.Load<SFArt>("SF/"+p.identity+"_field_rifle").Frame(p.Helmet?1:5),p.identity+" moving aim retains original headgear and weapon torso");p.Aiming=false;
                p.Crouching=true;p.Render(0);Check(!p.LocomotionRig.gameObject.activeSelf&&p.visual.enabled,p.identity+" crouch hides all rig parts");p.Crouching=false;
                p.action=SFAction.Shoot;p.Render(0);Check(!p.LocomotionRig.gameObject.activeSelf&&p.visual.enabled,p.identity+" firing pose takes priority");p.action=SFAction.Idle;
                p.FieldUniform=false;p.Render(0);Check(!p.LocomotionRig.gameObject.activeSelf&&p.CivilianLocomotionRig.gameObject.activeSelf,p.identity+" civilian outfit uses its own legs");p.FieldUniform=true;
                p.FieldUniform=false;p.Render(0);CheckLimbArt(p.CivilianLocomotionRig,p,true);p.FieldUniform=true;p.Render(0);
                p.Armed=false;p.Render(0);Check(p.LocomotionRig.gameObject.activeSelf&&p.LocomotionRig.BodyMode==1&&!p.visual.enabled,p.identity+" unarmed retains matching upper body");p.Armed=p.Rifle=true;
                p.health=0;p.Render(0);Check(!p.LocomotionRig.gameObject.activeSelf&&p.visual.enabled,p.identity+" defeat never leaves detached animated legs");p.health=p.maxHealth;
                g.SmokeCommand=default;g.SetState(SFState.Playing);p.Teleport(new Vector2(7,-.9f));yield return new WaitForSeconds(.2f);
                g.SmokeCommand=new SFCommand{jump=true,move=Vector2.right};yield return null;yield return new WaitForFixedUpdate();
                g.SmokeCommand=new SFCommand{move=Vector2.right};yield return new WaitForSeconds(.18f);
                Debug.Log("Airborne diagnostics "+p.identity+" state="+g.State+" grounded="+p.motor.IsGrounded+" y="+p.Height+" action="+p.action+" rig="+p.LocomotionRig.gameObject.activeSelf+" sprite="+p.visual.enabled);
                Check(!p.motor.IsGrounded&&!p.LocomotionRig.gameObject.activeSelf&&p.visual.enabled,p.identity+" airborne state hides grounded gait");
                g.SmokeCommand=default;yield return new WaitForSeconds(1.5f);
                Check(p.visual.enabled&&!p.LocomotionRig.gameObject.activeSelf,p.identity+" stopping returns to stable original stance");
                phase=p.LocomotionRig.Phase;yield return new WaitForSeconds(.15f);Check(p.LocomotionRig.Phase==phase,p.identity+" stationary feet do not cycle");
            }
            File.WriteAllLines(Path.Combine(qa,"gait-checks.txt"),checks);Application.Quit(checks.Any(c=>c.StartsWith("FAIL"))?1:0);
        }
    }
}
#endif
