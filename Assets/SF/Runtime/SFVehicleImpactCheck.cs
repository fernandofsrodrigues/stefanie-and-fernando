using System.Collections;
using UnityEngine;
namespace StefanieAndFernando
{
    public sealed partial class SFDriveCheck
    {
        IEnumerator CheckVehicleImpacts()
        {
            var model=MountedFixture();var e=model.Enemies[0];e.Health=160;Tick(model,20,default);
            Tick(model,1,new SFCommand{shoot=true});
            Check(e.Health==80&&!e.Resolved&&e.MountedHitTime>0,"first cannon hit visibly damages but does not stop a standard technical");
            Tick(model,5,default);Tick(model,1,new SFCommand{shoot=true});
            Check(e.Health==0&&e.Resolved&&model.Defeated==1&&!e.MissileDestroyed,"second cannon hit stops technical without falsely reporting a missile blast");
            Tick(model,20,new SFCommand{shoot=true});Check(model.Defeated==1&&model.MissileImpacts==0,"destroyed technical cannot award duplicate defeats");
            var lost=MountedFixture();lost.SetWeapon(1);Tick(lost,20,default);
            for(int n=0;n<40&&lost.Projectiles.Count==0;n++)Tick(lost,1,new SFCommand{shoot=true});
            Check(lost.Projectiles.Count==1,"lost-target fixture launches a live missile");
            lost.Enemies[0].Resolved=true;lost.Enemies.Add(new SFConvoyEnemy(1,0,0){Active=true,X=-1,Technical=true,Health=160,AttackClock=100});
            Tick(lost,20,default);Check(lost.Projectiles.Count==0&&lost.Defeated==0&&lost.MissileImpacts==0&&lost.Enemies[1].Health==160,"lost missile target cannot transfer damage or explosion to another vehicle");

            g.ReturnToLoadout(0);g.StartDrive(false,false,true);g.Drive.SetRole(SFDriveRole.Gunner);g.Drive.Board();
            for(int i=0;i<40;i++)g.TickDrive(.05f,default);
            g.Drive.Enemies.Clear();g.Drive.Enemies.Add(new SFConvoyEnemy(0,0,0){Technical=true,X=-2,Health=160,AttackClock=100});
            for(int i=0;i<20;i++)g.TickDrive(.05f,default);e=g.Drive.Enemies[0];
            int slots=g.VehicleEffectSlots;Check(slots==132&&g.RoadMedianPieces==36,"road allocates bounded reusable impact and median pieces at entry");
            Vector3 marker=g.RoadMedianPosition;float distance=g.Drive.Distance;g.TickDrive(.05f,default);
            Check(Mathf.Abs((marker.x-g.RoadMedianPosition.x)-(g.Drive.Distance-distance))<.001f,"median animation tracks traveled distance");
            g.TickDrive(.05f,new SFCommand{shoot=true});
            Check(g.VehicleScarsVisible(0)==2&&g.PursuitSmokeVisible(0)==3&&g.VehicleImpactVisible(0),"first cannon hit shows body scars engine smoke and localized spark core");
            yield return View("cannon-damaged");
            g.Drive.SetWeapon(1);g.TickDrive(.05f,default);
            for(int i=0;i<35&&g.Drive.Projectiles.Count==0;i++)g.TickDrive(.05f,new SFCommand{shoot=true});
            for(int i=0;i<20&&g.Drive.MissileImpacts==0;i++)g.TickDrive(.05f,default);
            Check(e.Resolved&&e.MissileDestroyed&&g.Drive.MissileImpacts==1&&g.VehicleBlastVisible(0)==6,"missile impact destroys target and starts six pooled blast plumes");
            Check(g.VehicleScarsVisible(0)==3&&!g.PursuitWarningVisible(0)&&!g.PursuitMuzzleVisible(0),"wreck clears hostile warning and muzzle but retains body damage");
            yield return View("missile-impact");
            float blast=e.BlastTime;Vector3 at=g.VehicleBlastPosition(0);marker=g.RoadMedianPosition;int impacts=g.Drive.MissileImpacts;
            g.SetState(SFState.Paused);g.TickDrive(.2f,default);
            Check(e.BlastTime==blast&&g.VehicleBlastPosition(0)==at&&g.RoadMedianPosition==marker&&g.Drive.MissileImpacts==impacts,"pause freezes blast median and impact event without retriggering");
            g.SetState(SFState.Playing);g.TickDrive(.2f,default);yield return View("missile-fireball");
            g.TickDrive(.2f,default);yield return View("missile-smoke");
            for(int i=0;i<30;i++)g.TickDrive(.05f,default);
            Check(g.VehicleBlastVisible(0)==0&&g.VehicleEffectSlots==slots,"blast expires without growing its pool or removing persistent damage");
            var threat=new SFConvoyEnemy(1,0,0){Active=true,Technical=true,X=0,Health=1000};g.Drive.Enemies.Add(threat);
            g.Drive.SetRole(SFDriveRole.Driver);foreach(var mag in g.Drive.Magazines)mag.Clear();
            for(int i=0;i<20&&g.Drive.Phase==SFDrivePhase.Driving;i++){threat.AttackClock=.001f;threat.Warning=false;g.TickDrive(.05f,default);}
            Check(g.Drive.Phase==SFDrivePhase.Recovery,"impact presentation can pass into road recovery");
            Check(g.Drive.Retry()&&!e.MissileDestroyed&&e.BlastTime==0&&e.MountedHitTime==0&&g.Drive.MissileImpacts==0,"checkpoint retry resets every damage transient and impact counter");
            g.RenderDrive();Check(g.VehicleBlastVisible(0)==0&&g.VehicleScarsVisible(0)==0,"retry does not display previous wreck effects");
            g.EndDrive();yield return null;Check(g.VehicleEffectSlots==0&&g.RoadMedianPieces==0,"road exit releases impact pools and moving median");
            g.ReturnToLoadout(4);Check(g.RouteName=="FORT LIBERTY","mission selector uses requested Fort Liberty name");
            g.ReturnToLoadout(0);
        }
    }
}
