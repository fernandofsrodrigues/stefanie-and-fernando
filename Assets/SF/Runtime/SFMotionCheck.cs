#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // Executable integration checks of actual commands, physics, rendering and companion navigation.
    // Passing these checks validates the integration, not the anatomy of generated gait studies.
    public sealed class SFMotionCheck:MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        SFGame g;
        void Check(bool pass,string name){results.Add((pass?"PASS: ":"FAIL: ")+name);Debug.Log(results.Last());}
        void Isolate()
        {
            foreach(var a in g.Actors.Where(a=>!a.hero)){a.Teleport(new Vector2(120,0));a.cooldown=100;}
            g.Player.invulnerable=g.Partner.invulnerable=100;
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return null;
            Check(!g.MotionStudyPreview,"unaccepted movement studies are off by default");
            foreach(var name in new[]{"props_urban","props_coastal","props_desert"})
            {var art=Resources.Load<SFArt>("SF/"+name);Check(art!=null&&art.frames.Length==6&&art.frames.All(f=>f!=null),name+" has six masked prop groups");}
            g.Selected=1;g.StartGame();yield return new WaitForSeconds(.4f);Isolate();
            Check(g.Player.identity=="stefanie"&&g.Player.X>g.Partner.X,"selected Stefanie starts in the lead");
            foreach(float x in new[]{.7f,3f,129f,132f})foreach(int direction in new[]{-1,1})
            {
                g.Player.Teleport(new Vector2(x,0));g.Player.facing=direction;
                var f=g.FormationGoal(4.5f,.85f);
                Check(f.x>=1&&f.x<=131.5f&&Mathf.Abs(f.x-x)>4,"formation keeps room at x="+x+" facing "+direction);
            }
            g.Player.Teleport(new Vector2(1,0));g.Player.facing=1;g.Player.lane=0;g.Partner.Teleport(new Vector2(4,0));
            g.CallBackup();yield return new WaitForSeconds(7);
            var allies=g.Actors.Where(a=>a.ally).ToArray();
            Check(allies.Length==2&&allies.All(a=>a.X>2&&Mathf.Abs(a.motor.Body.linearVelocity.x)<.1f),"backup settles at left boundary without walking against clamp");
            g.BackupRemaining=.01f;yield return new WaitForSeconds(.1f);
            for(int hero=0;hero<2;hero++)
            {
                g.Selected=hero;var p=g.Player;g.Partner.Teleport(new Vector2(110,0));Isolate();
                p.Teleport(new Vector2(3,0));p.lane=-.85f;p.Armed=true;p.Rifle=false;
                g.MotionStudyPreview=false;g.SmokeCommand=new SFCommand{move=Vector2.right,walk=true};yield return new WaitForSeconds(.4f);
                Check(p.CurrentSheet=="civil_pistol_rig"&&p.CivilianLocomotionRig.BodyMode==2,p.identity+" default sidearm movement uses matching articulated rig");
                g.MotionStudyPreview=true;var frames=new HashSet<int>();
                for(int i=0;i<16;i++)
                {yield return new WaitForSeconds(.08f);frames.Add(p.CurrentFrame);if(i%2==0)g.Capture("v04-"+p.identity+"-walk-"+(i/2).ToString("00"));}
                Check(p.CurrentSheet=="pistol_walk"&&frames.Count>=6,p.identity+" walk advances with travelled distance");
                p.Teleport(new Vector2(3,0));g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};yield return new WaitForSeconds(.3f);
                float runDeadline=Time.realtimeSinceStartup+1;while(p.CurrentSheet!="pistol_run"&&Time.realtimeSinceStartup<runDeadline)yield return null;
                Check(p.CurrentSheet=="pistol_run",p.identity+" full speed selects sidearm run study");
                g.Capture("v04-"+p.identity+"-run");
                g.SmokeCommand=new SFCommand{move=Vector2.left,aim=true,aimFacing=1};
                // Reversing a full run must decelerate through zero before backpedaling.
                // Let Update consume the command, then advance a bounded physical interval.
                yield return null;for(int step=0;step<25;step++)yield return new WaitForFixedUpdate();yield return null;
                Debug.Log("MOTION AIM "+p.identity+" sheet="+p.CurrentSheet+" action="+p.action+" aiming="+p.Aiming+" facing="+p.facing+" grounded="+p.motor.IsGrounded+" speed="+p.moveAmount+" vx="+p.motor.Body.linearVelocity.x+" rigAim="+p.CivilianLocomotionRig.Aimed+" frameDt="+Time.deltaTime);
                Check(p.CurrentSheet=="civil_pistol_rig"&&p.CivilianLocomotionRig.Aimed&&p.facing==1&&p.Aiming,p.identity+" moving aim retains matching aimed torso and independent facing");
                g.SmokeCommand=new SFCommand{crouch=true};yield return new WaitForSeconds(.2f);
                Check(p.CurrentSheet=="cover"&&p.CurrentFrame==4,p.identity+" crouch keeps armed kneeling pose");
                p.Rifle=true;g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};yield return new WaitForSeconds(.25f);
                Check(p.CurrentSheet=="civil_rifle_rig",p.identity+" rifle uses civilian rig instead of pistol study");
                p.Rifle=false;g.SmokeCommand=new SFCommand{shoot=true};yield return null;g.SmokeCommand=default;yield return new WaitForSeconds(.13f);
                Check(p.CurrentSheet=="recoil",p.identity+" firing takes priority over locomotion");
                yield return new WaitForSeconds(.6f);p.Armed=false;p.Teleport(new Vector2(3,0));
                g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};yield return new WaitForSeconds(.4f);
                g.SmokeCommand=default;yield return new WaitForSeconds(.05f);
                Check(p.CurrentSheet=="stop",p.identity+" release enters one-shot stop study");
                yield return new WaitForSeconds(.5f);Check(p.CurrentSheet=="idle",p.identity+" stop returns to existing idle");
                p.Teleport(new Vector2(4,0));p.lane=-1;g.SmokeCommand=new SFCommand{move=Vector2.up};frames.Clear();
                // Sample a travelled interval, not an old fixed run-speed window: default is now jogging.
                float laneStart=p.lane,deadline=g.Elapsed+2,watchdog=Time.realtimeSinceStartup+15;
                while(p.lane-laneStart<1.25f&&g.Elapsed<deadline&&Time.realtimeSinceStartup<watchdog)
                {yield return new WaitForSeconds(.08f);frames.Add(p.CurrentFrame);}
                Debug.Log("MOTION DEPTH "+p.identity+" sheet="+p.CurrentSheet+" action="+p.action+" laneTravel="+(p.lane-laneStart)+" grounded="+p.motor.IsGrounded+" speed="+p.moveAmount+" frames="+string.Join(",",frames)+" frameDt="+Time.deltaTime);
                Check(p.lane-laneStart>=1.25f&&p.CurrentSheet=="civil_north"&&frames.Count>=3,p.identity+" depth-only travel animates footsteps over 1.25 units");
                g.SmokeCommand=default;
                g.SetState(SFState.Paused);p.lane=-1;
                bool stable=true;float expectedSpeed=1.65f*.72f/5.2f;
                foreach(float dt in new[]{.001f,.05f,.002f,.04f,.016f,.001f,.05f})
                {
                    p.Tick(dt,new SFCommand{move=Vector2.up});
                    stable&=p.CurrentSheet=="civil_north"&&Mathf.Abs(p.moveAmount-expectedSpeed)<.001f;
                }
                Check(stable,p.identity+" depth locomotion stays stable across unequal frame intervals");
                g.SetState(SFState.Playing);
            }
            g.MotionStudyPreview=false;g.SetState(SFState.Menu);
            foreach(int route in new[]{0,1,5})
            {
                g.SelectRoute(route);yield return null;g.SetState(SFState.Playing);g.Player.Teleport(new Vector2(12,0));g.Player.lane=-.85f;
                yield return new WaitForSeconds(.2f);g.Capture("v04-cover-route-"+route);g.SetState(SFState.Menu);
            }
            string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));File.WriteAllLines(Path.Combine(qa,"motion-checks.txt"),results);
            Application.Quit(results.Any(x=>x.StartsWith("FAIL"))?2:0);
        }
    }
}
#endif
