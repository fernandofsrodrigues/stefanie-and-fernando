#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace StefanieAndFernando
{
    // Opt-in executable integration suite, never attached during ordinary play.
    public sealed class SFSandboxCheck:MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        void Check(bool pass,string name){results.Add((pass?"PASS: ":"FAIL: ")+name);Debug.Log(results.Last());}
        IEnumerator Start()
        {
            var g=GetComponent<SFGame>();yield return null;
            for(int r=0;r<6;r++)
            {
                g.SelectRoute(r);yield return null;
                Check(g.Covers.Count==5&&g.Pickups.Count==6&&g.Platforms.Length==0&&g.Hazards.Length==0,"route "+SFGame.RouteNames[r]+" has cover/supplies and no placeholder course");
                Check(g.World.GetComponentsInChildren<SpriteRenderer>().Where(sr=>sr.name.Contains(" / plate ")).All(sr=>sr.bounds.min.y<-.53f-5&&sr.bounds.max.y>6.2f),"background plates cover viewport and camera shake margin in "+SFGame.RouteNames[r]);
                g.Camera.transform.position=new Vector3(54,1,-10);g.Capture("v03-route-"+r);
                g.SetState(SFState.Playing);g.Player.Teleport(new Vector2(7,0));g.Player.lane=-.8f;g.Partner.Teleport(new Vector2(5,0));
                g.BackupCooldown=0;g.CallBackup();yield return new WaitForSeconds(5.6f);
                Check(g.Actors.Count(a=>a.ally&&a.identity=="ally_"+SFGame.RouteAllies[r])==2,"regional reinforcement mapping "+SFGame.AllyNames[r]);
                g.Capture("v03-backup-"+r);g.BackupRemaining=.01f;yield return new WaitForSeconds(.1f);g.SetState(SFState.Menu);
            }
            g.BackupCooldown=0;g.SelectRoute(0);yield return null;g.StartGame();yield return new WaitForSeconds(.3f);
            foreach(var e in g.Actors.Where(a=>!a.Friendly)){e.cooldown=100;e.Teleport(new Vector2(130,0));}
            var p=g.Player;g.Partner.Teleport(new Vector2(110,0));
            g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Check(p.motor.Body.linearVelocity.x>0&&p.motor.Body.linearVelocity.x<5.2f,"horizontal movement accelerates instead of snapping");
            yield return new WaitForSeconds(.3f);g.SmokeCommand=default;yield return new WaitForSeconds(.2f);
            Check(Mathf.Abs(p.motor.Body.linearVelocity.x)<.1f,"release brakes to a stop");
            p.Teleport(new Vector2(12.5f,0));p.lane=0;g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};yield return new WaitForSeconds(1);
            Check(p.X<13.75f,"physical movement stops at planter footprint");
            g.SmokeCommand=new SFCommand{move=Vector2.down};yield return new WaitForSeconds(.6f);
            g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};yield return new WaitForSeconds(1);
            Check(p.X>16.5f&&p.lane<-.7f,"depth-lane detour passes cover");g.SmokeCommand=default;
            var c=g.Covers[0];
            Check(c.BlocksMovement(new Vector2(12,-.35f),new Vector2(18,-.35f)),"navigation includes body clearance outside raw cover footprint");
            Check(c.Blocks(new Vector2(12,0),.85f,new Vector2(18,0),.85f),"low cover blocks crouched ray");
            Check(!c.Blocks(new Vector2(12,0),1.8f,new Vector2(18,0),1.55f),"standing ray clears low cover");
            Check(c.Blocks(new Vector2(18,0),.85f,new Vector2(12,0),.85f),"cover blocks both shooting directions");
            Check(!c.Blocks(new Vector2(12,-.8f),.85f,new Vector2(18,-.8f),.85f),"ray outside footprint is unobstructed");
            Check(g.Covers[2].Blocks(new Vector2(53,0),1.8f,new Vector2(59,0),1.55f),"tall cover blocks standing ray");
            var enemy=g.Actors.First(a=>!a.Friendly);p.Teleport(new Vector2(53,0));p.lane=0;p.facing=1;enemy.Teleport(new Vector2(59,0));enemy.lane=0;enemy.invulnerable=0;
            Check(g.ShotEndpoint(p,12).x<56,"untargeted shot visual stops at tall cover");
            float hp=enemy.health;g.ResolveAttack(p,SFAction.Shoot);Check(enemy.health==hp,"actual damage resolution respects tall cover");
            p.lane=enemy.lane=-.85f;g.ResolveAttack(p,SFAction.Shoot);Check(enemy.health<hp,"actual damage applies from clear lane");
            p.Teleport(new Vector2(10,0));p.lane=-.75f;p.health=p.maxHealth;g.Partner.health=g.Partner.maxHealth;
            var food=g.Pickups.First(x=>x.kind=="food");g.TryCollect(food);Check(!food.taken,"full-health team preserves food");
            p.health=50;g.TryCollect(food);Check(food.taken&&p.health==80,"food heals by authored amount");g.TryCollect(food);Check(p.health==80,"healing cannot be collected twice");
            Check(g.CallBackup()&&!g.CallBackup(),"backup call accepts once and rejects spam");
            float arrival=g.BackupArrival;g.SetState(SFState.Paused);yield return new WaitForSeconds(.25f);
            Check(g.BackupArrival==arrival,"pause freezes backup timer");g.SetState(SFState.Playing);yield return new WaitForSeconds(2.8f);
            var allies=g.Actors.Where(a=>a.ally).ToArray();Check(allies.Length==2&&allies.All(a=>a.Friendly&&!a.hero&&a.identity=="ally_tigre"),"regional call deploys two separate friendly AI actors");
            if(allies.Length>0){float ah=allies[0].health;allies[0].invulnerable=0;Check(!allies[0].Damage(25,p)&&allies[0].health==ah,"friendly-fire damage rejects allied team");}
            // Leave combat decisions and damage to the ally AI; isolate its target from the companion.
            p.Teleport(new Vector2(9,0));p.lane=-1.1f;g.Partner.Teleport(new Vector2(110,0));
            enemy.Teleport(new Vector2(18,0));enemy.lane=-.85f;enemy.health=enemy.maxHealth;enemy.invulnerable=0;enemy.cooldown=100;enemy.action=SFAction.Idle;
            foreach(var ally in allies){ally.Teleport(new Vector2(11,0));ally.lane=-.85f;ally.cooldown=0;ally.action=SFAction.Idle;}
            hp=enemy.health;yield return new WaitForSeconds(2.5f);
            Check(enemy.health<hp,"autonomous allies acquire and damage a hostile in a clear lane");g.Capture("v03-ally-engagement");
            int ko=g.KOs;g.BackupRemaining=.01f;yield return new WaitForSeconds(.1f);
            Check(!g.Actors.Any(a=>a.ally)&&g.KOs==ko&&g.BackupCooldown>0,"withdrawal removes allies without enemy KO credit and retains cooldown");
            // Continuous traversal under ordinary movement commands; invulnerability isolates navigation from balance.
            // Movement alone can clinch a nearby hostile. Park the completed combat
            // fixtures behind the start so this timed navigation leg does not stop
            // for grips (the combat/clinch suites exercise those separately).
            foreach(var actor in g.Actors)actor.ReleaseGrip();
            foreach(var hostile in g.Actors.Where(a=>!a.Friendly))
            {
                hostile.Teleport(new Vector2(1,0));hostile.lane=1.1f;
                hostile.cooldown=1000;hostile.action=SFAction.Idle;hostile.motor.Freeze(true);
            }
            p.Teleport(new Vector2(3,0));p.lane=-.85f;g.Partner.Teleport(new Vector2(1,0));g.Partner.lane=-.85f;
            g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};float until=Time.realtimeSinceStartup+35,traceAt=0;int nextSeam=1;
            while(p.X<130&&Time.realtimeSinceStartup<until)
            {
                p.invulnerable=g.Partner.invulnerable=1;
                if(p.X>22*nextSeam){g.Capture("v03-scroll-"+nextSeam);nextSeam++;}
                if(Time.realtimeSinceStartup>=traceAt){traceAt=Time.realtimeSinceStartup+3;Debug.Log("SANDBOX TRAVERSE: x="+p.X.ToString("F2")+" lane="+p.lane.ToString("F2")+" speed="+p.motor.Body.linearVelocity.x.ToString("F2")+" action="+p.action+" grip="+(p.GripTarget!=null?p.GripTarget.identity:"none")+" held="+(p.HeldBy!=null?p.HeldBy.identity:"none")+" seams="+(nextSeam-1));}
                yield return null;
            }
            Debug.Log("SANDBOX TRAVERSE END: x="+p.X.ToString("F2")+" lane="+p.lane.ToString("F2")+" action="+p.action+" grip="+(p.GripTarget!=null?p.GripTarget.identity:"none")+" seams="+(nextSeam-1));
            g.SmokeCommand=default;Check(p.X>=130&&nextSeam>=6,"continuous traversal crosses all five background joins");
            Check(g.State==SFState.Playing,"sandbox has no objective/extraction gate");g.Capture("v03-sandbox-end");
            string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));File.WriteAllLines(Path.Combine(qa,"sandbox-checks.txt"),results);
            Application.Quit(results.Any(x=>x.StartsWith("FAIL"))?2:0);
        }
    }
}
#endif
