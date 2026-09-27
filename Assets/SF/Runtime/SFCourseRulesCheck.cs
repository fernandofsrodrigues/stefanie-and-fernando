using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StefanieAndFernando
{
    // Deterministic integration arrangements exercise public gameplay actions and real bodies.
    // Normal-command start-to-extraction coverage remains in SFRouteCheck.
    public sealed class SFCourseRulesCheck:MonoBehaviour
    {
        SFGame g;string qa;readonly List<string> checks=new List<string>();
        void Check(bool ok,string name){string line=(ok?"PASS: ":"FAIL: ")+name;checks.Add(line);Debug.Log(line);}
        void Ready(SFActor a,float x,float y=0)
        {a.ReleaseGrip();a.Revive();a.health=a.maxHealth;a.invulnerable=a.cooldown=0;a.lane=0;a.Teleport(new Vector2(x,y));a.action=SFAction.Idle;}
        void Down(SFActor a){a.invulnerable=0;a.Damage(1000,null,true);}
        IEnumerator Capture(string name)
        {
            foreach(var a in g.Actors)a.Render(0);
            yield return new WaitForEndOfFrame();
            if(Application.platform!=RuntimePlatform.WebGLPlayer)g.Capture("v0846-course-"+name);
            yield return null;
        }
        IEnumerator CaptureUI(string name)
        {
            if(Application.platform==RuntimePlatform.WebGLPlayer)yield break;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(qa,"v0846-course-"+name+"-ui.png"));
            yield return new WaitForSeconds(.12f);
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();
            if(Application.platform!=RuntimePlatform.WebGLPlayer){qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(qa);}
            yield return new WaitForSeconds(.2f);
            bool nativeQuit=!Application.isEditor&&Application.platform!=RuntimePlatform.WebGLPlayer;
            Check(g.CanQuitCourse==nativeQuit,"course menu exposes Quit only in standalone player");
            yield return CaptureUI("menu");g.StartGame();g.enabled=false;
            Check(!g.CanQuitCourse,"Quit is not offered during live course play");
            foreach(var a in g.Actors.Where(a=>!a.hero)){a.health=0;a.motor.Freeze(true);}
            var first=g.Player;var second=g.Partner;
            bool originalShake=g.ShakeEnabled;g.ShakeEnabled=false;
            foreach(bool course in new[]{true,false})
            {
                g.CourseMode=course;Ready(first,60);Ready(second,58);g.SmokeCommand=default;g.enabled=true;
                yield return new WaitForSeconds(1.5f);g.enabled=false;
                Check(Mathf.Abs(g.Camera.transform.position.x-first.X)<5,(course?"course":"campaign")+" camera follows a living lead during actual simulation");
            }
            g.CourseMode=true;
            foreach(float edge in new[]{132f,.7f})
            {
                Ready(first,edge==132?131.9f:.8f);var direction=edge==132?Vector2.right:Vector2.left;
                for(int n=0;n<25;n++){first.Tick(Time.fixedDeltaTime,new SFCommand{move=direction});yield return new WaitForFixedUpdate();}
                Check(Mathf.Abs(first.X-edge)<.02f&&Mathf.Abs(first.motor.Body.linearVelocity.x)<.02f,"course boundary stops body velocity at x"+edge);
            }
            Ready(first,131.8f);Ready(second,130);g.enabled=true;yield return new WaitForSeconds(1.5f);g.enabled=false;
            float viewRight=g.Camera.transform.position.x+g.Camera.orthographicSize*g.Camera.aspect;
            Check(viewRight>=130&&viewRight<133.7f,"course camera keeps extraction visible without showing a long street beyond the bound");
            g.ShakeEnabled=originalShake;
            // C10: repeat jump presses below the high step. A one-way underside must
            // not grant another takeoff near the first jump's apex.
            Ready(first,59);Ready(second,50);yield return new WaitForSeconds(.15f);
            float peak=0,previousV=0;bool airBoost=false;
            for(int step=0;step<60;step++)
            {
                first.Tick(Time.fixedDeltaTime,new SFCommand{jump=true});yield return new WaitForFixedUpdate();
                float vy=first.motor.Body.linearVelocity.y;peak=Mathf.Max(peak,first.Height);
                if(first.Height>.5f&&previousV<1&&vy>8)airBoost=true;previousV=vy;
            }
            Debug.Log("STACKED PLATFORM TRACE: peak="+peak.ToString("0.000")+" midairBoost="+airBoost);
            Check(!airBoost&&peak<2.2f,"jump presses below high platform cannot grant an unsupported second takeoff");
            Ready(first,52.7f);yield return new WaitForSeconds(.15f);
            for(int step=0;step<65;step++)
            {first.Tick(Time.fixedDeltaTime,new SFCommand{move=first.X<55.5f?Vector2.right:Vector2.zero,jump=step==0});yield return new WaitForFixedUpdate();}
            Check(first.motor.IsGrounded&&Mathf.Abs(first.Height-1.1f)<.12f,"normal jump reaches the first step from the street");
            for(int step=0;step<80;step++)
            {first.Tick(Time.fixedDeltaTime,new SFCommand{move=first.X<58.9f?Vector2.right:Vector2.zero,jump=step==0});yield return new WaitForFixedUpdate();}
            Check(first.motor.IsGrounded&&Mathf.Abs(first.Height-g.Platforms[3].height)<.12f,"normal jump from the first step lands on the high platform");
            var signal2=g.Pickups.First(p=>p.kind=="data"&&Mathf.Abs(p.x-59)<.1f);int beforeSignals=g.DataCount;float beforeBond=g.Bond;
            g.TryCollect(signal2,first);Check(signal2.taken,"signal two is collectible after climbing both platform steps");
            g.Camera.transform.position=new Vector3(58.5f,1,-10);yield return Capture("stacked-platform");
            signal2.taken=false;signal2.visual.enabled=true;g.DataCount=beforeSignals;g.Bond=beforeBond;
            // AI must use the lower step when the upper deck exceeds a street jump.
            // Arrange the start only; all subsequent travel uses real AI commands/physics.
            foreach(float startX in new[]{54f,58.4f,62f})
            {
                Ready(first,59.5f,2.42f);Ready(second,startX);yield return new WaitForSeconds(.2f);
                bool arrived=false;
                for(int step=0;step<650;step++)
                {
                    second.Tick(Time.fixedDeltaTime,g.CompanionCommand());yield return new WaitForFixedUpdate();
                    if(second.motor.IsGrounded&&Mathf.Abs(second.Height-2.4f)<.1f&&Mathf.Abs(second.motor.Body.linearVelocity.y)<.1f){arrived=true;break;}
                }
                Debug.Log("AI STEP TRACE: companion start="+startX+" end="+second.X.ToString("0.00")+","+second.Height.ToString("0.00"));
                Check(arrived,"partner reaches upper step from street x"+startX);
            }
            g.Camera.transform.position=new Vector3(58.5f,1,-10);yield return Capture("partner-upper-step");
            Ready(first,59.5f,2.42f);Ready(second,58.4f);yield return new WaitForSeconds(.2f);g.CompanionCommand();
            Ready(first,63);yield return new WaitForSeconds(.2f);var abandonedFollow=g.CompanionCommand();
            Check(abandonedFollow.move.x>0&&!abandonedFollow.jump,"partner abandons lower-step detour when lead returns to street");
            var climbEnemy=g.Actors.First(a=>!a.hero&&!a.ranged&&!a.boss);
            foreach(float startX in new[]{54f,58.4f,62f,66f})
            {
                Ready(first,59.5f,2.42f);Ready(second,60.7f,2.42f);first.invulnerable=second.invulnerable=100;
                Ready(climbEnemy,startX);climbEnemy.motor.Freeze(false);yield return new WaitForSeconds(.2f);
                bool arrived=false;
                for(int step=0;step<650;step++)
                {
                    var approach=g.EnemyCommand(climbEnemy);
                    if(startX==62&&step%20==0)Debug.Log($"AI PATH TRACE: tick={step} x={climbEnemy.X:F2} y={climbEnemy.Height:F2} vy={climbEnemy.motor.Body.linearVelocity.y:F2} waypoint={climbEnemy.CourseStepIndex} move={approach.move.x} jump={approach.jump} action={climbEnemy.action} first={first.X:F2},{first.Height:F2},{first.motor.Body.linearVelocity.y:F2},{first.motor.IsGrounded} second={second.X:F2},{second.Height:F2},{second.motor.Body.linearVelocity.y:F2},{second.motor.IsGrounded}");
                    climbEnemy.Tick(Time.fixedDeltaTime,approach);yield return new WaitForFixedUpdate();
                    if(climbEnemy.motor.IsGrounded&&Mathf.Abs(climbEnemy.Height-2.4f)<.1f&&Mathf.Abs(climbEnemy.motor.Body.linearVelocity.y)<.1f){arrived=true;break;}
                }
                Debug.Log("AI STEP TRACE: enemy start="+startX+" end="+climbEnemy.X.ToString("0.00")+","+climbEnemy.Height.ToString("0.00"));
                Check(arrived,"enemy reaches upper step from street x"+startX);
            }
            g.Camera.transform.position=new Vector3(58.5f,1,-10);yield return Capture("enemy-upper-step");
            Ready(first,59.5f,2.42f);Ready(second,70);Ready(climbEnemy,58.4f);yield return new WaitForSeconds(.2f);g.EnemyCommand(climbEnemy);
            Ready(first,63);yield return new WaitForSeconds(.2f);var abandonedClimb=g.EnemyCommand(climbEnemy);
            Check(abandonedClimb.move.x>0&&!abandonedClimb.jump,"enemy abandons lower-step detour when target returns to street");
            climbEnemy.health=0;climbEnemy.motor.Freeze(true);
            // Ranged and descent cases are separate from the melee ascent fixtures.
            var stepShooter=g.Actors.First(a=>!a.hero&&a.identity=="keel");
            foreach(var start in new[]{new Vector2(54,0),new Vector2(58.4f,0),new Vector2(62,0),new Vector2(66,0),new Vector2(55.5f,1.12f)})
            {
                Ready(first,59.5f,2.42f);Ready(second,60.7f,2.42f);first.invulnerable=second.invulnerable=100;
                Ready(stepShooter,start.x,start.y);stepShooter.EnemyAmmo.Reset(6,12);stepShooter.motor.Freeze(false);yield return new WaitForSeconds(.2f);
                bool arrived=false;
                for(int step=0;step<650;step++)
                {
                    var command=g.EnemyCommand(stepShooter);
                    stepShooter.Tick(Time.fixedDeltaTime,command);yield return new WaitForFixedUpdate();
                    if(stepShooter.motor.IsGrounded&&Mathf.Abs(stepShooter.Height-2.4f)<.1f&&Mathf.Abs(stepShooter.motor.Body.linearVelocity.y)<.1f){arrived=true;break;}
                }
                Debug.Log($"RANGED STEP TRACE: start={start} end={stepShooter.X:F2},{stepShooter.Height:F2} action={stepShooter.action}");
                Check(arrived,"ranged enemy reaches upper deck from "+start);
            }
            g.Camera.transform.position=new Vector3(58.5f,1,-10);yield return Capture("ranged-upper-step");
            stepShooter.health=0;stepShooter.motor.Freeze(true);
            foreach(var descending in new[]{climbEnemy,stepShooter})
            {
                Ready(first,54,1.12f);Ready(second,53.5f,1.12f);first.invulnerable=second.invulnerable=100;
                Ready(descending,59,2.42f);if(descending.HasEnemyFirearm)descending.EnemyAmmo.Reset(6,12);descending.motor.Freeze(false);yield return new WaitForSeconds(.2f);
                bool arrived=false;
                for(int step=0;step<450;step++)
                {
                    descending.Tick(Time.fixedDeltaTime,g.EnemyCommand(descending));yield return new WaitForFixedUpdate();
                    if(descending.motor.IsGrounded&&Mathf.Abs(descending.Height-1.1f)<.1f&&Mathf.Abs(descending.motor.Body.linearVelocity.y)<.1f){arrived=true;break;}
                }
                Debug.Log($"DESCENT TRACE: {descending.identity} end={descending.X:F2},{descending.Height:F2}");
                Check(arrived,descending.identity+" steps down to a hero on the lower deck");
                descending.health=0;descending.motor.Freeze(true);
            }
            Ready(first,54,1.12f);Ready(second,53.5f,1.12f);first.invulnerable=second.invulnerable=100;
            Ready(stepShooter,56.85f,1.12f);stepShooter.EnemyAmmo.Reset(6,12);stepShooter.motor.Freeze(false);yield return new WaitForSeconds(.2f);
            int idleHops=0;float lastVertical=0;
            for(int step=0;step<400;step++)
            {
                stepShooter.Tick(Time.fixedDeltaTime,g.EnemyCommand(stepShooter));yield return new WaitForFixedUpdate();
                float vertical=stepShooter.motor.Body.linearVelocity.y;
                if(lastVertical<1&&vertical>8)idleHops++;lastVertical=vertical;
            }
            Debug.Log($"RANGED HOLD TRACE: hops={idleHops} end={stepShooter.X:F2},{stepShooter.Height:F2} action={stepShooter.action}");
            Check(idleHops==0&&stepShooter.motor.IsGrounded&&Mathf.Abs(stepShooter.Height-1.1f)<.1f,"ranged enemy holds firing position without repeated traversal hops");
            g.Camera.transform.position=new Vector3(55.5f,1,-10);yield return Capture("ranged-hold");
            stepShooter.health=0;stepShooter.motor.Freeze(true);
            Ready(first,20);Ready(second,21);Down(first);
            Check(g.Player==second&&g.Partner==first&&g.State==SFState.Playing,"downed lead transfers control to standing partner");
            for(int n=0;n<300;n++)first.Tick(.1f,default);
            Check(!first.Alive,"course hero remains down after 30 simulated seconds");
            float bodyX=first.X;Ready(second,48);g.CompanionCommand();
            Check(Mathf.Abs(first.X-bodyX)<.01f,"downed body does not teleport when partner moves over 13 units away");
            g.Bond=0;Check(!g.TryInteract()&&!first.Alive,"distant contextual interaction cannot revive");
            Ready(second,21);g.Bond=0;Check(g.TryInteract()&&first.Alive&&g.Bond==0,"nearby E revive succeeds at zero bond");
            Check(first.health==55,"nearby revive returns 55 health");
            Down(first);Ready(second,25);g.Bond=34;g.AssistCooldown=0;
            Check(!g.TryAssist()&&!first.Alive&&g.Bond==34,"ranged revive rejects insufficient bond without charge");
            g.Bond=35;Check(g.TryAssist()&&first.Alive&&g.Bond==0&&g.AssistCooldown==6,"V revive spends exactly 35 bond and starts cooldown");
            Check(first.invulnerable>=2,"ranged revive retains full two-second recovery protection");
            Down(first);g.Bond=100;
            Check(!g.TryAssist()&&!first.Alive&&g.Bond==100,"assist cooldown blocks repeat revive without charge");
            g.AssistCooldown=0;Ready(second,29);
            Check(!g.TryAssist()&&!first.Alive&&g.Bond==100,"assist range remains seven units");
            Ready(second,21);g.Bond=0;g.Camera.transform.position=new Vector3(21,1,-10);g.NoticeTime=0;g.SpeechTime=0;
            Check(g.PartnerDownHint().Contains("E NEARBY")&&g.PartnerDownHint().Contains("35"),"keyboard down prompt names free nearby revive and assist cost");
            yield return Capture("partner-down");
            Down(second);g.enabled=true;yield return null;g.enabled=false;
            Check(g.State==SFState.Lost&&!g.Player.Alive&&!g.Partner.Alive,"both heroes down produces defeat on next game tick");
            yield return Capture("defeat");
            g.Notify("PARTNER DOWN");g.Checkpoint=62;g.RetryCheckpoint();
            Check(g.NoticeTime==0,"checkpoint retry clears stale partner-down notice");
            Check(g.State==SFState.Playing&&g.Fernando.health==g.Fernando.maxHealth&&g.Stefanie.health==g.Stefanie.maxHealth,"checkpoint retry restores both heroes and play state");
            Check(Mathf.Abs(g.Fernando.X-62)<.01f&&Mathf.Abs(g.Stefanie.X-60.5f)<.01f,"retry returns both heroes to the saved checkpoint");

            // Reproduce the reported entry-while-off then stationary/live case with a sleeping body.
            Ready(first,38);Ready(second,33);yield return new WaitForSeconds(.08f);g.Elapsed=1;g.TickHazards();
            Check(first.health==first.maxHealth,"inactive fault does no damage at its center");
            first.motor.Body.Sleep();Check(first.motor.Body.IsSleeping(),"stationary fixture is a sleeping Rigidbody2D");
            g.Elapsed=2;g.TickHazards();
            Check(first.health==first.maxHealth-13,"live fault damages a sleeping stationary hero");
            g.TickHazards();Check(first.health==first.maxHealth-13,"hazard respects invulnerability instead of per-frame stacking");
            first.Tick(.61f,default);g.TickHazards();
            Check(first.health==first.maxHealth-26,"remaining on live fault causes a subsequent timed hit");
            Ready(first,39.21f);g.TickHazards();Check(first.health==first.maxHealth,"outside visible hazard footprint is safe");
            Ready(first,38,.6f);g.TickHazards();Check(first.health==first.maxHealth,"jumping above fault clears damage height");
            Ready(first,38);yield return new WaitForSeconds(.08f);g.SetState(SFState.Paused);g.TickHazards();
            Check(first.health==first.maxHealth,"paused course never applies hazard damage");
            g.SetState(SFState.Playing);g.TickHazards();Check(first.health==first.maxHealth-13,"live hazard resumes with gameplay");
            Ready(second,38);yield return new WaitForSeconds(.08f);first.invulnerable=1;g.TickHazards();Check(second.health==second.maxHealth-13,"stationary partner is subject to same hazard rules");
            g.Camera.transform.position=new Vector3(38,1,-10);g.NoticeTime=0;g.SpeechTime=0;
            yield return Capture("live-hazard");
            var washes=g.World.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.name=="Electrical current span").ToArray();
            Check(washes.Length==3&&washes.All(s=>Mathf.Abs(s.bounds.size.x-1.5f)<.01f&&s.color.a>=.17f&&s.sortingOrder<50),"live electrical wash covers full damage span below contact shadows");
            g.Elapsed=1;g.TickHazards();Check(washes.All(s=>s.color.a==0),"safe electrical phase removes the full-span live wash");
            yield return Capture("safe-hazard");g.Elapsed=2;g.TickHazards();

            // Platform pursuit uses actual grounded collision and commands, not injected jump flags.
            Ready(first,54.5f,1.12f);Ready(second,55.5f,1.12f);
            var enemy=g.Actors.First(a=>!a.hero&&!a.ranged&&!a.boss);
            Ready(enemy,52.65f);enemy.motor.Freeze(false);
            yield return new WaitForSeconds(.35f);
            Check(enemy.motor.IsGrounded&&enemy.Height<.2f&&first.Height>.9f,"platform pursuit fixture has grounded enemy below hero");
            var pursuit=g.EnemyCommand(enemy);Check(pursuit.jump,"enemy directly below platform hero requests a jump");
            enemy.Tick(.02f,pursuit);yield return new WaitForSeconds(.22f);
            Check(enemy.Height>.5f,"platform pursuit jump moves enemy upward through actual physics");
            enemy.health=0;enemy.motor.Freeze(true);

            // Physical jog-speed takeoff windows across a continuously live strip, no immunity.
            foreach(float offset in new[]{1.8f,1.5f,1.2f})
            {
                Ready(first,75-offset);Ready(second,70);yield return new WaitForSeconds(.1f);
                first.Tick(.02f,new SFCommand{move=Vector2.right,jump=true});
                for(int step=0;step<55;step++)
                {yield return new WaitForFixedUpdate();g.Elapsed=2;g.TickHazards();first.Tick(Time.fixedDeltaTime,new SFCommand{move=Vector2.right});}
                Check(first.X>75.75f&&first.health==first.maxHealth,"jog jump clears live strip from "+offset.ToString("0.0")+" units before center");
            }

            Ready(g.Player,50.8f);Ready(g.Partner,52.65f);yield return new WaitForSeconds(.1f);
            Check(g.Player.Height<.2f&&g.Partner.Height<.2f,"unused-platform fixture places both heroes on the ground outside the platform collider");
            Check(!g.CompanionCommand().jump,"partner does not climb unused platform while lead remains on ground");
            Ready(g.Player,54.5f,1.12f);g.Player.lane=.4f;yield return new WaitForSeconds(.15f);
            g.Player.Tick(.2f,new SFCommand{move=Vector2.up});g.Player.Render(0);
            Check(g.Player.motor.IsGrounded&&g.Player.Height>.9f&&Mathf.Abs(g.Player.lane)<.01f,"grounded course actor settles to platform depth despite held lane input");
            Check(Mathf.Abs(g.Player.shadow.transform.position.y-(g.Player.Height-3+.035f))<.02f,"grounded platform shadow stays at the actor's feet");
            Ready(g.Player,25);Ready(g.Partner,25);yield return new WaitForSeconds(.08f);
            var follow=g.CompanionCommand();Check(follow.move.sqrMagnitude>0,"overlapping course partner chooses a separate formation position");
            Ready(g.Player,40);Ready(g.Partner,36.5f);yield return new WaitForSeconds(.08f);g.Elapsed=2;
            Check(g.CompanionCommand().move.x==0,"partner approaching live strip waits outside the damage area");
            Ready(g.Partner,38);yield return new WaitForSeconds(.08f);
            Check(g.CompanionCommand().move.x!=0,"partner already inside live strip moves out instead of waiting");

            // Reproduce waiting just beyond a strip, where hazard avoidance used to
            // choose the lead's exact position. Simulate commands and real bodies.
            foreach(float sign in new[]{-1f,1f})foreach(float offset in new[]{1.2f,1.8f,2.5f})
            {
                Ready(g.Player,75+sign*offset);Ready(g.Partner,75+sign*1.8f);
                yield return new WaitForSeconds(.08f);
                for(int step=0;step<65;step++)
                {g.Elapsed=2;g.Partner.Tick(Time.fixedDeltaTime,g.CompanionCommand());yield return new WaitForFixedUpdate();}
                Check(Mathf.Abs(g.Player.X-g.Partner.X)>1.1f&&Mathf.Abs(g.Partner.X-75)>.75f,"partner separates beside strip at signed lead offset "+(sign*offset));
            }
            Ready(g.Player,42);Ready(g.Partner,38);Ready(enemy,39.2f);enemy.motor.Freeze(false);
            yield return new WaitForSeconds(.08f);enemy.BeginAttack(SFAction.Punch);
            var escape=g.CompanionCommand();
            Check(!escape.punch&&!escape.guard&&escape.move.x!=0,"partner inside current prioritizes exit over nearby punch or guard");
            enemy.health=0;enemy.motor.Freeze(true);

            Ready(g.Player,15);Ready(g.Partner,11);yield return new WaitForSeconds(.1f);
            g.Player.Tick(.02f,new SFCommand{jump=true});bool crossed=false,keptDepth=true,keptShadow=true;
            for(int step=0;step<25;step++)
            {
                yield return new WaitForFixedUpdate();
                if(g.Player.motor.IsGrounded&&g.Player.Height>.3f&&g.Player.motor.Body.linearVelocity.y>.1f)
                {
                    crossed=true;g.Player.lane=.2f;g.Player.Tick(.02f,new SFCommand{move=Vector2.up});g.Player.Render(0);
                    keptDepth&=Mathf.Abs(g.Player.lane)>.1f;
                    keptShadow&=Mathf.Abs(g.Player.shadow.transform.position.y-(g.Player.Height-3+.035f))>.1f;
                }
            }
            Check(crossed,"jump fixture observes grounded overlap while rising through one-way platform");
            Check(crossed&&keptDepth,"rising through platform preserves lane input");
            Check(crossed&&keptShadow,"rising through platform keeps shadow on street");
            Ready(g.Player,13.6f);g.Player.lane=.55f;Ready(g.Partner,8);Ready(enemy,14.5f,1.12f);enemy.motor.Freeze(false);
            yield return new WaitForSeconds(.15f);g.EnemyCommand(enemy);
            Check(enemy.Busy,"platform enemy can answer street hero at lane clamp");
            enemy.health=0;enemy.motor.Freeze(true);

            Ready(g.Player,79);Ready(g.Partner,73.4f);yield return new WaitForSeconds(.1f);
            g.Elapsed=1.2f;Check(g.CompanionCommand().move.x==0,"resting partner waits when off window cannot cover crossing");
            g.Elapsed=.3f;Check(g.CompanionCommand().move.x>0,"resting partner crosses when off window has enough time");
            g.Elapsed=.85f;g.Partner.motor.Body.linearVelocity=new Vector2(5.2f,0);
            Check(g.CompanionCommand().move.x>0,"moving partner uses its speed rather than needless full-stop wait");
            g.Partner.motor.Body.linearVelocity=Vector2.zero;
            Ready(g.Player,90.3f);Ready(g.Partner,87);Ready(enemy,94.5f,1.52f);enemy.motor.Freeze(false);
            yield return new WaitForSeconds(.2f);bool monotonic=true;float last=enemy.X;
            for(int step=0;step<180&&enemy.Height>1.3f;step++)
            {enemy.Tick(Time.fixedDeltaTime,g.EnemyCommand(enemy));yield return new WaitForFixedUpdate();monotonic&=enemy.X<=last+.015f;last=enemy.X;}
            Check(monotonic&&enemy.Height<1.3f,"perched enemy walks consistently off its ledge toward the street hero");
            Ready(g.Player,60.6f);Ready(enemy,55.2f,1.12f);yield return new WaitForSeconds(.15f);
            Check(!g.EnemyCommand(enemy).jump,"lower street target does not trigger a p53 to p57 hop loop");
            enemy.health=0;enemy.motor.Freeze(true);

            Ready(g.Player,20);Ready(g.Partner,21);Ready(enemy,22);enemy.motor.Freeze(false);
            Check(!g.TryApproachClinch(g.Partner,new SFCommand{move=Vector2.right}),"course partner cannot enter a clinch without an AI follow-up");
            enemy.health=enemy.maxHealth/2;Ready(g.Player,5);Ready(g.Partner,6);
            Check(g.EnemyCommand(enemy).move.x<0,"damaged course enemy stays engaged beyond initial activation distance");
            enemy.health=0;enemy.motor.Freeze(true);
            var boss=g.Actors.First(a=>a.boss);Ready(boss,30);boss.BeginAttack(SFAction.Punch);boss.Damage(1,g.Player);
            Check(boss.action==SFAction.Punch,"course boss wind-up survives a light hit");
            boss.action=SFAction.Knocked;boss.moveAmount=1;boss.Render(0);
            Check(boss.CurrentSheet=="enemy"&&boss.CurrentFrame==6,"knocked Vesper uses kneeling silhouette despite stale movement");
            boss.action=SFAction.Held;boss.Render(0);
            Check(boss.CurrentSheet=="enemy"&&boss.CurrentFrame==6,"held Vesper does not stand in idle pose");
            boss.action=SFAction.Guard;boss.Render(0);
            Check(boss.CurrentSheet=="enemy"&&boss.CurrentFrame==4,"Vesper guard uses braced baton pose");
            boss.action=SFAction.Knocked;Ready(g.Player,28);Ready(g.Partner,26);g.Camera.transform.position=new Vector3(30,1,-10);
            yield return Capture("vesper-reaction");
            boss.health=0;boss.motor.Freeze(true);
            boss.Render(0);Check(boss.CurrentSheet=="enemy"&&boss.CurrentFrame==7,"defeated Vesper retains surrender silhouette");

            Ready(g.Player,129);Ready(g.Partner,124);g.DataCount=2;g.Notify("UNRELATED NOTICE");g.TryExtract();
            Check(g.Notice.Contains("three pieces of intel")&&g.InteractionHint().Contains("2 OF 3"),"extraction explains missing signals even while another notice is active");
            g.SetState(SFState.Playing);g.ShowHelp=true;g.SetState(SFState.Won);
            Check(!g.ShowHelp,"course result cannot remain covered by help");
            Check(g.CanQuitCourse==nativeQuit,"course result exposes Quit only in standalone player");
            var resultVoice=g.Audio.ResultVoice;int plays=g.Audio.ResultPlays;
            Check(resultVoice.clip!=null&&resultVoice.clip.name=="result_win"&&resultVoice.clip.length>1,"win selects the original success cue on its own audio source");
            yield return new WaitForSeconds(.18f);
            Check(resultVoice.isPlaying&&resultVoice.time>.04f,"success cue continues after simulation and world audio pause");
            float cueTime=resultVoice.time;g.SetState(SFState.Won);
            Check(g.Audio.ResultPlays==plays&&resultVoice.time>=cueTime-.02f,"repeated Won state cannot restart or duplicate result cue");
            float volume=g.Audio.Volume;g.Audio.SetMasterVolume(0);yield return null;
            Check(resultVoice.mute&&resultVoice.volume==0,"master mute silences result source");
            g.Audio.SetMasterVolume(volume);g.SetState(SFState.Playing);
            Check(!resultVoice.isPlaying&&resultVoice.clip==null,"leaving results clears lingering cue");
            g.SetState(SFState.Lost);yield return new WaitForSeconds(.18f);
            Check(resultVoice.clip!=null&&resultVoice.clip.name=="result_fail"&&resultVoice.isPlaying&&resultVoice.time>.04f,"defeat has a distinct cue that survives stopped simulation");
            g.RetryCheckpoint();Check(!resultVoice.isPlaying&&resultVoice.clip==null,"checkpoint retry stops defeat cue immediately");
            g.SetState(SFState.Playing);g.PauseForInterruption();
            Check(g.State==SFState.Paused,"browser interruption pauses active gameplay");
            Check(g.CanQuitCourse==nativeQuit,"course pause exposes Quit only in standalone player");
            var courseHUD=g.GetComponent<SFHUD>();float quitAt=Time.unscaledTime;
            Check(!courseHUD.ConfirmCourseQuit(quitAt)&&courseHUD.CourseQuitArmed(quitAt),"first Quit press only arms confirmation");
            Check(!courseHUD.ConfirmCourseQuit(quitAt+.1f),"fast Quit double-click cannot exit");
            Check(courseHUD.ConfirmCourseQuit(quitAt+.5f),"deliberate second Quit press confirms after label delay");
            Check(!courseHUD.ConfirmCourseQuit(quitAt+3.1f),"expired Quit confirmation requires a fresh first press");
            g.SetState(SFState.Won);Check(!courseHUD.CourseQuitArmed(quitAt+3.2f),"moving to a different result card disarms Quit");
            g.SetState(SFState.Paused);courseHUD.ConfirmCourseQuit(quitAt+4);
            g.SetState(SFState.Playing);g.SetState(SFState.Paused);
            Check(!courseHUD.CourseQuitArmed(quitAt+4.1f),"resume then pause disarms Quit without requiring a rendered playing frame");
            courseHUD.ConfirmCourseQuit(quitAt+5);g.SetState(SFState.Paused);
            Check(courseHUD.ConfirmCourseQuit(quitAt+5.5f),"repeating the same pause state does not interrupt deliberate confirmation");
            courseHUD.DisarmCourseQuit();
            yield return CaptureUI("pause");

            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
            g.SetState(SFState.Menu);g.EnableTouch("1");InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputSystem.Update();g.RefreshControlTakeover();
            Check(g.TouchControls,"menu mouse press preserves touch toggle state until the UI handles release");
            g.SetState(SFState.Paused);InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputSystem.Update();g.RefreshControlTakeover();
            Check(g.TouchControls,"pause controls do not change layout on mouse press");
            g.ShowHelp=true;g.RefreshControlTakeover();Check(g.TouchControls,"touch help stays accessible when clicking its back button");
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();g.SetState(SFState.Playing);
            g.RefreshControlTakeover();Check(g.TouchControls,"completed menu click cannot switch touch layout after resume");
            g.EnableTouch("1");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));InputSystem.Update();g.RefreshControlTakeover();
            Check(!g.TouchControls&&g.ReadCommand().move.x>0,"physical keyboard takes over touch controls and retains the same-frame move");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            g.EnableTouch("1");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F7));InputSystem.Update();g.RefreshControlTakeover();
            Check(g.TouchControls,"F7 remains an explicit toggle instead of being consumed as takeover");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));InputSystem.Update();g.RefreshControlTakeover();
            Check(!g.TouchControls,"physical mouse button takes over touch controls");
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            g.SetState(SFState.Paused);g.EnableTouch("1");g.RefreshTouchLayout(900,1600);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));InputSystem.Update();g.RefreshControlTakeover();
            Check(!g.TouchControls&&!g.LayoutBlocked&&g.State==SFState.Paused,"physical keyboard escapes portrait touch block without resuming the mission");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();g.EnableTouch("1");g.RefreshTouchLayout(900,1600);
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputSystem.Update();g.RefreshControlTakeover();
            Check(!g.TouchControls&&!g.LayoutBlocked&&g.State==SFState.Paused,"physical mouse escapes portrait touch block without a hidden menu action");
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();g.SetState(SFState.Playing);
            g.EnableTouch("1");g.TouchControls=false;g.RestoreTouchPreference();
            Check(g.TouchControls,"scene initialization restores explicit touch preference");
            g.EnableTouch("0");g.InitializeTouch("1");Check(!g.TouchControls,"automatic coarse-pointer initialization does not override an explicit choice");
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.P));InputSystem.Update();g.ProcessPauseAndRetryKeys();
            Check(g.State==SFState.Paused,"P pauses course without requiring browser Escape");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.P));InputSystem.Update();g.ProcessPauseAndRetryKeys();
            Check(g.State==SFState.Playing,"P resumes paused course");
            g.SetState(SFState.Paused);g.ShowHelp=true;InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.P));InputSystem.Update();g.ProcessPauseAndRetryKeys();
            Check(!g.ShowHelp&&g.State==SFState.Paused,"P closes course help while keeping the pause card");
            g.CourseMode=false;g.SetState(SFState.Playing);InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.P));InputSystem.Update();g.ProcessPauseAndRetryKeys();
            Check(g.State==SFState.Paused,"P pauses campaign as advertised by the browser footer");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.P));InputSystem.Update();g.ProcessPauseAndRetryKeys();
            Check(g.State==SFState.Playing,"P resumes campaign without changing mission mode");g.CourseMode=true;
            Down(first);Down(second);g.SetState(SFState.Lost);g.Checkpoint=62;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));InputSystem.Update();g.ProcessPauseAndRetryKeys();
            Check(g.State==SFState.Playing&&first.Alive&&second.Alive&&Mathf.Abs(g.Fernando.X-62)<.01f,"Enter retries actual defeat at saved checkpoint");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            var pad=InputSystem.AddDevice<Gamepad>();pad.MakeCurrent();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.LeftShoulder));InputSystem.Update();g.ProcessGamepad();
            InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();g.ProcessGamepad();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.LeftShoulder).WithButton(GamepadButton.DpadUp));InputSystem.Update();g.ProcessGamepad();
            var guarded=g.ReadCommand();Check(guarded.interact&&!guarded.backup,"course controller guard plus Up still interacts instead of calling backup");
            InputSystem.RemoveDevice(pad);g.ProcessGamepad();
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
            g.SetState(SFState.Playing);

            Ready(first,20);Ready(second,21);g.CourseMode=false;Down(first);
            for(int n=0;n<79;n++)first.Tick(.1f,default);
            Check(!first.Alive,"party self-recovery does not happen before eight seconds");
            first.Tick(.2f,default);Check(first.Alive&&first.health==45,"party campaign retains eight-second self-recovery");
            g.CourseMode=true;Down(first);Down(second);g.SetState(SFState.Lost);
            yield return CaptureUI("defeat");
            bool ok=checks.All(s=>s.StartsWith("PASS:"));string result="COURSE RULES QA COMPLETE: "+(ok?"PASS":"FAIL")+" / "+checks.Count+" checks";
            Debug.Log(result);
            if(Application.platform!=RuntimePlatform.WebGLPlayer){File.WriteAllLines(Path.Combine(qa,"course-rules-v0846.txt"),checks.Concat(new[]{result}));Application.Quit(ok?0:2);}
        }
    }
}
