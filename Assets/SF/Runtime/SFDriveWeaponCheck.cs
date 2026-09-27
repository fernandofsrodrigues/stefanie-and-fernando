using System.Collections;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFDriveCheck
    {
        void CheckDriveReloadTiming()
        {
            for(int weapon=0;weapon<2;weapon++)
            {
                var m=new SFDriveModel(false);m.SetRole(SFDriveRole.Gunner);m.SetWeapon(weapon);m.Board();Tick(m,40,default);
                m.Magazine.Fire();m.Magazine.Fire();int loaded=m.Magazine.Loaded,total=m.Magazine.Total;
                Check(m.Reload()&&m.ReloadProgress==0,"road weapon "+weapon+" reload begins at zero visual progress");
                Tick(m,5,default);float p=m.ReloadProgress;
                Check(p>0&&p<.46f&&m.ReloadStage=="MAGAZINE"&&m.Magazine.Loaded==loaded,"road weapon "+weapon+" early magazine pose does not grant ammunition");
                m.Tick(.2f,default,true);Check(m.ReloadProgress==p,"road weapon "+weapon+" pause freezes visual reload progress");
                m.SetRole(SFDriveRole.Driver);m.Tick(.02f,default,false);m.SetRole(SFDriveRole.Gunner);
                Check(m.Reloading&&m.ReloadProgress>p,"road weapon "+weapon+" role transfer retains reload ownership");
                while(m.ReloadProgress<.68f)m.Tick(.02f,default,false);
                Check(m.ReloadStage=="SEATING"&&m.Magazine.Loaded==loaded,"road weapon "+weapon+" seating pose still waits for completion");
                m.SetWeapon(1-weapon);Check(!m.Reloading&&m.ReloadProgress==0&&m.Magazines[weapon].Loaded==loaded&&m.Magazines[weapon].Total==total,"road weapon "+weapon+" switch cancels visual and ammunition transaction together");
                m.SetWeapon(weapon);m.Reload();while(m.ReloadProgress<.9f)m.Tick(.02f,default,false);
                Check(m.ReloadStage=="READYING"&&m.Magazine.Loaded==loaded,"road weapon "+weapon+" return to aim does not complete early");
                Tick(m,10,default);Check(!m.Reloading&&m.Magazine.Loaded==m.Magazine.Capacity&&m.Magazine.Total==total,"road weapon "+weapon+" completion transfers reserve once and conserves rounds");
            }
            var ai=new SFDriveModel(false);ai.Board();Tick(ai,40,default);
            ai.Enemies.Add(new SFConvoyEnemy(0,0,0){X=0,Age=2,Health=100});ai.Tick(.2f,default,false);
            Check(ai.Fired>0&&ai.LastShotWeapon==0&&ai.Shots.TrueForAll(s=>s.Hostile||s.Weapon==0),"AI changing from rifle to close-range pistol records the actual shot weapon");
        }
        IEnumerator CheckDriveWeaponVisuals()
        {
            Check(g.StartDrive(),"reload presentation test enters a separate road session");
            g.Drive.SetRole(SFDriveRole.Gunner);g.Drive.Board();for(int i=0;i<40;i++)g.TickDrive(.05f,default);
            for(int weapon=0;weapon<2;weapon++)
            {
                g.Drive.SetWeapon(weapon);g.Drive.Magazine.Fire();g.Drive.Reload();
                var art=Resources.Load<SFArt>("SF/vehicle_pickup_reload_"+(weapon==0?"pistol":"rifle"));
                Check(art!=null&&art.frames.Length==2&&art.Frame(0)!=art.Frame(1),"road weapon "+weapon+" has two distinct imported reload frames");
                while(g.Drive.ReloadProgress<.25f)g.TickDrive(.02f,default);
                Check(g.DriveVisiblePose==art.Frame(0)&&g.DriveVisibleWheels==2&&!g.DriveMuzzleVisible,"road weapon "+weapon+" early reload shows magazine pose with two registered wheels and no flash");
                yield return View("reload-"+weapon+"-magazine");
                while(g.Drive.ReloadProgress<.67f)g.TickDrive(.02f,default);
                Check(g.DriveVisiblePose==art.Frame(1)&&g.DriveVisibleWheels==2,"road weapon "+weapon+" late reload shows seating pose with two registered wheels");
                yield return View("reload-"+weapon+"-seating");
                var sprite=g.DriveVisiblePose;float p=g.Drive.ReloadProgress;
                g.SetState(SFState.Paused);g.TickDrive(.2f,new SFCommand{shoot=true,reload=true});
                Check(g.Drive.ReloadProgress==p&&g.DriveVisiblePose==sprite&&!g.DriveMuzzleVisible,"road weapon "+weapon+" paused input cannot advance reload art or fire");
                g.SetState(SFState.Playing);while(g.Drive.Reloading)g.TickDrive(.02f,default);
                Check(g.DriveVisiblePose!=art.Frame(0)&&g.DriveVisiblePose!=art.Frame(1),"road weapon "+weapon+" completed reload leaves the reload pose");
            }
            while(g.Drive.Distance<60)g.TickDrive(.05f,default);
            for(int weapon=0;weapon<2;weapon++)
            {
                g.Drive.SetWeapon(weapon);for(int i=0;i<8;i++)g.TickDrive(.05f,default);
                var e=g.Drive.Enemies[0];e.Active=true;e.Resolved=false;e.X=0;e.Health=1;e.AttackClock=100;g.Drive.SelectTarget(0);
                int fired=g.Drive.Fired;g.TickDrive(.02f,new SFCommand{shoot=true});
                Check(g.Drive.Fired==fired+1&&g.Drive.LastShotWeapon==weapon&&g.DriveMuzzleVisible,"road weapon "+weapon+" actual shot displays its own muzzle effect");
                var shotPose=Resources.Load<SFArt>("SF/vehicle_pickup_guns").Frame(weapon);
                Check(e.Resolved&&g.DriveVisiblePose==shotPose,"road weapon "+weapon+" finishing shot keeps the aiming pose after target defeat");
                var point=g.DriveMuzzlePosition;
                Check(point.x>2.6f&&point.x<2.9f&&point.y>SFGame.DriveRoadY(g.Drive.Lane)+1.2f&&point.y<SFGame.DriveRoadY(g.Drive.Lane)+1.6f,"road weapon "+weapon+" flash is anchored at the rear passenger barrel");
                yield return View("shot-"+weapon);
                g.SetState(SFState.Paused);float flash=g.Drive.MuzzleTime;g.TickDrive(.2f,default);
                Check(g.Drive.MuzzleTime==flash&&g.DriveMuzzlePosition==point,"road weapon "+weapon+" pause freezes shot feedback");g.SetState(SFState.Playing);
                g.Drive.Reload();g.RenderDrive();Check(!g.DriveMuzzleVisible,"road weapon "+weapon+" reload cancels lingering muzzle flash");
                g.Drive.SetWeapon(1-weapon);g.RenderDrive();Check(!g.Drive.Reloading&&!g.DriveMuzzleVisible,"road weapon "+weapon+" weapon swap clears reload and firing presentation");
            }
            g.EndDrive();yield return null;
        }
    }
}
