using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public static class SFAerialKick
    {
        public const float Contact=.12f, ActiveEnd=.38f, Duration=.60f, LandingRecovery=.16f;

        // Sweep the hero's root through a forward foot/torso contact volume. Coordinates are X, lane, elevation.
        public static bool Sweep(Vector3 from,Vector3 to,int facing,Vector3 target,out float fraction)
        {
            float near=0,far=1;
            var min=new Vector3(target.x-(facing>0?2.2f:.12f),target.y-.55f,target.z-.60f);
            var max=new Vector3(target.x+(facing>0?.12f:2.2f),target.y+.55f,target.z+1.55f);
            for(int axis=0;axis<3;axis++)
            {
                float delta=to[axis]-from[axis];
                if(Mathf.Abs(delta)<.000001f){if(from[axis]<min[axis]||from[axis]>max[axis]){fraction=0;return false;}continue;}
                float a=(min[axis]-from[axis])/delta,b=(max[axis]-from[axis])/delta;
                near=Mathf.Max(near,Mathf.Min(a,b));far=Mathf.Min(far,Mathf.Max(a,b));
                if(near>far){fraction=0;return false;}
            }
            fraction=near;return true;
        }
        public static int Frame(float clock,float landingRemaining=0)
        {
            if(landingRemaining>0)return landingRemaining>LandingRecovery*.5f?4:5;
            return clock<Contact*.5f?0:clock<Contact?1:clock<ActiveEnd?2:3;
        }
    }

    public sealed partial class SFActor
    {
        public bool AerialUsed {get;private set;}
        public float AerialLandingRemaining {get;private set;}
        public readonly HashSet<SFActor> AerialHits=new HashSet<SFActor>();
        Vector3 aerialPrevious;
        float aerialDrive;
        bool aerialAwaitingLanding;
        public bool FlyingKick=>hero&&AerialAttack&&action==SFAction.Kick;
        public Vector3 CombatPosition=>new Vector3(X,lane,Height);

        void StartAerialKick()
        {
            AerialUsed=aerialAwaitingLanding=true;AerialHits.Clear();aerialPrevious=CombatPosition;
            aerialDrive=facing*Mathf.Clamp(Mathf.Abs(motor.Body.linearVelocity.x)/5.2f,.85f,1.25f);
            attackAt=SFAerialKick.Contact;attackDuration=SFAerialKick.Duration;
            ResetCombo();
        }
        void ResetAerialKick()
        {
            AerialUsed=aerialAwaitingLanding=AerialAttack=false;AerialLandingRemaining=0;aerialDrive=0;AerialHits.Clear();
        }
        void TickAerialLanding(float dt)
        {
            AerialLandingRemaining=Mathf.Max(0,AerialLandingRemaining-dt);
            if(!motor.IsGrounded)return;
            AerialUsed=false;
            if(!aerialAwaitingLanding)return;
            aerialAwaitingLanding=false;AerialLandingRemaining=SFAerialKick.LandingRecovery;
            cooldown=Mathf.Max(cooldown,AerialLandingRemaining);
            if(FlyingKick){actionClock=SFAerialKick.Duration-AerialLandingRemaining;attackDelivered=true;}
        }
        void TickAerialContact(float dt)
        {
            var now=CombatPosition;float before=actionClock-dt;
            if(!motor.IsGrounded&&AerialLandingRemaining<=0&&actionClock>=SFAerialKick.Contact&&before<SFAerialKick.ActiveEnd)
            {
                float first=Mathf.Clamp01((SFAerialKick.Contact-before)/Mathf.Max(.000001f,dt));
                float last=Mathf.Clamp01((SFAerialKick.ActiveEnd-before)/Mathf.Max(.000001f,dt));
                game.ResolveAerialKick(this,Vector3.Lerp(aerialPrevious,now,first),Vector3.Lerp(aerialPrevious,now,last));
            }
            aerialPrevious=now;
        }
    }

    public sealed partial class SFGame
    {
        public void ResolveAerialKick(SFActor attacker,Vector3 from,Vector3 to)
        {
            if(!attacker.FlyingKick||!attacker.Alive)return;
            var contacts=new List<(SFActor actor,float fraction)>();
            foreach(var target in Actors)
            {
                if(!target.Alive||target.Friendly==attacker.Friendly||attacker.AerialHits.Contains(target))continue;
                if(target.HeldBy!=null&&target.HeldBy!=attacker&&target.HeldBy.Friendly==attacker.Friendly)continue;
                if(SFAerialKick.Sweep(from,to,attacker.facing,target.CombatPosition,out float fraction))contacts.Add((target,fraction));
            }
            foreach(var hit in contacts.OrderBy(h=>h.fraction))
            {
                if(!attacker.FlyingKick)break; // a parry/damage cancels the remaining sweep
                var target=hit.actor;var point=Vector3.Lerp(from,to,hit.fraction);
                if(Covers.Any(c=>c.Blocks(new Vector2(point.x,point.y),point.z+.55f,target.GroundPosition,target.Height+.8f)))continue;
                float damage=OutgoingDamage(attacker,26*1.3f*(attacker!=Player&&CourseMode?.65f:1),SFAction.Kick);
                if(!target.Damage(damage,attacker))continue;
                attacker.AerialHits.Add(target);
                if(target.Alive&&!target.boss)
                {
                    target.KnockDown();float next=HoldClampX(target,ClampCoverX(target,target.X+attacker.facing*.8f));
                    target.motor.Body.position=new Vector2(next,target.Height);target.transform.position=new Vector3(next,target.Height,0);
                }
                Bond=Mathf.Min(100,Bond+7);RegisterHit(attacker);
            }
        }
    }
}
