using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    public enum SFDriveRole { Driver, Gunner }
    public sealed class SFConvoyEnemy
    {
        public readonly int Id; public readonly float Spawn; public readonly int Lane;
        public float X=-17,Health=100,Age,AttackClock=3.4f,LockedLane,LockedX=4,Flash;
        public bool Active,Resolved,Warning; public int Shots;
        public bool Technical,FromAhead;
        public float GroundSpeed,WheelAngle,RetreatTime,MuzzleTime;
        public float MountedHitTime,BlastTime;
        public bool MissileDestroyed;
        public float TireRadius=>Id%2==0?.31f:.34f;
        public void Roll(float distance){WheelAngle=Mathf.Repeat(WheelAngle-distance/TireRadius*Mathf.Rad2Deg,360);}
        public SFConvoyEnemy(int id,float spawn,int lane){Id=id;Spawn=spawn;Lane=lane;}
        public string Name=>Technical?(FromAhead?"ROADBLOCK TECHNICAL":"FLANKER TECHNICAL"):Id%2==0?"INTERCEPTOR":"PURSUIT VAN";
    }
    public sealed class SFDriveShot
    {
        public Vector2 From,To;public float Life=.13f;public bool Hostile;public int Weapon=-1,SourceEnemy=-1;
    }
    // Deterministic, isolated arcade convoy simulation. It never changes foot-combat inventories.
    public sealed partial class SFDriveModel
    {
        public const float Length=480,MaxSpeed=11,HalfLength=2.45f,PlayerX=4;
        public static readonly float[] BarrierDistances={130,340};
        public static readonly float[] BarrierLanes={1,-1};
        public readonly HashSet<int> Contacted=new HashSet<int>();
        public readonly List<SFConvoyEnemy> Enemies=new List<SFConvoyEnemy>();
        public readonly List<SFDriveShot> Shots=new List<SFDriveShot>();
        public readonly SFMagazine[] Magazines={new SFMagazine(),new SFMagazine()};
        public SFDrivePhase Phase {get;private set;}=SFDrivePhase.Ready;
        public SFDriveRole Role {get;private set;}=SFDriveRole.Driver;
        public float X {get;private set;}=PlayerX;
        public float MinX {get;private set;}=-4;
        public float MaxX {get;private set;}=6.8f;
        public float BuddyX=>X-3.55f;
        public float BuddyLane {get;private set;}
        public bool Braking {get;private set;}
        public float PositionSpeed {get;private set;}
        public void SetViewport(float halfWidth){MinX=Mathf.Max(-4,-halfWidth+(Motorcycle?4.9f:2.8f));MaxX=Mathf.Max(MinX,Mathf.Min(6.8f,halfWidth-(Motorcycle?1.7f:2.8f)));X=Mathf.Clamp(X,MinX,MaxX);}
        public float Distance {get;private set;} public float Lane {get;private set;}
        public int TargetLane {get;private set;}
        public float Speed {get;private set;} public float Clock {get;private set;}
        public float Checkpoint {get;private set;} public float WheelAngle {get;private set;}
        public float ImpactTime {get;private set;} public float ReloadTime {get;private set;}
        public float MuzzleTime {get;private set;} public float AimHoldTime {get;private set;}
        public float RecoilTime {get;private set;}
        public float RecoilDuration=>LastShotWeapon==0?.22f:.14f;
        public float RecoilAmount=>Mathf.SmoothStep(0,1,Mathf.Clamp01(RecoilTime/RecoilDuration));
        public int LastShotWeapon {get;private set;}=-1;
        public int Integrity {get;private set;}=100;
        public int Impacts {get;private set;} public int Fired {get;private set;}
        public int HostileShots {get;private set;} public int Defeated {get;private set;}
        public int Escaped {get;private set;} public int Weapon {get;private set;}=1;
        public int Target {get;private set;}=-1;
        public bool Reloading=>ReloadTime>0;
        public float ReloadDuration=>JLTV?(Weapon==0?2.4f:2.8f):Weapon==0?1.35f:2.15f;
        public float ReloadProgress=>Reloading?Mathf.Clamp01(1-ReloadTime/ReloadDuration):0;
        public string ReloadStage=>JLTV?"REARMING":ReloadProgress<.46f?"MAGAZINE":ReloadProgress<.85f?"SEATING":"READYING";
        public SFMagazine Magazine=>Magazines[Weapon];
        public string WeaponName=>JLTV?(Weapon==0?"30 mm CANNON":"GUIDED MISSILE"):Weapon==0?"PISTOL 9mm":Ram?"HK MR762A1":"FN SCAR-H Mk 17";
        public float SceneryBlend=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(280,400,Distance));
        public float Range=>JLTV?18:Weapon==0?9:14;
        float cooldown,laneRepeat,aiClock;bool triggerHeld,requireRelease;int steerHeld;
        public readonly bool Motorcycle,Ram,JLTV;
        float CollisionHalfLength=>Motorcycle?1.45f:HalfLength;
        public SFDriveModel(bool encounters=true,bool motorcycle=false,bool ram=false,bool jltv=false)
        {
            Motorcycle=motorcycle;JLTV=jltv&&!motorcycle;Ram=ram&&!motorcycle&&!JLTV;Weapon=JLTV?0:1;ResetAmmo();
            if(encounters&&!motorcycle)for(int i=0;i<6;i++)Enemies.Add(new SFConvoyEnemy(i,new[]{42,78,166,242,285,375}[i],new[]{0,1,-1,1,0,-1}[i]){Technical=JLTV,FromAhead=JLTV&&i%2==1,X=JLTV&&i%2==1?21:-17,Health=JLTV?160:100});
        }
        void ResetAmmo(){Magazines[0].Reset(JLTV?30:17,JLTV?90:68);Magazines[1].Reset(JLTV?1:20,JLTV?5:100);}
        public bool Board(){if(Phase!=SFDrivePhase.Ready)return false;Phase=SFDrivePhase.Boarding;Clock=0;return true;}
        public void SetRole(SFDriveRole role){if(Motorcycle||Role==role)return;Role=role;LockTime=0;lockTarget=-1;requireRelease=true;triggerHeld=true;steerHeld=0;laneRepeat=0;aiClock=0;}
        public void SetWeapon(int weapon)
        {
            if(Motorcycle)return;
            weapon=Mathf.Clamp(weapon,0,1);if(Weapon==weapon)return;
            Weapon=weapon;LockTime=0;lockTarget=-1;ReloadTime=MuzzleTime=AimHoldTime=RecoilTime=0;cooldown=Mathf.Max(cooldown,.15f);requireRelease=true;
        }
        public bool Reload()
        {
            if(Motorcycle||Phase!=SFDrivePhase.Driving||Reloading||!Magazine.CanReload)return false;
            LockTime=0;lockTarget=-1;ReloadTime=ReloadDuration;MuzzleTime=AimHoldTime=RecoilTime=0;return true;
        }
        public void SelectTarget(int id){if(id>=0&&id<Enemies.Count&&Enemies[id].Active&&!Enemies[id].Resolved)Target=id;}
        public void NextTarget()
        {
            for(int n=1;n<=Enemies.Count;n++){int i=(Target+n+Enemies.Count)%Enemies.Count;if(Enemies[i].Active&&!Enemies[i].Resolved){Target=i;return;}}
            Target=-1;
        }
        public bool Retry()
        {
            if(Phase!=SFDrivePhase.Recovery)return false;
            X=Mathf.Clamp(PlayerX,MinX,MaxX);BuddyLane=0;Braking=false;PositionSpeed=0;Distance=Checkpoint;Speed=Lane=Clock=ImpactTime=ReloadTime=MuzzleTime=AimHoldTime=RecoilTime=cooldown=0;LastShotWeapon=-1;TargetLane=0;Integrity=100;Contacted.Clear();Shots.Clear();
            foreach(var e in Enemies){e.Active=false;e.Resolved=e.Spawn+70<Checkpoint;e.X=e.FromAhead?21:-17;e.Health=e.Technical?160:100;e.Age=e.GroundSpeed=e.WheelAngle=e.RetreatTime=e.MuzzleTime=e.Flash=e.MountedHitTime=e.BlastTime=0;e.MissileDestroyed=false;e.Warning=false;e.Shots=0;e.AttackClock=3.4f;}
            Projectiles.Clear();LockTime=0;lockTarget=-1;TurretYaw=TurretPitch=0;Defeated=Escaped=0;Fired=HostileShots=MissileImpacts=0;Target=-1;ResetAmmo();requireRelease=true;
            for(int i=0;i<BarrierDistances.Length;i++)if(BarrierDistances[i]<Checkpoint)Contacted.Add(i);
            Phase=SFDrivePhase.Driving;return true;
        }
        public void Tick(float dt,SFCommand c,bool paused)
        {
            if(paused||dt<=0)return;
            if(c.swap)SetRole(Role==SFDriveRole.Driver?SFDriveRole.Gunner:SFDriveRole.Driver);
            if(c.cycleWeapon||c.toggleArmed)SetWeapon((Weapon+1)%2);
            if(c.weapon>0)SetWeapon(c.weapon-1);
            if(c.interact&&Phase==SFDrivePhase.Driving)NextTarget();
            if(c.reload)Reload();
            bool pressed=c.shoot&&!triggerHeld;
            if(!c.shoot)requireRelease=false;
            triggerHeld=c.shoot;
            for(float left=Mathf.Min(dt,.25f);left>.000001f;)
            {float step=Mathf.Min(left,.02f);Step(step,c,pressed);pressed=false;left-=step;}
        }
        void Step(float dt,SFCommand c,bool pressed)
        {
            if(Phase==SFDrivePhase.Driving||Phase==SFDrivePhase.Stopping)
                foreach(var e in Enemies){e.MountedHitTime=Mathf.Max(0,e.MountedHitTime-dt);e.BlastTime=Mathf.Max(0,e.BlastTime-dt);}
            // Resolved vehicles coast in road space. They no longer slide backward at a fixed
            // speed or keep spinning while the road is paused/recovering.
            if(Phase==SFDrivePhase.Driving||Phase==SFDrivePhase.Stopping)
            foreach(var vehicle in Enemies)if(vehicle.Active&&vehicle.Resolved)
            {
                vehicle.Warning=false;vehicle.MuzzleTime=vehicle.Flash=0;vehicle.RetreatTime+=dt;
                vehicle.GroundSpeed=Mathf.MoveTowards(vehicle.GroundSpeed,0,dt*7);
                vehicle.X+=(vehicle.GroundSpeed-Speed)*dt;vehicle.Roll(vehicle.GroundSpeed*dt);
                if(vehicle.X<-20)vehicle.Active=false;
            }
            ImpactTime=Mathf.Max(0,ImpactTime-dt);MuzzleTime=Mathf.Max(0,MuzzleTime-dt);AimHoldTime=Mathf.Max(0,AimHoldTime-dt);RecoilTime=Mathf.Max(0,RecoilTime-dt);cooldown=Mathf.Max(0,cooldown-dt);Clock+=dt;
            for(int i=Shots.Count-1;i>=0;i--){Shots[i].Life-=dt;if(Shots[i].Life<=0)Shots.RemoveAt(i);}
            if(Phase==SFDrivePhase.Ready){if(c.interact||c.jump)Board();return;}
            if(Phase==SFDrivePhase.Recovery){if(c.interact||c.jump)Retry();return;}
            if(Phase==SFDrivePhase.Boarding){if(Clock>=1.8f){Phase=SFDrivePhase.Driving;Clock=0;}return;}
            if(Phase==SFDrivePhase.Dismounting){if(Clock>=1.8f){Phase=SFDrivePhase.Complete;Clock=0;}return;}
            if(Phase==SFDrivePhase.Complete)return;
            if(Phase==SFDrivePhase.Stopping)
            {
                float parkedX=X;X=Mathf.MoveTowards(X,Mathf.Clamp(PlayerX,MinX,MaxX),dt*2);WheelAngle-=(X-parkedX)*115;
                BuddyLane=Mathf.MoveTowards(BuddyLane,0,dt*1.4f);
                Speed=Mathf.MoveTowards(Speed,0,dt*8);Lane=Mathf.MoveTowards(Lane,0,dt*1.4f);WheelAngle-=Speed*dt*115;
                if(Speed<=.001f&&Mathf.Abs(Lane)<.001f&&Mathf.Abs(X-Mathf.Clamp(PlayerX,MinX,MaxX))<.001f){Phase=SFDrivePhase.Dismounting;Clock=0;}return;
            }
            TickProjectiles(dt);
            if(ReloadTime>0){ReloadTime=Mathf.Max(0,ReloadTime-dt);if(ReloadTime==0)Magazine.Reload();}
            float previousPosition=X,previousProgress=Distance+X-PlayerX;Braking=false;
            laneRepeat-=dt;int steer=c.move.y>.35f?1:c.move.y<-.35f?-1:0;
            if(Role==SFDriveRole.Driver)
            {
                if(steer!=0&&(steer!=steerHeld||laneRepeat<=0)){TargetLane=Mathf.Clamp(TargetLane+steer,-1,1);laneRepeat=.28f;}
                float throttle=c.punch?1:0;
                bool brake=c.guard||c.crouch;Braking=brake;
                X=Mathf.Clamp(X+Mathf.Clamp(c.move.x,-1,1)*dt*(Motorcycle?3.6f:3f),MinX,MaxX);
                Speed=Mathf.MoveTowards(Speed,brake?0:throttle*MaxSpeed,dt*(brake?15:throttle>0?4:2));
            }
            else
            {
                Speed=Mathf.MoveTowards(Speed,!JLTV&&Weapon==0?6.5f:MaxSpeed,dt*4);
                if(steer!=0&&steer!=steerHeld)NextTarget();
                aiClock-=dt;if(aiClock<=0){ChooseSafeLane();aiClock=.3f;}
            }
            PositionSpeed=(X-previousPosition)/dt;
            steerHeld=steer;Lane=Mathf.MoveTowards(Lane,TargetLane,dt*2.7f);
            if(Motorcycle)
            {
                float buddyGoal=TargetLane,progress=Distance+BuddyX-PlayerX;
                for(int i=0;i<BarrierDistances.Length;i++)if(BarrierDistances[i]>progress-3&&BarrierDistances[i]<progress+22&&Mathf.Abs(buddyGoal-BarrierLanes[i])<.5f)buddyGoal=0;
                BuddyLane=Mathf.MoveTowards(BuddyLane,buddyGoal,dt*2.7f);
            }
            float next=Mathf.Min(Length,Distance+Speed*dt);
            for(int i=0;i<BarrierDistances.Length;i++)
            {
                if(Contacted.Contains(i)||Mathf.Abs(Lane-BarrierLanes[i])>.38f||Mathf.Abs(Speed+PositionSpeed)<.3f)continue;
                float progress=next+X-PlayerX;
                if(Mathf.Max(progress,previousProgress)+CollisionHalfLength<BarrierDistances[i]-.45f||Mathf.Min(progress,previousProgress)-CollisionHalfLength>BarrierDistances[i]+.45f)continue;
                Contacted.Add(i);Damage(20);Impacts++;Speed*=.12f;
            }
            WheelAngle-=(next-Distance+X-previousPosition)*115;Distance=next;
            if(Distance>=240)Checkpoint=240;
            if(Phase!=SFDrivePhase.Driving)return;
            TickEnemies(dt);
            if(Phase!=SFDrivePhase.Driving)return;
            if(Target<0||!Enemies[Target].Active||Enemies[Target].Resolved)NextTarget();
            var target=Target>=0?Enemies[Target]:null;
            if(JLTV)TickTurret(dt,target,c);
            if(Role==SFDriveRole.Driver)
            {
                if(target!=null&&target.Age>1.4f)
                {
                    int desired=JLTV?(target.Health>100&&Mathf.Abs(X-target.X)>8&&Magazines[1].Total>0?1:0):X-target.X>8.6f?1:0;
                    if(!Reloading&&desired!=Weapon)SetWeapon(desired);
                    if(Magazine.Loaded==0)Reload();
                    else Fire(target,false);
                }
            }
            else if(!requireRelease&&c.shoot&&(JLTV?Weapon==0||LockTime>=1:Weapon==1&&!Ram||pressed))Fire(target,c.aim);
            if(Distance>=Length)
            {
                foreach(var e in Enemies)if(e.Active&&!e.Resolved){e.Resolved=true;Escaped++;}
                Projectiles.Clear();LockTime=0;Phase=SFDrivePhase.Stopping;Clock=0;ReloadTime=MuzzleTime=AimHoldTime=RecoilTime=0;
            }
        }
        void ChooseSafeLane()
        {
            int best=TargetLane;float bestScore=10000;
            for(int lane=-1;lane<=1;lane++)
            {
                float score=Mathf.Abs(lane-TargetLane)*.22f+Mathf.Abs(lane)*.04f;
                for(int i=0;i<BarrierDistances.Length;i++)if(!Contacted.Contains(i)&&BarrierDistances[i]>Distance-HalfLength&&BarrierDistances[i]<Distance+22&&Mathf.Abs(lane-BarrierLanes[i])<.4f)score+=10;
                foreach(var e in Enemies)if(e.Active&&!e.Resolved&&e.Warning&&Mathf.Abs(lane-e.LockedLane)<.5f)score+=5;
                if(score<bestScore){best=lane;bestScore=score;}
            }
            TargetLane=best;
        }
        void TickEnemies(float dt)
        {
            foreach(var e in Enemies)
            {
                e.Flash=Mathf.Max(0,e.Flash-dt);
                e.MuzzleTime=Mathf.Max(0,e.MuzzleTime-dt);
                if(e.Resolved)continue;
                if(Distance<e.Spawn)continue;
                e.Active=true;e.Age+=dt;
                float previousX=e.X;
                e.X=Mathf.MoveTowards(e.X,e.FromAhead?Mathf.Min(X+7,MaxX+.45f):X-(Speed>7?10:6)-(e.Id%2)*2,dt*5);
                e.GroundSpeed=Mathf.Max(0,Speed+(e.X-previousX)/dt);e.Roll(e.GroundSpeed*dt);
                if(Distance>e.Spawn+100||e.Age>30){e.Resolved=true;Escaped++;continue;}
                e.AttackClock-=dt;
                if(!e.Warning&&e.AttackClock<=1.15f){e.Warning=true;e.LockedLane=Lane;e.LockedX=X;}
                if(e.AttackClock<=0)
                {
                    HostileShots++;e.Shots++;e.MuzzleTime=.09f;
                    Shots.Add(new SFDriveShot{From=new Vector2(e.X+1,e.Lane),To=new Vector2(e.LockedX,e.LockedLane),Hostile=true,SourceEnemy=Enemies.IndexOf(e)});
                    if(Mathf.Abs(Lane-e.LockedLane)<.42f&&Mathf.Abs(X-e.LockedX)<2.15f)Damage(8);
                    e.Warning=false;e.AttackClock=e.Id%2==0?3.8f:4.6f;
                }
            }
        }
        void Damage(int amount){Integrity=Mathf.Max(0,Integrity-amount);ImpactTime=.5f;if(Integrity==0){Phase=SFDrivePhase.Recovery;Speed=0;ReloadTime=MuzzleTime=AimHoldTime=RecoilTime=0;Shots.Clear();Projectiles.Clear();LockTime=0;}}
        void Fire(SFConvoyEnemy e,bool aimed)
        {
            if(cooldown>0||Reloading||e==null||e.Resolved||!e.Active||Mathf.Abs(X-e.X)>Range)return;
            if(JLTV){FireMounted(e);return;}
            if(!Magazine.Fire())return;
            cooldown=Weapon==0?.32f:Ram?.32f:.14f;
            Fired++;LastShotWeapon=Weapon;MuzzleTime=.07f;AimHoldTime=.24f;RecoilTime=RecoilDuration;e.Flash=.11f;
            e.Health=Mathf.Max(0,e.Health-(Weapon==0?18:Weapon==1?(Ram?32:26):38)*(aimed?1.08f:1));
            Shots.Add(new SFDriveShot{From=new Vector2(X-1,Lane),To=new Vector2(e.X,e.Lane),Weapon=Weapon});
            if(e.Health<=0){e.Resolved=true;Defeated++;}
        }
    }
}
