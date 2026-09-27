#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace StefanieAndFernando
{
    // Opt-in executable check for the C10 feel pack (SFFeel, SFHUDFeel, SFAtmosphere and enemy variety): -sfCourse -sfFeel,
    // optionally -sfChapter N. Never attached during ordinary play. Writes QA/feel-checks.txt and prints FEEL QA COMPLETE: PASS|FAIL.
    public sealed class SFFeelCheck:MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        void Check(bool pass,string name){results.Add((pass?"PASS: ":"FAIL: ")+name);Debug.Log(results.Last());}
        static void Ready(SFActor a){a.invulnerable=0;a.health=a.maxHealth;a.action=SFAction.Idle;}
        static bool Same(Color a,Color b)=>Mathf.Abs(a.r-b.r)<.002f&&Mathf.Abs(a.g-b.g)<.002f&&Mathf.Abs(a.b-b.b)<.002f&&Mathf.Abs(a.a-b.a)<.002f;
        static bool Moved(Vector3[] a,Vector3[] b)=>a.Length==b.Length&&a.Length>0&&a.Where((p,i)=>(p-b[i]).sqrMagnitude>1e-6f).Any();
        static bool Held(Vector3[] a,Vector3[] b)=>a.Length==b.Length&&a.Length>0&&a.Select((p,i)=>(p-b[i]).sqrMagnitude<1e-10f).All(x=>x);
        IEnumerator Start()
        {
            var g=GetComponent<SFGame>();yield return null;
            int chapterIndex=g.CourseChapter;
            Check(g.CourseMode,"feel check runs on the class course (chapter "+chapterIndex+")");
            Check(!g.FeelActive&&!g.PresentationVariety&&!g.AtmosphereActive&&Time.timeScale==1,"automated runs keep feel, variety and atmosphere off unless a check opts in");
            g.FeelForced=true;g.ShakeEnabled=true;yield return null;
            Check(!g.FeelActive&&Time.timeScale==1&&g.AtmosphereCount==0,"the menu never alters time and builds no atmosphere");
            g.StartGame();yield return new WaitForSecondsRealtime(.3f);
            Check(g.FeelActive&&Time.timeScale==1,"live play starts at normal time");
            var p=g.Player;var enemies=g.Actors.Where(a=>!a.Friendly).ToArray();
            foreach(var e in enemies)e.cooldown=100;
            var grunt=enemies.First(a=>!a.boss);
            // The final boss stands last on every route.
            var boss=enemies.Where(a=>a.boss).OrderByDescending(a=>a.X).First();
            p.action=SFAction.Idle;

            // Atmosphere: built for this chapter in live play, drifting, frozen on pause, hidden without the opt-in.
            int moteCount=chapterIndex==2?64:chapterIndex==1?44:34;
            Check(g.AtmosphereCount==moteCount&&g.AtmosphereShown,"live play builds and shows the chapter's "+moteCount+" atmosphere motes");
            var drift=g.AtmospherePositions;yield return new WaitForSecondsRealtime(.25f);
            Check(Moved(drift,g.AtmospherePositions)&&g.AtmosphereShown,"the atmosphere drifts in live play");
            g.SetState(SFState.Paused);yield return null;var paused=g.AtmospherePositions;yield return new WaitForSecondsRealtime(.3f);
            Check(Held(paused,g.AtmospherePositions)&&g.AtmosphereShown,"pause freezes the atmosphere in place");
            g.SetState(SFState.Playing);g.FeelForced=false;yield return null;
            Check(g.AtmosphereCount==moteCount&&g.AtmosphereHidden,"without the opt-in the atmosphere is hidden");
            g.FeelForced=true;yield return null;
            Check(g.AtmosphereShown,"opting back in shows the atmosphere again");

            // Enemy variety counts persons, not identities (SFEnemyProfiles.Person): a repeated person's later instance tints differently,
            // whichever identity it carries; heroes and bosses never do. Chapter 0 has one identity per person, so its twins are latch/latch.
            string Person(SFActor a)=>SFEnemyProfiles.Person(a.identity);
            var regulars=enemies.Where(a=>!a.boss).ToArray();
            var twins=regulars.GroupBy(Person).FirstOrDefault(t=>t.Count()>1)?.ToArray();
            Check(twins!=null,"the chapter repeats a regular enemy person");
            if(twins!=null)
            {
                var first=twins[0];var second=twins[1];Ready(first);Ready(second);first.Render(0);second.Render(0);
                var forcedFirst=first.visual.color;var forcedSecond=second.visual.color;
                Check(first.Variety==0&&second.Variety==1&&!Same(forcedFirst,forcedSecond),"forced: "+Person(first)+" #2 ("+second.identity+") tints differently from #1 ("+first.identity+"), which stays unchanged");
                if(first.CurrentSheet=="idle"&&second.CurrentSheet=="idle")Check(first.CurrentFrame!=second.CurrentFrame,"forced: the twins' idle loops run out of phase");
                g.FeelForced=false;first.Render(0);second.Render(0);
                // Two identities of one person may differ in their own right (a fallback draws each with its profile tint), so the plain
                // colours are compared only between equal identities; the forced #2 is always its plain colour times the first variant.
                bool plainTwins=first.identity!=second.identity||Same(first.visual.color,second.visual.color);
                Check(first.Variety==0&&second.Variety==0&&Same(first.visual.color,forcedFirst)&&Same(second.visual.color*SFActor.VarietyTint(1),forcedSecond)&&plainTwins,"without the opt-in neither takes a variety tint"+(first.identity==second.identity?" and the twins render identically":""));
                g.FeelForced=true;
            }
            // Every regular: its rank is the number of same-person actors created before it, and each repeat (rank 1+) renders away from
            // its plain colour while each first instance stays plain.
            foreach(var a in regulars){Ready(a);a.Render(0);}
            var ranks=regulars.Select(a=>a.Variety).ToArray();var tinted=regulars.Select(a=>a.visual.color).ToArray();
            g.FeelForced=false;foreach(var a in regulars)a.Render(0);var plainColors=regulars.Select(a=>a.visual.color).ToArray();g.FeelForced=true;
            var order=g.Actors.ToList();var wrong=new List<string>();
            for(int i=0;i<regulars.Length;i++)
            {
                var a=regulars[i];int want=order.TakeWhile(b=>b!=a).Count(b=>Person(b)==Person(a));
                if(ranks[i]!=want||(want>0)==Same(tinted[i],plainColors[i]))wrong.Add(a.identity+"@"+a.X.ToString("0.0")+" rank "+ranks[i]+" want "+want);
            }
            Check(wrong.Count==0,"forced: every regular's variety rank counts earlier instances of its person, and only repeats tint"+(wrong.Count>0?": "+string.Join("; ",wrong):""));
            // Rio fields the smuggler and his shotgun variant: one person under two identities, so the later of the two always tints.
            var smuggler=regulars.FirstOrDefault(a=>a.identity=="smuggler");var shotgun=regulars.FirstOrDefault(a=>a.identity=="smuggler_shotgun");
            if(chapterIndex==1)Check(smuggler!=null&&shotgun!=null,"Rio fields both the smuggler and the shotgun smuggler");
            if(smuggler!=null&&shotgun!=null)
            {
                var early=order.IndexOf(smuggler)<order.IndexOf(shotgun)?smuggler:shotgun;var late=early==smuggler?shotgun:smuggler;
                Ready(early);Ready(late);early.Render(0);late.Render(0);var earlyColor=early.visual.color;var lateColor=late.visual.color;
                g.FeelForced=false;late.Render(0);var latePlain=late.visual.color;g.FeelForced=true;late.Render(0);
                Check(SFEnemyProfiles.Person("smuggler_shotgun")=="smuggler"&&early.Variety==0&&late.Variety>=1&&!Same(lateColor,latePlain)&&!Same(lateColor,earlyColor),"forced: the shotgun smuggler is the smuggler's own person, so the later of the two ("+late.identity+" at x"+late.X.ToString("0")+", rank "+late.Variety+") takes a variant tint");
            }
            var bosses=enemies.Where(a=>a.boss).ToArray();var heroes=new[]{g.Fernando,g.Stefanie};
            foreach(var a in bosses.Concat(heroes))a.Render(0);
            var forced=bosses.Concat(heroes).Select(a=>a.visual.color).ToArray();bool plain=bosses.Concat(heroes).All(a=>a.Variety==0);
            g.FeelForced=false;foreach(var a in bosses.Concat(heroes))a.Render(0);
            Check(plain&&bosses.Concat(heroes).Select((a,i)=>Same(a.visual.color,forced[i])).All(x=>x),"bosses and heroes never take a variety tint");
            g.FeelForced=true;yield return null;

            // Hit-stop.
            Ready(grunt);Check(grunt.Damage(6,p)&&Time.timeScale<.1f&&Mathf.Abs(g.HitStopRemaining-.05f)<.001f,"a light hit by the lead hero freezes the frame for .05 s");
            yield return new WaitForSecondsRealtime(.3f);
            Check(Time.timeScale==1&&g.HitStopRemaining==0,"hit-stop releases within .3 s of real time");
            Ready(grunt);grunt.Damage(20,p);Check(g.HitStopRemaining>=.079f,"a heavy hit holds the freeze longer");
            yield return new WaitForSecondsRealtime(.3f);
            Ready(p);grunt.action=SFAction.Idle;Check(p.Damage(4,grunt)&&Time.timeScale<.1f,"an enemy's hit on the lead hero freezes the frame");
            yield return new WaitForSecondsRealtime(.3f);p.health=p.maxHealth;
            // The lead's own knockout: Defeated() passes the lead to the living partner before the hit reports, and the freeze still plays.
            int lead=g.Selected;Ready(p);Ready(g.Partner);grunt.action=SFAction.Idle;p.Damage(9999,grunt);
            Check(!p.Alive&&g.Player!=p&&Time.timeScale<.1f&&Mathf.Abs(g.HitStopRemaining-.12f)<.001f,"the lead hero's knockout freezes the frame after the lead passes to the partner");
            p.Revive();g.Selected=lead;yield return new WaitForSecondsRealtime(.3f);p.health=p.maxHealth;
            Ready(g.Partner);grunt.action=SFAction.Idle;g.Partner.Damage(9999,grunt);
            Check(!g.Partner.Alive&&g.Player==p&&Time.timeScale==1&&g.HitStopRemaining==0,"the partner's knockout away from the lead never freezes the frame");
            g.Partner.Revive();g.Partner.health=g.Partner.maxHealth;

            Ready(grunt);g.Partner.action=SFAction.Idle;grunt.Damage(6,g.Partner);
            Check(Time.timeScale==1&&g.HitStopRemaining==0,"partner exchanges never freeze the lead's frame");
            Ready(grunt);p.action=SFAction.Shoot;grunt.Damage(6,p);p.action=SFAction.Idle;
            Check(Time.timeScale==1&&g.HitStopRemaining==0,"gunfire never freezes the frame");
            Ready(p);p.Damage(13,null,true);p.health=p.maxHealth;
            Check(Time.timeScale==1&&g.HitStopRemaining==0,"hazard damage never freezes the frame");

            Ready(grunt);grunt.Damage(6,p);Check(Time.timeScale<.1f,"freeze armed before pause");
            g.SetState(SFState.Paused);yield return null;
            Check(Time.timeScale==1,"pausing restores normal time");
            g.SetState(SFState.Playing);yield return null;
            Check(Time.timeScale==1&&g.HitStopRemaining==0,"resuming never replays a stale freeze");

            g.ShakeEnabled=false;Ready(grunt);grunt.Damage(6,p);
            Check(Time.timeScale==1&&g.HitStopRemaining==0,"camera impact motion OFF also disables hit-stop");
            g.ShakeEnabled=true;Ready(grunt);grunt.Damage(6,p);bool armed=Time.timeScale<.1f;g.ShakeEnabled=false;yield return null;
            Check(armed&&Time.timeScale==1&&g.HitStopRemaining==0,"switching camera impact motion OFF mid-freeze restores normal time");
            g.ShakeEnabled=true;
            g.FeelForced=false;Ready(grunt);grunt.Damage(6,p);yield return null;
            Check(Time.timeScale==1&&g.HitStopRemaining==0,"SmokeMode without opt-in keeps hit-stop off");
            g.FeelForced=true;

            // Boss title card.
            var chapter=g.Chapter;
            g.Say("STEFANIE","Check line.");Check(g.BossCardTime==0,"a hero's line raises no title card");
            g.Say(chapter.bossName,"Check line.");
            Check(g.BossCardTime>SFGame.BossCardLength-.05f&&g.BossCardName==chapter.bossName&&g.BossCardRole=="BOSS  /  "+chapter.city,"the boss's first line raises its title card");
            // Chapters 1-2 fight on raised tiers behind the band (SFHUD draws the card before the world labels), so its fill stays light there.
            var hud=GetComponent<SFHUD>();
            Check(hud!=null&&(g.InChapter?hud.CardFill<=.35f:hud.CardFill>=.8f),"the title card band is "+(g.InChapter?"light over chapter tiers":"solid over Curitiba's open sky")+" (fill "+(hud!=null?hud.CardFill.ToString("0.00"):"none")+")");
            // The role line (y 332-358) moves under the name (y 414-438) only when a world label covers its row and that row is clear.
            Rect top=new Rect(700,332,160,26),low=new Rect(700,414,160,24),hazardBox=new Rect(655,318,250,38),dataLabel=new Rect(710,427,140,25);
            Check(!SFHUD.DropRole(new List<Rect>(),top,low)&&SFHUD.DropRole(new List<Rect>{hazardBox},top,low)&&!SFHUD.DropRole(new List<Rect>{hazardBox,dataLabel},top,low)&&!SFHUD.DropRole(new List<Rect>{dataLabel},top,low),"the card's role line drops under the name only when a label covers its row and the lower row is clear");
            yield return new WaitForSecondsRealtime(.5f);
            float left=g.BossCardTime;g.Say(chapter.bossName,"Check line.");
            Check(g.BossCardTime<=left&&left<SFGame.BossCardLength-.3f,"the title card shows once per run and counts down");
            g.SetState(SFState.Paused);left=g.BossCardTime;yield return new WaitForSecondsRealtime(.3f);
            Check(g.BossCardTime==left,"pause holds the title card");
            g.SetState(SFState.Playing);yield return new WaitForSecondsRealtime(SFGame.BossCardLength);
            Check(g.BossCardTime==0,"the title card clears after its run time");
            foreach(var beat in chapter.beats.Where(b=>b.actor>=0&&b.actor<chapter.cast.Length&&chapter.cast[b.actor].boss&&!chapter.cast[b.actor].final))
            {g.Say(beat.speaker,beat.line);Check(g.BossCardName==beat.speaker&&g.BossCardRole=="MID-BOSS  /  "+chapter.city,"mid-boss "+beat.speaker+" gets a MID-BOSS card");}

            // Boss knockout beat.
            Ready(boss);boss.cooldown=100;p.action=SFAction.Idle;boss.Damage(9999,p);
            Check(!boss.Alive&&g.KnockoutFlash>.9f&&Time.timeScale<.1f,"a boss knockout freezes and flashes");
            yield return new WaitForSecondsRealtime(.45f);
            Check(Time.timeScale>.25f&&Time.timeScale<1,"the knockout eases through slow motion");
            yield return new WaitForSecondsRealtime(1.2f);
            Check(Time.timeScale==1&&g.KnockoutFlash==0,"normal time and a clear screen return after the knockout beat");
            Check(g.State==SFState.Playing,"the knockout beat leaves the course playable");
            // A boss finished by gunfire (course heroes carry sidearms) gets the same beat; ordinary gunfire above still never freezes.
            Ready(boss);boss.cooldown=100;p.action=SFAction.Shoot;boss.Damage(9999,p);p.action=SFAction.Idle;
            Check(!boss.Alive&&g.KnockoutFlash>.9f&&Time.timeScale<.1f,"a boss finished by gunfire also freezes and flashes");
            yield return new WaitForSecondsRealtime(1.7f);
            Check(Time.timeScale==1&&g.KnockoutFlash==0&&g.State==SFState.Playing,"normal time returns after a gunfire knockout beat");

            // Every exit from live play releases an armed freeze: a restart (the game component going away), defeat, the menu, victory.
            Ready(grunt);grunt.Damage(6,p);armed=Time.timeScale<.1f;g.enabled=false;
            Check(armed&&Time.timeScale==1&&g.HitStopRemaining==0,"a restart (OnDisable) restores normal time");
            g.enabled=true;yield return null;
            Ready(grunt);grunt.Damage(6,p);armed=Time.timeScale<.1f;g.SetState(SFState.Lost);yield return null;
            Check(armed&&Time.timeScale==1,"the defeat card restores normal time");
            g.SetState(SFState.Playing);yield return null;
            Ready(grunt);grunt.Damage(6,p);armed=Time.timeScale<.1f;g.SetState(SFState.Menu);yield return null;
            Check(armed&&Time.timeScale==1&&g.AtmosphereHidden,"the menu restores normal time and hides the atmosphere");
            g.SetState(SFState.Playing);yield return null;
            Ready(grunt);grunt.Damage(6,p);armed=Time.timeScale<.1f;g.SetState(SFState.Won);yield return null;
            Check(armed&&Time.timeScale==1,"the victory card restores normal time");

            g.FeelForced=false;yield return null;Check(Time.timeScale==1&&!g.PresentationVariety&&g.AtmosphereHidden,"time scale is normal when the check releases feel");
            bool failed=results.Any(x=>x.StartsWith("FAIL"));
            string verdict="FEEL QA COMPLETE: "+(failed?"FAIL":"PASS");results.Add(verdict);
            string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(qa);File.WriteAllLines(Path.Combine(qa,"feel-checks.txt"),results);
            Debug.Log(verdict);
            Application.Quit(failed?2:0);
        }
    }
}
#endif
