using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StefanieAndFernando
{
    [Serializable] public sealed class SFLoadout
    {
        public int outfit,gear,sidearm;
        public bool helmet=true,startRifle;
    }
    public sealed partial class SFGame
    {
        public readonly SFLoadout[] Loadouts={new SFLoadout(),new SFLoadout()};
        public bool Heroic=true;
        public int CompletedMissions,Chain,BestChain;
        public string PartnerIntent="WITH YOU";
        public float Zoom=5.625f;
        float chainTime,armedThreatTime;
        public int HostilesRemaining=>Actors.Count(a=>!a.Friendly&&a.Alive);
        public string Objective=>HostilesRemaining>0?"CLEAR THE ROUTE  /  "+HostilesRemaining+" HOSTILES":"REACH THE EXIT TOGETHER  >";
        public SFLoadout Loadout(SFActor actor)=>Loadouts[actor.identity=="fernando"?0:1];
        public void ApplyLoadouts()
        {
            foreach(var a in new[]{Fernando,Stefanie})
            {
                var kit=Loadout(a);a.FieldUniform=kit.outfit==1;a.Helmet=kit.helmet;
                a.maxHealth=kit.gear==1?150:120;a.health=a.maxHealth;
                a.ResetWeapons();a.Armed=kit.startRifle;a.Rifle=kit.startRifle;a.ReleaseGrip();a.action=SFAction.Idle;a.cooldown=0;a.Render(0);
            }
            // Each hero carries independent magazines and reserve ammunition.
        }
        public void AdjustZoom(float amount)
        {
            Zoom=Mathf.Clamp(Zoom-amount*.38f,3.9f,5.625f);
        }
        public void SetDisplay(int height)
        {
            if(Application.platform==RuntimePlatform.WebGLPlayer){Screen.fullScreen=!Screen.fullScreen;return;}
            Screen.SetResolution(height*16/9,height,FullScreenMode.FullScreenWindow);
        }
        public void ReturnToLoadout(int route)
        {
            if(DriveActive)EndDrive();
            if(CourseMode){Restart();return;}
            foreach(var a in Actors.ToArray())
            {
                a.ReleaseGrip();
                if(a.ally){Actors.Remove(a);Destroy(a.shadow.gameObject);Destroy(a.gameObject);}
            }
            if(supportVehicle!=null)Destroy(supportVehicle.gameObject);
            BackupRemaining=BackupArrival=BackupCooldown=0;BackupCalls=0;
            foreach(var e in effects)if(e.visual!=null)Destroy(e.visual.gameObject);effects.Clear();
            foreach(var trail in shotTrails){trail.active=false;trail.core.enabled=trail.glow.enabled=false;}
            SetState(SFState.Menu);SelectRoute(route);
            foreach(var a in new[]{Fernando,Stefanie}){a.Revive();a.health=a.maxHealth;}
            Player.Teleport(new Vector2(3,.1f));Partner.Teleport(new Vector2(1.5f,.1f));
            Elapsed=0;KOs=Assists=Chain=BestChain=DataCount=0;Bond=70;AssistCooldown=0;
            NoticeTime=SpeechTime=0;armedThreatTime=0;cameraX=10;Zoom=5.625f;Checkpoint=3;
        }
        void TickParty(float dt,SFCommand command)
        {
            if(CourseMode)Zoom=5.625f;else AdjustZoom(command.zoom); // Course framing is fixed so feet and hazard strips stay above the HUD bar.
            Camera.orthographicSize=Mathf.Lerp(Camera.orthographicSize,Zoom,1-Mathf.Exp(-dt*9));
            chainTime-=dt;if(chainTime<=0)Chain=0;
            if(!CourseMode&&HostilesRemaining==0&&Player.X>=126&&Partner.Alive&&Player.Alive&&Mathf.Abs(Player.X-Partner.X)<6)
            {
                CompletedMissions|=1<<RouteIndex;
                if(!SmokeMode){PlayerPrefs.SetInt("SF.CompletedMissions",CompletedMissions);PlayerPrefs.Save();}
                SetState(SFState.Won);
            }
        }
        public float IncomingDamage(SFActor actor,float amount)
        {
            if(!actor.hero||CourseMode)return amount;
            return amount*(Heroic?.6f:1)*(Loadout(actor).gear==1?.82f:1);
        }
        public float OutgoingDamage(SFActor actor,float amount,SFAction action)
        {
            if(!actor.hero||CourseMode)return amount;
            if(action==SFAction.Shoot&&!actor.ShotRifle&&Loadout(actor).sidearm==1)amount*=1.3f;
            if(action!=SFAction.Shoot)amount*=Heroic?1.35f:1.1f;
            if(actor.CounterTime>0&&action!=SFAction.Shoot){amount*=1.6f;actor.CounterTime=0;Notify("COUNTER");}
            return amount;
        }
        public void RegisterHit(SFActor actor)
        {
            if(!actor.hero)return;Chain++;BestChain=Mathf.Max(BestChain,Chain);chainTime=3;
        }
        public SFCommand TacticalPartnerCommand()
        {
            var buddy=Partner;var c=new SFCommand{run=true};
            if(!buddy.Alive||buddy.Restrained)return c;
            if(buddy.GripTarget!=null){c.kick=buddy.ClinchHits>0;c.punch=!c.kick;PartnerIntent="CLINCH";return c;}
            var threats=Actors.Where(a=>!a.Friendly&&a.Alive&&a.HeldBy==null&&Mathf.Abs(a.X-Player.X)<10&&Mathf.Abs(a.Height-buddy.Height)<1.3f).ToArray();
            // Only a live hostile with a visible firing line can trigger the armed priority.
            var gunner=threats.Where(a=>a.ranged&&!a.Restrained&&ShotCover(buddy,a)==null)
                .OrderByDescending(a=>a.action==SFAction.Shoot).ThenBy(a=>Vector2.Distance(a.GroundPosition,buddy.GroundPosition)).FirstOrDefault();
            var enemy=gunner??threats.OrderBy(a=>Vector2.Distance(a.GroundPosition,buddy.GroundPosition)).FirstOrDefault();
            if(gunner!=null)armedThreatTime=Elapsed+1.1f;
            bool wantsGun=buddy.TotalAmmo>0&&(gunner!=null||Elapsed<armedThreatTime);
            if(!buddy.Busy&&buddy.action!=SFAction.Hurt)
            {if(buddy.Armed!=wantsGun)c.toggleArmed=true;}
            if(wantsGun&&buddy.Magazine.Total==0)c.weapon=buddy.Rifle?1:2;
            if(wantsGun&&buddy.Armed&&buddy.Magazine.Loaded==0&&buddy.Magazine.CanReload)c.reload=true;
            // Keep the chosen weapon unless it is completely empty.
            float danger=enemy==null?100:Vector2.Distance(enemy.GroundPosition,buddy.GroundPosition);
            bool needsHealth=buddy.health<buddy.maxHealth*.72f||Player.health<Player.maxHealth*.5f;
            var supply=Pickups.Where(p=>!p.taken&&Mathf.Abs(p.x-buddy.X)<6&&Mathf.Abs(p.x-Player.X)<9&&Mathf.Abs(p.height-.8f-buddy.Height)<.8f&&
                ((needsHealth&&(p.kind=="medical"||p.kind=="food"))||(buddy.Magazine.Total<18&&p.kind=="ammo")||(Bond<45&&p.kind=="drink")))
                .OrderBy(p=>Mathf.Abs(p.x-buddy.X)).FirstOrDefault();
            if(supply!=null&&gunner==null&&(danger>3||buddy.health<buddy.maxHealth*.25f))
            {
                PartnerIntent="RECOVERING SUPPLIES";c.move=Navigate(buddy,new Vector2(supply.x,supply.lane)).move;
                TryCollect(supply,buddy);return c;
            }
            var follow=FormationGoal(1.7f,Player.lane+.18f);
            if(enemy==null){PartnerIntent="WITH YOU";c.move=Navigate(buddy,follow).move;return c;}
            float dx=enemy.X-buddy.X;int face=dx>=0?1:-1;
            bool lined=Mathf.Abs(enemy.lane-buddy.lane)<.45f;
            if(!buddy.Busy)buddy.facing=face;
            float range=wantsGun?4.8f:1.1f;
            var goal=new Vector2(enemy.X-face*range,enemy.lane);
            c.move=Navigate(buddy,goal).move;
            if(wantsGun)
            {
                PartnerIntent="COVERING ARMED THREAT";c.aim=true;c.aimFacing=face;
                c.shoot=buddy.Armed&&Mathf.Abs(dx)<10&&lined&&ShotCover(buddy,enemy)==null;
                if(c.shoot){c.move=Vector2.zero;if(buddy.Rifle&&buddy.PrecisionRifle)c.shoot=!buddy.Busy&&buddy.cooldown<=0;}
            }
            else
            {
                PartnerIntent="CLOSE COMBAT";
                bool close=Mathf.Abs(dx)<1.6f&&lined&&ClearAttack(buddy,enemy,false);
                c.guard=close&&enemy.Busy&&enemy.action!=SFAction.Shoot&&enemy.actionClock>enemy.attackAt-.2f;
                c.grapple=close&&!enemy.boss&&enemy.health>32&&buddy.cooldown<=0&&!buddy.Busy;
                c.punch=close&&!c.grapple&&!c.guard;
            }
            return c;
        }
        public bool ThrowEnemy(SFActor actor)
        {
            var target=actor.GripTarget;
            if(target==null||!target.Alive||actor.Busy||!ClearAttack(actor,target,false))return false;
            actor.ReleaseGrip(true);target.invulnerable=0;
            target.Damage(OutgoingDamage(actor,24,SFAction.Sweep),actor,true);
            target.ThrowOwner=actor;target.ThrowSpeed=actor.facing*9;target.ThrowClock=.62f;target.ThrowHits.Clear();
            if(target.Alive)target.KnockDown();
            actor.cooldown=.48f;RegisterHit(actor);Audio.PlayAt("bodyheavy",actor.X,.64f);Notify("THROW");return true;
        }
        public void TickThrown(SFActor target,float dt)
        {
            if(target.ThrowClock<=0)return;
            target.ThrowClock-=dt;
            float next=target.X+target.ThrowSpeed*dt,allowed=HoldClampX(target,ClampCoverX(target,next));
            if(Mathf.Abs(next-allowed)>.015f)target.ThrowClock=0;
            target.motor.Body.position=new Vector2(allowed,target.Height);
            target.transform.position=new Vector3(allowed,target.Height,0);
            target.ThrowSpeed=Mathf.MoveTowards(target.ThrowSpeed,0,dt*8);
            foreach(var other in Actors.Where(a=>a!=target&&a.Alive&&a.Friendly!=target.ThrowOwner.Friendly&&Mathf.Abs(a.X-target.X)<.65f&&Mathf.Abs(a.lane-target.lane)<.5f&&Mathf.Abs(a.Height-target.Height)<1&&ClearAttack(target,a,false)).ToArray())
            {
                if(!target.ThrowHits.Add(other))continue;
                if(other.Damage(22,target.ThrowOwner,true)){other.KnockDown();RegisterHit(target.ThrowOwner);}
            }
        }
    }
}
