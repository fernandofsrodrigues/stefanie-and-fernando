using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed class SFMountedProjectile
    {
        public Vector2 From,To;
        public int Target;
        public float Age,Duration=.65f;
    }
    public sealed partial class SFDriveModel
    {
        // Deliberately fictional arcade timing and damage, not weapon operating data.
        public readonly List<SFMountedProjectile> Projectiles=new List<SFMountedProjectile>();
        public float TurretYaw {get;private set;}
        public float TurretPitch {get;private set;}
        public float LockTime {get;private set;}
        public int MissileImpacts {get;private set;}
        public float LastMissileImpactX {get;private set;}
        public const float CannonDamage=80,BlastDuration=1.25f,MountedHitDuration=.24f;
        int lockTarget=-1;
        public bool TurretAligned(SFConvoyEnemy target)=>target!=null&&Mathf.Abs(Mathf.DeltaAngle(TurretYaw,target.X<X?180:0))<8;
        void TickTurret(float dt,SFConvoyEnemy target,SFCommand c)
        {
            float yaw=target!=null&&target.X<X?180:0;
            TurretYaw=Mathf.MoveTowardsAngle(TurretYaw,yaw,240*dt);
            float pitch=target==null?0:Mathf.Atan2((target.Lane-Lane)*1.05f-1.2f,Mathf.Max(1,Mathf.Abs(target.X-X)))*Mathf.Rad2Deg;
            TurretPitch=Mathf.MoveTowards(TurretPitch,Mathf.Clamp(pitch,-25,25),dt*60);
            if(target==null||target.Id!=lockTarget){LockTime=0;lockTarget=target==null?-1:target.Id;}
            bool locking=Weapon==1&&!Reloading&&Magazine.Loaded>0&&target!=null&&TurretAligned(target)&&Mathf.Abs(target.X-X)<=Range&&(Role==SFDriveRole.Driver||c.shoot&&!requireRelease);
            LockTime=locking?Mathf.Min(1,LockTime+dt):0;
        }
        void FireMounted(SFConvoyEnemy target)
        {
            if(!TurretAligned(target)||Weapon==1&&LockTime<1)return;
            if(!Magazine.Fire())return;
            cooldown=Weapon==0?.22f:.65f;
            Fired++;LastShotWeapon=Weapon;MuzzleTime=.07f;AimHoldTime=.24f;RecoilTime=RecoilDuration;
            if(Weapon==0)
            {
                MountedDamage(target,CannonDamage);
                Shots.Add(new SFDriveShot{From=new Vector2(X,Lane),To=new Vector2(target.X,target.Lane),Weapon=0});
            }
            else
            {
                Projectiles.Add(new SFMountedProjectile{From=new Vector2(X,Lane),To=new Vector2(target.X,target.Lane),Target=Enemies.IndexOf(target)});
                LockTime=0;requireRelease=true;
            }
        }
        void MountedDamage(SFConvoyEnemy target,float damage,bool missile=false)
        {
            if(!target.Active||target.Resolved)return;
            // A valid missile impact is a decisive vehicle finisher regardless of target health.
            target.Health=missile?0:Mathf.Max(0,target.Health-damage);target.Flash=.16f;target.MountedHitTime=MountedHitDuration;
            if(missile){target.MissileDestroyed=true;target.BlastTime=BlastDuration;MissileImpacts++;LastMissileImpactX=target.X;}
            if(target.Health==0){target.Resolved=true;target.Warning=false;Defeated++;}
        }
        void TickProjectiles(float dt)
        {
            for(int i=Projectiles.Count-1;i>=0;i--)
            {
                var p=Projectiles[i];p.Age+=dt;
                if(p.Target>=0&&p.Target<Enemies.Count)
                {
                    var target=Enemies[p.Target];
                    if(target.Active&&!target.Resolved)p.To=new Vector2(target.X,target.Lane);
                    if(p.Age>=p.Duration)MountedDamage(target,0,true);
                }
                if(p.Age>=p.Duration)Projectiles.RemoveAt(i);
            }
        }
    }
}
