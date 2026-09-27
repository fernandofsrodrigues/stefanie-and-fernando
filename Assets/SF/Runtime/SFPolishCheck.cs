#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed class SFPolishCheck:MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        SFGame g;
        void Check(bool pass,string label){results.Add((pass?"PASS: ":"FAIL: ")+label);Debug.Log(results.Last());}
        void Isolate()
        {
            foreach(var a in g.Actors.Where(a=>!a.hero)){a.Teleport(new Vector2(130,0));a.cooldown=100;}
            g.Partner.Teleport(new Vector2(110,0));g.Player.invulnerable=g.Partner.invulnerable=100;
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return null;
            foreach(string id in new[]{"silk","ratchet","foreman","cantilever"})
            {var art=Resources.Load<SFArt>("SF/"+id);Check(art!=null&&art.frames.Length==8&&art.frames.All(f=>f!=null),id+" has eight usable sprite entries");}
            for(int route=0;route<6;route++)
            {
                g.SelectRoute(route);yield return null;
                Check(g.Actors.Count(a=>!a.Friendly)==20&&g.Actors.Count(a=>a.boss)==1&&g.Actors.Any(a=>a.identity=="silk")&&g.Actors.Single(a=>a.boss).identity==(route==2?"cantilever":route>=3?"foreman":"vesper"),"route "+route+" replaces its cast without duplicate enemies");
            }
            g.SelectRoute(0);yield return null;g.StartGame();yield return new WaitForSeconds(.3f);Isolate();
            Check(Mathf.Abs(g.Covers[1].height-g.Covers[1].visual.bounds.size.y)<.01f,"side-on cover protection height matches its visible top");
            var p=g.Player;p.Teleport(new Vector2(8,0));p.lane=-.85f;
            // Includes the body-impact banks added after this original polish fixture.
            var banks=new[]{("pistol",2),("rifle",2),("shotgun",2),("step",3),("grass",3),("hit",3),("heavy",3),("cloth",3),("cover",3),("bodyhit",5),("bodyheavy",5)};
            Check(banks.All(b=>g.Audio.BankSize(b.Item1)==b.Item2)&&g.Audio.LoadedClips==34&&g.Audio.MusicTracks==4,"all 11 recorded SFX banks (34 clips) and four music tracks load");
            var music=g.GetComponents<AudioSource>().FirstOrDefault(s=>s.clip!=null&&(s.clip.name.StartsWith("music_")||s.clip.name=="pressure")&&s.isPlaying);
            Check(music!=null,"music starts with gameplay");
            g.SetState(SFState.Paused);yield return new WaitForSeconds(.12f);float musicTime=music!=null?music.time:0;
            yield return new WaitForSeconds(.15f);Check(music!=null&&music.isPlaying&&music.time>musicTime,"pause keeps quiet soundtrack playing");
            g.SetState(SFState.Playing);yield return new WaitForSeconds(.15f);Check(music!=null&&music.isPlaying,"music resumes with gameplay");
            for(int hero=0;hero<2;hero++)
            {
                g.Selected=hero;Isolate();p=g.Player;p.Armed=false;p.Teleport(new Vector2(8,0));p.lane=-.85f;yield return new WaitForSeconds(.2f);
                var sprite=p.visual.sprite;var scale=p.visual.transform.localScale;bool stable=true;
                for(int i=0;i<12;i++){yield return new WaitForSeconds(.08f);stable&=p.visual.sprite==sprite&&p.visual.transform.localScale==scale;}
                Check(stable&&p.CurrentSheet=="idle",p.identity+" idle has no whole-body pulsing");
            }
            g.Selected=0;Isolate();p=g.Player;
            var enemy=g.Actors.First(a=>!a.Friendly);p.Teleport(new Vector2(53,0));p.lane=0;p.facing=1;p.ShotRifle=true;
            enemy.Teleport(new Vector2(59,0));enemy.lane=0;enemy.invulnerable=0;float health=enemy.health;
            g.ResolveAttack(p,SFAction.Shoot);
            Check(enemy.health==health&&g.ActiveTracers>0&&g.LastShotEndpoint.x<56,"rifle tracer and damage both stop at tall cover");
            g.SetState(SFState.Paused);int active=g.ActiveTracers;yield return new WaitForSeconds(.2f);Check(g.ActiveTracers==active,"pause freezes tracer lifetime");
            g.SetState(SFState.Playing);yield return new WaitForSeconds(.2f);Check(g.ActiveTracers==0,"short tracer returns to pool");
            p.lane=enemy.lane=-.85f;enemy.invulnerable=0;health=enemy.health;g.ResolveAttack(p,SFAction.Shoot);
            Check(enemy.health<health&&Mathf.Abs(g.LastShotEndpoint.x-enemy.X)<.1f&&g.Audio.LastPlayed=="rifle","clear shot hits target and selects rifle recording");
            p.ShotRifle=false;g.ResolveAttack(p,SFAction.Shoot);Check(g.Audio.LastPlayed=="pistol","sidearm selects separate recording bank");
            for(int i=0;i<40;i++)g.ResolveAttack(p,SFAction.Shoot);
            Check(g.ActiveTracers<=32,"shot burst respects bounded tracer pool");yield return new WaitForSeconds(.2f);
            Check(g.ActiveTracers==0,"pooled burst leaves no permanent beams");
            Isolate();p.Teleport(new Vector2(8,0));p.lane=-.85f;p.Armed=false;p.facing=1;
            enemy.Teleport(new Vector2(9,0));enemy.lane=-.85f;enemy.Revive();enemy.health=64;enemy.invulnerable=0;enemy.cooldown=100;
            yield return new WaitForSeconds(.15f);Check(g.TryGrapple(p),"grounded opponent enters clinch");
            g.SmokeCommand=new SFCommand{punch=true};yield return new WaitForSeconds(.48f);
            Check(p.ClinchHits==1&&p.GripTarget==enemy&&enemy.HeldBy==p&&Mathf.Abs(enemy.health-45.1f)<.01f,"first knee lands while retaining clinch");g.Capture("v05-clinch");
            yield return new WaitForSeconds(.8f);g.SmokeCommand=default;
            Check(p.ClinchHits==2&&p.GripTarget==enemy&&enemy.HeldBy==p&&Mathf.Abs(enemy.health-26.2f)<.01f,"second knee retains the grip with exactly two hits");
            g.SmokeCommand=new SFCommand{punch=true};float kneeDeadline=Time.realtimeSinceStartup+1.5f;
            while(p.ClinchHits<3&&Time.realtimeSinceStartup<kneeDeadline)yield return null;
            g.SmokeCommand=default;
            Check(p.ClinchHits==3&&p.GripTarget==null&&enemy.HeldBy==null&&enemy.action==SFAction.Knocked&&Mathf.Abs(enemy.health-7.3f)<.01f,"third knee releases opponent into knockdown with exactly three hits (hits "+p.ClinchHits+", health "+enemy.health.ToString("0.00")+", state "+enemy.action+")");
            yield return new WaitForSeconds(.5f);p.cooldown=0;enemy.Revive();enemy.invulnerable=0;enemy.cooldown=100;enemy.Teleport(new Vector2(p.X+1,0));enemy.lane=p.lane;
            Check(g.TryGrapple(p),"clinch can be reacquired after recovery");p.invulnerable=0;p.Damage(5,enemy,true);
            Check(p.GripTarget==null&&enemy.HeldBy==null,"damage interrupts and releases clinch");
            yield return new WaitForSeconds(.4f);p.invulnerable=100;Isolate();
            var ranged=g.Actors.First(a=>a.ranged&&!a.Friendly);var cover=g.Covers[0];
            p.Teleport(new Vector2(cover.footprint.xMin-2.5f,0));p.lane=0;
            ranged.Teleport(new Vector2(cover.footprint.xMax+.6f,0));ranged.lane=0;ranged.cooldown=0;ranged.action=SFAction.Idle;ranged.health=ranged.maxHealth;
            yield return new WaitForSeconds(.2f);Check(ranged.CoverChoice==cover&&ranged.Crouching&&g.ShotCover(p,ranged)==cover,"ranged AI shelters behind low cover");g.Capture("v05-enemy-cover");
            bool fired=false;float until=Time.realtimeSinceStartup+4.5f;
            while(Time.realtimeSinceStartup<until){fired|=ranged.action==SFAction.Shoot;yield return null;}
            Check(fired,"ranged AI leaves low stance and fires during its exposed phase");
            Isolate();cover=g.Covers[2];p.Teleport(new Vector2(cover.footprint.xMin-2.5f,0));p.lane=0;
            ranged.Teleport(new Vector2(cover.footprint.xMax+.6f,0));ranged.lane=0;ranged.cooldown=0;ranged.action=SFAction.Idle;ranged.CoverChoice=null;
            fired=false;until=Time.realtimeSinceStartup+5;
            while(Time.realtimeSinceStartup<until){fired|=ranged.action==SFAction.Shoot;yield return null;}
            Check(fired,"ranged AI can step past tall-cover edge and obtain a clear firing angle");
            Isolate();var silk=g.Actors.First(a=>a.identity=="silk");p.Teleport(new Vector2(25,0));p.lane=-.85f;
            silk.Teleport(new Vector2(26.6f,0));silk.lane=-.85f;silk.cooldown=0;silk.action=SFAction.Idle;
            yield return new WaitForSeconds(.12f);Check(silk.action==SFAction.Kick&&silk.actionClock<silk.attackAt,"martial-artist attack starts with a visible wind-up");g.Capture("v05-kick-windup");
            string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));File.WriteAllLines(Path.Combine(qa,"polish-checks.txt"),results);
            Application.Quit(results.Any(x=>x.StartsWith("FAIL"))?2:0);
        }
    }
}
#endif
