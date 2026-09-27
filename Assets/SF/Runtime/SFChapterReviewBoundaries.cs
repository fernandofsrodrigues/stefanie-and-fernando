using System.Collections;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFChapterCheck
    {
        IEnumerator ReviewR2Boundaries(SFActor boss,SFPlatform deck)
        {
            foreach(int side in new[]{-1,1})
            {
                yield return ReviewGrip(boss,deck,side);
                foreach(var hero in new[]{g.Fernando,g.Stefanie})yield return ReviewFinisher(hero,boss,deck,side);
            }
            // Rio has only a boss holding its tier. The aerial shove intentionally
            // excludes bosses, so exercise actual non-boss guards from Anbar's cast.
            var guard=Foes.FirstOrDefault(a=>a.HoldTier&&!a.boss);
            if(guard!=null)
            {
                int index=System.Array.IndexOf(Foes,guard);var spawn=g.Chapter.cast[index];
                var guardDeck=g.Platforms[DeckAt(spawn.x,spawn.height)];
                foreach(int side in new[]{-1,1})yield return ReviewAerial(guard,guardDeck,side);
            }
            else Info("R2 aerial guard shove is exercised on Anbar's real non-boss guard; Rio has no eligible cast member.");
            if(g.Chapter.key=="rio")
            {
                yield return ReviewStepStrip(40,38.4f,46.2f);
                yield return ReviewStepStrip(89.4f,91,84.8f);
            }
            else
            {
                yield return ReviewStepStrip(21.6f,20,27.5f);
                yield return ReviewStepStrip(21.6f,23.2f,17);
            }
        }

        bool ReviewOnDeck(SFActor target,SFPlatform deck)=>target.Alive&&Standing(target)&&
            target.X>=deck.x+.45f&&target.X<=deck.x+deck.width-.45f&&Near(target.Height,deck.height,.12f);

        IEnumerator ReviewGrip(SFActor target,SFPlatform deck,int side)
        {
            ReviewReset();float edge=side<0?deck.x:deck.x+deck.width;
            Ready(g.Player,edge-side*.8f,deck.height+.05f);
            Ready(g.Partner,edge-side*4,deck.height+.05f);
            target.motor.Freeze(false);Ready(target,edge-side*.55f,deck.height+.05f);
            yield return new WaitForSeconds(.3f);
            bool placed=Standing(g.Player)&&ReviewOnDeck(target,deck);
            target.health=target.maxHealth*.3f;g.Player.facing=side;
            float proposed=g.Player.X+side*.85f;
            bool crosses=side<0?proposed<deck.x:proposed>deck.x+deck.width;
            bool held=g.TryGrapple(g.Player,quiet:true,chosen:target);
            float pulledX=target.X;g.Player.ReleaseGrip();
            yield return new WaitForSeconds(.3f);
            Check(placed&&crosses&&held,"R2 actual edge grip requests a pull beyond deck, direction "+side);
            Check(held&&ReviewOnDeck(target,deck),"R2 grip leaves guard supported: pulled "+pulledX.ToString("F2")+", end "+target.X.ToString("F2")+"/"+target.Height.ToString("F2"));
        }

        IEnumerator ReviewFinisher(SFActor hero,SFActor target,SFPlatform deck,int side)
        {
            g.Selected=hero==g.Fernando?0:1;ReviewReset();float edge=side<0?deck.x:deck.x+deck.width;
            Ready(hero,edge-side*1.8f,deck.height+.05f);hero.facing=side;
            Ready(g.Partner,edge-side*4,deck.height+.05f);
            target.motor.Freeze(false);Ready(target,edge-side*.6f,deck.height+.05f);
            yield return new WaitForSeconds(.3f);
            bool placed=Standing(hero)&&ReviewOnDeck(target,deck),connected=true;
            int length=SFCombo.Length(hero.identity);
            for(int step=0;step<length;step++)
            {
                float hp=target.health;hero.Tick(Time.fixedDeltaTime,new SFCommand{punch=true});
                float duration=hero.attackDuration+.1f;
                for(float t=0;t<duration;t+=Time.fixedDeltaTime)
                {
                    hero.Tick(Time.fixedDeltaTime,default);target.Tick(Time.fixedDeltaTime,default);
                    yield return new WaitForFixedUpdate();
                }
                connected&=hero.ActiveComboStep==step&&target.health<hp;
            }
            Check(placed&&connected&&hero.ComboMove.finisher,"R2 "+hero.identity+" connects the full "+length+"-hit combo at edge "+side);
            Check(connected&&ReviewOnDeck(target,deck),"R2 finisher keeps held boss on deck: "+target.X.ToString("F2")+"/"+target.Height.ToString("F2"));
        }

        IEnumerator ReviewAerial(SFActor target,SFPlatform deck,int side)
        {
            ReviewReset();float edge=side<0?deck.x:deck.x+deck.width;
            target.motor.Freeze(false);Ready(target,edge-side*.6f,deck.height+.05f);
            Ready(g.Partner,3,.05f);yield return new WaitForSeconds(.3f);
            bool placed=ReviewOnDeck(target,deck);
            Ready(g.Player,edge-side*1.9f,deck.height+.75f);g.Player.facing=side;
            yield return new WaitForFixedUpdate();
            bool airborne=!g.Player.motor.IsGrounded;float hp=target.health;
            g.Player.Tick(Time.fixedDeltaTime,new SFCommand{kick=true});
            bool started=g.Player.FlyingKick;
            for(int n=0;n<30;n++)
            {
                g.Player.Tick(Time.fixedDeltaTime,default);target.Tick(Time.fixedDeltaTime,default);
                yield return new WaitForFixedUpdate();
            }
            bool hit=g.Player.AerialHits.Contains(target)&&target.health<hp;
            Check(placed&&airborne&&started&&hit,"R2 airborne input kick hits real "+target.identity+" guard at edge "+side);
            Check(hit&&ReviewOnDeck(target,deck),"R2 aerial shove keeps guard supported: "+target.X.ToString("F2")+"/"+target.Height.ToString("F2"));
        }

        IEnumerator ReviewStepStrip(float hazard,float buddyX,float leadX)
        {
            ReviewReset();Ready(g.Player,leadX,3.05f);Ready(g.Partner,buddyX,.05f);
            yield return new WaitForSeconds(.3f);
            bool placed=Standing(g.Player)&&Standing(g.Partner)&&Near(g.Player.Height,3,.12f)&&g.Partner.Height<.12f;
            float hp=g.Partner.health,previous=g.Partner.X,largestStep=0;
            bool waypoint=false,wait=false,crossed=false,arrived=false;
            int side=leadX>buddyX?1:-1;
            for(int n=0;n<420;n++)
            {
                var c=g.CompanionCommand();waypoint|=g.Partner.CourseStepIndex>=0;
                wait|=g.Partner.CourseStepIndex>=0&&g.HazardActive&&c.move.x==0&&Mathf.Abs(g.Partner.X-hazard)>1.2f;
                g.Partner.Tick(Time.fixedDeltaTime,c);yield return new WaitForFixedUpdate();
                g.Elapsed+=Time.fixedDeltaTime;g.TickHazards();
                largestStep=Mathf.Max(largestStep,Mathf.Abs(g.Partner.X-previous));previous=g.Partner.X;
                crossed|=(g.Partner.X-hazard)*side>1.2f;
                arrived|=Standing(g.Partner)&&Near(g.Partner.Height,3,.15f)&&Mathf.Abs(g.Partner.X-leadX)<6;
            }
            Check(placed&&waypoint&&wait,"R2 step route waits before live street strip "+hazard+", direction "+side);
            Check(placed&&crossed&&arrived&&largestStep<.5f&&Near(g.Partner.health,hp),
                "R2 step route crosses and climbs without damage/teleport: damage "+(hp-g.Partner.health).ToString("F2")+", max step "+largestStep.ToString("F2")+", end "+g.Partner.X.ToString("F2")+"/"+g.Partner.Height.ToString("F2"));
        }
    }
}
