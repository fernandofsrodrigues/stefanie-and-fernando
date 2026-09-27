using System.Collections;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFDriveCheck
    {
        SFDriveModel RecoilFixture(int weapon)
        {
            var m=new SFDriveModel(false);m.SetRole(SFDriveRole.Gunner);m.SetWeapon(weapon);m.Board();Tick(m,40,default);
            m.Enemies.Add(new SFConvoyEnemy(0,0,0){X=0,Health=10000,AttackClock=100});m.Tick(.02f,default,false);return m;
        }
        void CheckGunnerRecoilClock()
        {
            for(int weapon=0;weapon<2;weapon++)
            {
                var m=RecoilFixture(weapon);string label="recoil weapon "+weapon+" ";
                Check(m.RecoilTime==0&&m.RecoilAmount==0,label+"starts settled without a shot");
                m.Tick(.02f,new SFCommand{shoot=true},false);float impulse=m.RecoilTime;
                Check(m.Fired==1&&m.RecoilAmount==1&&impulse==(weapon==0?.22f:.14f),label+"real shot starts its own bounded recovery clock");
                m.Tick(.25f,new SFCommand{reload=true,cycleWeapon=true},true);
                Check(m.RecoilTime==impulse&&!m.Reloading&&m.Weapon==weapon,label+"pause freezes recoil and ignores queued actions");
                m.SetRole(SFDriveRole.Driver);m.SetRole(SFDriveRole.Gunner);
                Check(m.RecoilTime==impulse&&m.Fired==1,label+"role transfer preserves the ongoing shot without duplicating it");
                m.Tick(.04f,default,false);Check(m.RecoilTime<impulse&&m.RecoilAmount>0&&m.RecoilAmount<1,label+"returns smoothly toward aim on the road clock");
                Tick(m,6,default);Check(m.RecoilAmount==0,label+"settles fully instead of accumulating gunner drift");
                m.Enemies[0].X=0;m.Tick(.02f,new SFCommand{shoot=true},false);Check(m.RecoilTime>0&&m.Reload()&&m.RecoilTime==0,label+"reload takes immediate ownership from recoil");
                while(m.Reloading)m.Tick(.02f,default,false);
                m.Enemies[0].X=0;m.Tick(.02f,new SFCommand{shoot=true},false);m.SetWeapon(1-weapon);
                Check(m.RecoilTime==0&&m.RecoilAmount==0,label+"weapon swap cannot inherit the previous weapon impulse");
                m.SetWeapon(weapon);Tick(m,6,default);m.Magazine.Clear();m.Enemies[0].X=0;m.Tick(.02f,new SFCommand{shoot=true},false);
                Check(m.RecoilTime==0,label+"empty magazine cannot create cosmetic recoil");
                var a=RecoilFixture(weapon);var b=RecoilFixture(weapon);a.Tick(.02f,new SFCommand{shoot=true},false);b.Tick(.02f,new SFCommand{shoot=true},false);
                for(int i=0;i<6;i++)a.Tick(.01f,default,false);b.Tick(.06f,default,false);
                Check(Mathf.Abs(a.RecoilAmount-b.RecoilAmount)<.0001f,label+"recovery is independent of render tick partition");
            }
            var recovery=RecoilFixture(1);recovery.Tick(.02f,new SFCommand{shoot=true},false);
            for(int i=0;i<14;i++)recovery.Enemies.Add(new SFConvoyEnemy(i+1,0,0){X=0,AttackClock=.001f});
            recovery.Tick(.02f,default,false);
            Check(recovery.Phase==SFDrivePhase.Recovery&&recovery.RecoilTime==0,"vehicle failure clears gunner recoil before recovery presentation");
            Check(recovery.Retry()&&recovery.RecoilTime==0&&recovery.LastShotWeapon==-1,"checkpoint retry has no stale shot ownership");
            var finish=RecoilFixture(1);for(int i=0;i<1600&&finish.Phase==SFDrivePhase.Driving;i++)
            {finish.Enemies[0].X=0;finish.Enemies[0].Resolved=false;finish.Enemies[0].Age=0;finish.Enemies[0].Health=10000;finish.Tick(.05f,new SFCommand{shoot=true,reload=finish.Magazine.Loaded==0},false);}
            Check(finish.Phase==SFDrivePhase.Stopping&&finish.RecoilTime==0,"arrival cancels recoil before vehicle stopping and dismount");
        }
        IEnumerator CheckGunnerRecoilVisuals()
        {
            Check(g.StartDrive()&&g.DriveRecoilRigCount==2,"road allocates one reusable recoil mesh for each pickup firearm");
            g.Drive.SetRole(SFDriveRole.Gunner);g.Drive.Board();for(int i=0;i<40;i++)g.TickDrive(.05f,default);
            while(g.Drive.Distance<60)g.TickDrive(.05f,default);
            for(int weapon=0;weapon<2;weapon++)
            {
                string label="rendered recoil weapon "+weapon+" ";g.Drive.SetWeapon(weapon);for(int i=0;i<8;i++)g.TickDrive(.05f,default);
                var e=g.Drive.Enemies[0];e.Active=true;e.Resolved=false;e.X=0;e.Health=10000;e.AttackClock=100;g.Drive.SelectTarget(0);g.RenderDrive();
                Check(g.DriveRecoilVisibleCount==0,label+"rest uses the unchanged aim sprite");yield return View("recoil-"+weapon+"-rest");
                g.TickDrive(.02f,new SFCommand{shoot=true});
                Check(g.DriveRecoilVisibleCount==1&&g.DriveVisibleWheels==2,label+"shot uses one mesh and retains two registered wheels");
                // Sample actual uploaded mesh vertices, not just the analytical weighting function.
                Check(g.DriveRecoilMeshDisplacement(weapon,50*129+38).sqrMagnitude>.0004f,label+"passenger hand region moves in the rendered mesh");
                bool fixedTruck=true;foreach(int index in new[]{12*129+23,12*129+105,32*129+60,49*129+71,60*129+50,6*129+120})fixedTruck&=g.DriveRecoilMeshDisplacement(weapon,index)==Vector3.zero;
                Check(fixedTruck,label+"rim centers chassis driver hood and roof stay pinned");
                var muzzle=g.DriveMuzzlePosition;var bound=Resources.Load<SFArt>("SF/vehicle_pickup_guns").Frame(weapon).bounds;
                Vector2 p=new Vector2(weapon==0?.272f:.262f,weapon==0?.783f:.780f);Vector3 barrel=g.DriveRecoilPoint(weapon,p);
                Check(Mathf.Abs(muzzle.y-barrel.y)<.001f&&muzzle.x<barrel.x&&barrel.x-muzzle.x<.07f,label+"muzzle flash follows the displaced barrel");
                yield return View("recoil-"+weapon+"-peak");
                g.SetState(SFState.Paused);var hand=g.DriveRecoilMeshDisplacement(weapon,50*129+38);g.TickDrive(.2f,default);
                Check(g.DriveRecoilMeshDisplacement(weapon,50*129+38)==hand&&g.DriveMuzzlePosition==muzzle,label+"pause preserves exact mesh and muzzle alignment");g.SetState(SFState.Playing);
                g.TickDrive(.08f,default);Check(g.DriveRecoilMeshDisplacement(weapon,50*129+38).sqrMagnitude<hand.sqrMagnitude,label+"mesh recovers after the firing impulse");yield return View("recoil-"+weapon+"-return");
                for(int i=0;i<6;i++)g.TickDrive(.05f,default);
                Check(g.DriveRecoilVisibleCount==0&&!g.DriveMuzzleVisible,label+"settled aim releases transient rendering");
                e.X=0;g.TickDrive(.02f,new SFCommand{shoot=true});g.Drive.Reload();g.RenderDrive();
                Check(g.DriveRecoilVisibleCount==0&&!g.DriveMuzzleVisible&&g.Drive.Reloading,label+"reload hides recoil without doubling the vehicle");
                g.Drive.SetWeapon(1-weapon);g.RenderDrive();Check(g.DriveRecoilVisibleCount==0,label+"weapon switch leaves no ghost recoil mesh");
            }
            g.EndDrive();yield return null;
            Check(g.DriveRecoilRigCount==0&&GameObject.Find("Isolated gunner recoil 0")==null,"road exit releases recoil objects and mesh ownership");
        }
    }
}
