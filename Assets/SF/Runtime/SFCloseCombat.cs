using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public static class SFGroundAnimation
    {
        public const float FirstHit=.5f,Interval=.24f;
        public const int Hits=6;
        // Contact poses bracket the same timestamps that drive damage; guards alternate between them.
        public static int Frame(float clock)
        {
            if(clock<FirstHit-.06f||clock>FirstHit+(Hits-1)*Interval+.10f)return 0;
            int beat=Mathf.Clamp(Mathf.FloorToInt((clock-FirstHit+.06f)/Interval),0,Hits-1);
            float phase=clock-(FirstHit+beat*Interval);
            return phase<=.08f?(beat%2==0?1:3):(beat%2==0?2:0);
        }
    }
    public sealed partial class SFActor
    {
        public bool GroundSequence {get;private set;}
        public int GroundHits {get;private set;}
        public float GripReentryDelay {get;private set;}
        public string GroundLabel=>GroundSequence?(GroundHits%2==0?"JAB":"STRAIGHT")+"  /  "+GroundHits+" OF 6  /  MOVE TO RISE":"";
        public bool TryGroundSequence()
        {
            if(!hero||Armed||!Alive||Busy||Restrained||action==SFAction.Hurt||cooldown>0||!motor.IsGrounded)return false;
            if(GripTarget==null&&!game.TryGrapple(this,1.4f,true))return false;
            if(GripTarget==null||GripTarget.HeldBy!=this||!GripTarget.Alive)return false;
            ResetCombo();GroundSequence=true;GroundHits=0;action=SFAction.GroundStrike;actionClock=0;
            attackDuration=2.15f;attackAt=.5f;cooldown=attackDuration;GripTime=3;
            GripTarget.action=SFAction.Knocked;GripTarget.actionClock=0;
            motor.SetCommand(0,false);moveAmount=0;
            game.Notify(game.TouchControls?"TAKEDOWN  /  MOVE OR EVADE TO RISE":game.GamepadActive?"TAKEDOWN  /  STICK OR B TO RISE":"TAKEDOWN  /  MOVE OR X TO RISE");
            return true;
        }
        bool TickGroundSequence(SFCommand c)
        {
            if(!GroundSequence)return false;
            var target=GripTarget;
            bool escape=actionClock>=.30f&&(c.move.sqrMagnitude>.04f||c.dash||c.jump||c.guard||c.ground||c.toggleArmed||c.cycleWeapon||c.weapon!=0);
            if(target==null||!target.Alive||target.HeldBy!=this||!game.ClearAttack(this,target,false)||Mathf.Abs(X-target.X)>1.6f||Mathf.Abs(lane-target.lane)>.5f||Mathf.Abs(Height-target.Height)>.35f||escape||actionClock>=attackDuration)
            {ReleaseGrip(true);action=SFAction.Idle;actionClock=0;cooldown=.18f;Render(0);return true;}
            motor.SetCommand(0,false);moveAmount=0;Crouching=Aiming=false;
            target.actionClock=0; // The owned opponent cannot get up midway through this short sequence.
            while(GroundHits<SFGroundAnimation.Hits&&actionClock>=SFGroundAnimation.FirstHit+GroundHits*SFGroundAnimation.Interval)
            {
                GroundHits++;
                if(target.Damage(game.OutgoingDamage(this,7,SFAction.GroundStrike),this,true,true))game.RegisterHit(this);
                if(!target.Alive||!GroundSequence)break;
            }
            if(!GroundSequence){action=SFAction.Idle;actionClock=0;cooldown=.18f;}
            Render(0);return true;
        }
    }
    public sealed partial class SFGame
    {
        public bool TryApproachClinch(SFActor actor,SFCommand command)
        {
            if(!actor.hero||(CourseMode&&actor!=Player)||actor.Armed||actor.GripTarget!=null||actor.GripReentryDelay>0||actor.cooldown>0||actor.Busy||actor.Restrained||actor.action==SFAction.Hurt||!actor.motor.IsGrounded||command.move.sqrMagnitude<.04f)return false; // Course partner AI has no clinch follow-up; running past an opponent must not lock it in a grip.
            if(command.punch||command.kick||command.shoot||command.guard||command.aim||command.crouch||command.jump||command.dash||command.grapple||command.ground||command.toggleArmed||command.cycleWeapon||command.weapon!=0)return false;
            var target=Actors.Where(a=>a.Friendly!=actor.Friendly&&a.Alive&&!a.Restrained&&a.HeldBy==null&&(!a.boss||a.health<=a.maxHealth*.35f)&&Mathf.Abs(a.Height-actor.Height)<.35f&&SFMath.WithinStrike(actor.GroundPosition,actor.Height,actor.facing,a.GroundPosition,a.Height,1.05f,.38f))
                .Where(a=>Vector2.Dot(command.move,a.GroundPosition-actor.GroundPosition)>.08f&&ClearAttack(actor,a,false)).OrderBy(a=>Vector2.Distance(a.GroundPosition,actor.GroundPosition)).FirstOrDefault();
            if(target==null)return false;
            return TryGrapple(actor,1.05f,true,target);
        }
    }
}
