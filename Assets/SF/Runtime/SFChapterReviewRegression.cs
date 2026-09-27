using System.Collections;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFChapterCheck
    {
        // Opt-in real-body probes, kept identical across the old and repaired builds.
        // Run with -sfCourse -sfChapterCheck -sfReviewR2 -sfChapter 1 (or 2).
        IEnumerator ReviewR2()
        {
            g.ShakeEnabled=false;g.StartGame();g.enabled=false;
            Check(g.State==SFState.Playing,"R2 starts the requested chapter");
            var ch=g.Chapter;var boss=g.FinalBoss;
            int f=System.Array.FindIndex(ch.cast,e=>e.final);
            Check(f>=0&&boss!=null,"R2 final boss exists");
            if(f<0||boss==null)yield break;
            var fd=ch.cast[f];var deck=g.Platforms[DeckAt(fd.x,fd.height)];
            ReviewReset();
            yield return Stage(boss,deck.x+2,deck.height,new Vector2(deck.x+8,0),new Vector2(deck.x+9.5f,0));
            yield return Chase(boss,deck.height,2);
            Check(!offDeck&&hops==0&&minX>=deck.x+1.8f&&maxX>=deck.x+3.5f,
                "R2 held boss follows heroes below its deck: x "+minX.ToString("F2")+".."+maxX.ToString("F2")+", start "+(deck.x+2));
            foreach(int side in new[]{-1,1})yield return ReviewThrow(boss,deck,side);
            foreach(int side in new[]{-1,1})yield return ReviewLeash(side);
            var kicker=Foes.FirstOrDefault(a=>a.LayoutId=="silk"||a.LayoutId=="cantilever");
            Check(kicker!=null,"R2 chapter has a kicker for grounded and guard checks");
            if(kicker!=null){yield return ReviewKickStart(kicker);yield return ReviewKickGuard(kicker);}
            var z=ch.hazards.First(h=>h.height==(ch.key=="rio"?3:1.5f));
            foreach(int side in new[]{-1,1})yield return ReviewStrip(z.x,z.height,side);
            yield return ReviewR2Boundaries(boss,deck);
            Info("R2 real-body probes are isolated regression evidence, not a substitute for complete routes or rendered UI review.");
        }

        void ReviewReset()
        {
            foreach(var a in g.Actors)
            {
                a.ReleaseGrip();a.motor.Freeze(true);a.ThrowClock=0;a.ThrowHits.Clear();
                a.AerialAttack=false;a.GuardTime=0;a.CounterTime=0;
                if(!a.hero)a.health=0;
            }
            g.Player.motor.Freeze(false);g.Partner.motor.Freeze(false);
            Ready(g.Player,3,.05f);Ready(g.Partner,1.5f,.05f);
            g.Player.Armed=g.Partner.Armed=false;g.Elapsed=2;g.Bond=70;
        }

        IEnumerator ReviewThrow(SFActor target,SFPlatform deck,int side)
        {
            ReviewReset();
            float edge=side<0?deck.x:deck.x+deck.width;
            Ready(g.Player,edge-side*1.35f,deck.height+.05f);
            Ready(g.Partner,edge-side*4,deck.height+.05f);
            target.motor.Freeze(false);Ready(target,edge-side*.6f,deck.height+.05f);
            yield return new WaitForSeconds(.3f);
            target.health=target.maxHealth*.3f;g.Player.facing=side;
            bool placed=Standing(g.Player)&&Standing(target)&&Near(target.Height,deck.height,.12f);
            bool grip=g.TryGrapple(g.Player,quiet:true,chosen:target);
            bool thrown=grip&&g.ThrowEnemy(g.Player);float low=target.X,high=target.X;
            for(int n=0;n<45;n++)
            {
                target.Tick(Time.fixedDeltaTime,default);yield return new WaitForFixedUpdate();
                low=Mathf.Min(low,target.X);high=Mathf.Max(high,target.X);
            }
            Check(placed&&grip&&thrown,"R2 real grapple and throw starts at "+(side<0?"left":"right")+" deck edge");
            Check(thrown&&target.Alive&&target.ThrowClock<=0&&low>=deck.x+.45f&&high<=deck.x+deck.width-.45f&&Near(target.Height,deck.height,.12f),
                "R2 throw ends on held guard's deck: x "+low.ToString("F2")+".."+high.ToString("F2")+", h "+target.Height.ToString("F2"));
        }

        IEnumerator ReviewLeash(int side)
        {
            ReviewReset();
            float leadX=side<0?109:127,buddyX=side<0?126:110;
            Ready(g.Player,leadX,1.55f);Ready(g.Partner,buddyX,4.55f);
            yield return new WaitForSeconds(.35f);
            bool placed=Standing(g.Player)&&Standing(g.Partner)&&Near(g.Player.Height,1.5f,.12f)&&Near(g.Partner.Height,4.5f,.12f);
            float firstStep=0,previous=g.Partner.X;
            for(int n=0;n<50;n++)
            {
                g.Partner.Tick(Time.fixedDeltaTime,g.CompanionCommand());yield return new WaitForFixedUpdate();g.Elapsed+=Time.fixedDeltaTime;
                if(n==0)firstStep=Mathf.Abs(g.Partner.X-previous);
            }
            Check(placed&&firstStep>13,"R2 horizontal leash starts with live upper-deck contacts and teleports "+firstStep.ToString("F2"));
            Check(placed&&Standing(g.Partner)&&Near(g.Partner.Height,1.5f,.12f)&&Mathf.Abs(g.Partner.X-g.Player.X)<6,
                "R2 horizontal leash settles on lower lead deck: x "+g.Partner.X.ToString("F2")+", h "+g.Partner.Height.ToString("F3"));
        }

        IEnumerator ReviewKickStart(SFActor foe)
        {
            ReviewReset();foe.motor.Freeze(false);
            Ready(g.Player,6.5f,.05f);Ready(g.Partner,2,.05f);Ready(foe,5,.75f);
            foe.motor.Body.linearVelocity=new Vector2(0,-1);yield return new WaitForFixedUpdate();
            bool falling=!foe.motor.IsGrounded&&foe.motor.Body.linearVelocity.y<0;
            var c=g.EnemyCommand(foe);bool airborneStart=foe.action==SFAction.Kick;
            foe.Tick(Time.fixedDeltaTime,c);yield return new WaitForFixedUpdate();
            Check(falling&&!airborneStart,"R2 a falling chapter kicker cannot start an aerial kick (started "+airborneStart+")");
            Ready(foe,5,.05f);yield return new WaitForSeconds(.25f);
            bool grounded=Standing(foe);g.EnemyCommand(foe);
            Check(grounded&&foe.action==SFAction.Kick&&!foe.AerialAttack,"R2 grounded kicker still begins its ordinary kick");
        }

        IEnumerator ReviewKickGuard(SFActor foe)
        {
            ReviewReset();foe.motor.Freeze(false);
            Ready(g.Player,2,.05f);Ready(g.Partner,5,.05f);Ready(foe,6.9f,.05f);
            yield return new WaitForSeconds(.25f);
            foe.facing=-1;g.Partner.facing=1;foe.BeginAttack(SFAction.Kick);
            bool guarding=false;float hp=g.Partner.health;
            // Hold spacing and suppress the partner's counter-punch to isolate its guard
            // choice. The actual enemy attack and Damage/block/parry path still run.
            int steps=Mathf.CeilToInt((foe.attackAt+.12f)/Time.fixedDeltaTime);
            for(int n=0;n<steps;n++)
            {
                var c=g.CompanionCommand();guarding|=c.guard;c.move=Vector2.zero;c.punch=false;c.jump=false;
                g.Partner.Tick(Time.fixedDeltaTime,c);foe.Tick(Time.fixedDeltaTime,default);
                yield return new WaitForFixedUpdate();
            }
            float damage=hp-g.Partner.health;
            Check(guarding&&damage>=0&&damage<=5,"R2 partner guards an actual kick wind-up and limits damage: "+damage.ToString("F2"));
        }

        IEnumerator ReviewStrip(float x,float height,int side)
        {
            ReviewReset();float lower=height-1.5f;
            Ready(g.Player,x+side*1.9f,height+.05f);Ready(g.Partner,x-side*3.6f,lower+.05f);
            yield return new WaitForSeconds(.3f);
            bool placed=Standing(g.Player)&&Standing(g.Partner)&&Near(g.Player.Height,height,.12f)&&Near(g.Partner.Height,lower,.12f);
            g.Partner.motor.Body.linearVelocity=new Vector2(side*5.2f,0);
            float hp=g.Partner.health,peak=g.Partner.Height;bool arrived=false;
            for(int n=0;n<150;n++)
            {
                g.Partner.Tick(Time.fixedDeltaTime,g.CompanionCommand());yield return new WaitForFixedUpdate();
                g.Elapsed+=Time.fixedDeltaTime;g.TickHazards();peak=Mathf.Max(peak,g.Partner.Height);
                arrived|=Standing(g.Partner)&&Near(g.Partner.Height,height,.15f)&&Mathf.Abs(g.Partner.X-g.Player.X)<6;
            }
            Check(placed,"R2 running approach starts clear of upper strip at "+x+", direction "+side);
            Check(placed&&arrived&&Near(g.Partner.health,hp),"R2 upper-strip approach reaches lead without strip damage: damage "+(hp-g.Partner.health).ToString("F2")+", peak "+peak.ToString("F2")+", end "+g.Partner.X.ToString("F2")+"/"+g.Partner.Height.ToString("F2"));
        }
    }
}
