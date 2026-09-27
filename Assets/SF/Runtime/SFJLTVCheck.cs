using System.Collections;
using UnityEngine;
namespace StefanieAndFernando
{
    public sealed partial class SFDriveCheck
    {
        SFDriveModel MountedFixture(bool ahead=false)
        {
            var m=new SFDriveModel(false,false,false,true);m.SetRole(SFDriveRole.Gunner);m.Board();Tick(m,40,default);
            m.Enemies.Add(new SFConvoyEnemy(0,0,ahead?1:0){Technical=true,FromAhead=ahead,X=ahead?7:-2,Health=1000,AttackClock=100});
            Tick(m,2,default);return m;
        }
        IEnumerator CheckJLTV()
        {
            foreach(bool ahead in new[]{false,true})
            {
                string label=ahead?"JLTV ahead: ":"JLTV behind: ";var m=MountedFixture(ahead);var e=m.Enemies[0];
                Check(m.JLTV&&!m.Ram&&!m.Motorcycle&&m.Magazines[0].Capacity==30&&m.Magazines[1].Capacity==1,label+"separate mounted weapon magazines");
                if(!ahead){m.Tick(.05f,new SFCommand{shoot=true},false);Check(m.Fired==0,label+"cannot fire through an unsettled turret traverse");}
                Tick(m,20,default);Check(m.TurretAligned(e),label+"turret settles toward selected vehicle");
                int rounds=m.Magazine.Total;Tick(m,12,new SFCommand{shoot=true});
                Check(m.Fired>=2&&e.Health==1000-80*m.Fired&&m.Magazine.Total==rounds-m.Fired,label+"held cannon uses ammo and damages only its target");
                Check(ahead?Mathf.Abs(m.TurretYaw)<1:Mathf.Abs(Mathf.DeltaAngle(m.TurretYaw,180))<1,label+"independent turret faces correct side");
                Check(Mathf.Abs(m.TurretPitch)<=25,label+"elevation remains bounded");
                float health=e.Health;m.SetWeapon(1);Tick(m,2,default);int fired=m.Fired;
                Tick(m,10,new SFCommand{shoot=true});Check(m.Fired==fired&&m.LockTime>0&&m.LockTime<1,label+"missile requires a held lock interval");
                float locked=m.LockTime;float yaw=m.TurretYaw;m.Tick(.25f,new SFCommand{shoot=true},true);
                Check(m.LockTime==locked&&m.TurretYaw==yaw,label+"pause freezes lock and traverse");
                Tick(m,1,default);Check(m.LockTime==0,label+"released fire cancels partial lock");
                for(int n=0;n<40&&m.Projectiles.Count==0;n++)Tick(m,1,new SFCommand{shoot=true});
                Check(m.Fired==fired+1&&m.Projectiles.Count==1&&e.Health==health&&m.Magazine.Loaded==0,label+"launch consumes one round before delayed damage");
                if(m.Projectiles.Count>0){float age=m.Projectiles[0].Age;m.Tick(.25f,default,true);Check(m.Projectiles[0].Age==age,label+"pause freezes airborne projectile");}
                m.SetWeapon(0);Tick(m,16,default);Check(m.Projectiles.Count==0&&e.Health==0&&e.Resolved&&m.MissileImpacts==1,label+"missile finishes high-health vehicle once after weapon switch");
                m.SetWeapon(1);int total=m.Magazine.Total;Check(m.Reload(),label+"empty launcher can rearm from finite reserve");
                Tick(m,15,default);Check(m.Magazine.Loaded==0&&m.Magazine.Total==total,label+"rearm transfers nothing early");
                m.SetWeapon(0);Check(m.Magazines[1].Loaded==0&&m.Magazines[1].Total==total&&!m.Reloading,label+"weapon switch cancels rearm without duplicating ammo");
                m.SetWeapon(1);m.Reload();Tick(m,60,default);Check(m.Magazine.Loaded==1&&m.Magazine.Total==total,label+"completed rearm conserves ammunition");
                m.Magazine.Clear();Tick(m,30,new SFCommand{shoot=true});Check(m.Fired==fired+1&&m.LockTime==0&&!m.Reload(),label+"empty reserves cannot lock fire or refill");
            }
            var selection=MountedFixture();selection.SetWeapon(1);Tick(selection,20,default);Tick(selection,10,new SFCommand{shoot=true});
            selection.Enemies.Add(new SFConvoyEnemy(1,0,1){Active=true,Technical=true,FromAhead=true,X=7,Health=1000,AttackClock=100});selection.NextTarget();Tick(selection,1,new SFCommand{shoot=true});
            Check(selection.Target==1&&selection.LockTime<.1f,"JLTV target change cannot reuse another target's lock");
            selection.SetRole(SFDriveRole.Driver);Check(selection.LockTime==0,"JLTV role transfer cancels partial lock");
            var ai=new SFDriveModel(true,false,false,true);ai.Board();Tick(ai,650,new SFCommand{punch=true});
            Check(ai.Fired>0&&ai.Defeated>0,"JLTV AI gunner chooses and uses mounted weapons");
            foreach(int route in new[]{0,1,2,3,4,5})
            {
                g.ReturnToLoadout(route);Check(g.StartDrive(false,false,true)&&g.ConvoyVehicle=="DESERT JLTV","JLTV selectable from route "+route);g.EndDrive();
            }
            g.ReturnToLoadout(0);g.StartDrive();g.ChangeReadyVehicle();g.ChangeReadyVehicle();
            Check(g.Drive.JLTV,"ready selector reaches JLTV after Ram");g.ChangeReadyVehicle();Check(g.Drive.Motorcycle,"ready selector continues from JLTV to both motorcycles");g.ChangeReadyVehicle();Check(!g.Drive.Motorcycle&&!g.Drive.Ram&&!g.Drive.JLTV,"ready selector wraps back to BYD");g.EndDrive();
            g.StartDrive(false,false,true);g.Drive.SetRole(SFDriveRole.Gunner);g.Drive.Board();for(int i=0;i<40;i++)g.TickDrive(.05f,default);
            g.Drive.Enemies.Clear();g.Drive.Enemies.Add(new SFConvoyEnemy(0,0,0){Technical=true,X=-3,Health=1000,AttackClock=100});g.Drive.Enemies.Add(new SFConvoyEnemy(1,0,1){Technical=true,FromAhead=true,X=7,Health=1000,AttackClock=100});
            for(int i=0;i<22;i++)g.TickDrive(.05f,default);g.Drive.SelectTarget(0);for(int i=0;i<20;i++)g.TickDrive(.05f,default);
            Check(g.JLTVTurretVisible&&g.JLTVTurretScaleX<0&&g.DriveVisibleWheels==2,"rendered JLTV has an independent rear-facing turret and two registered rims");
            Check(g.DriveVisiblePose.name.StartsWith("vehicle_jltv_body"),"JLTV keeps the same unmirrored chassis and crew while aiming behind");
            g.TickDrive(.05f,new SFCommand{shoot=true});yield return View("jltv-rear-cannon");
            Check(g.DriveMuzzleVisible&&g.DriveMuzzlePosition.x<g.JLTVMount.x,"rear cannon flash originates at traversed muzzle");
            g.Drive.SelectTarget(1);for(int i=0;i<22;i++)g.TickDrive(.05f,default);g.TickDrive(.05f,new SFCommand{shoot=true});yield return View("jltv-front-cannon");
            Check(g.JLTVTurretScaleX>0&&g.DriveMuzzlePosition.x>g.JLTVMount.x,"front cannon flash follows independent turret without reversing chassis");
            g.Drive.SetWeapon(1);g.TickDrive(.05f,default);for(int i=0;i<25&&g.Drive.Projectiles.Count==0;i++)g.TickDrive(.05f,new SFCommand{shoot=true});
            yield return View("jltv-missile");Check(g.Drive.Projectiles.Count==1,"rendered missile launch has a live delayed projectile");
            g.SetState(SFState.Paused);float clock=g.Drive.Clock;g.TickDrive(.2f,new SFCommand{shoot=true});Check(g.Drive.Clock==clock,"integrated pause freezes JLTV actions");
            g.EndDrive();Check(!g.DriveActive&&!g.JLTVTurretVisible,"leaving road cleans mounted visuals and action ownership");g.ReturnToLoadout(0);
            yield return CheckVehicleImpacts();
        }
    }
}
