using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace StefanieAndFernando
{
    public sealed partial class SFDriveCheck
    {
        IEnumerator CheckMotorcycles()
        {
            var throttle=new SFCommand{punch=true};var m=new SFDriveModel(motorcycle:true);
            Check(m.Motorcycle&&m.Enemies.Count==0,"scenic motorcycles do not inherit pickup pursuers");
            m.SetRole(SFDriveRole.Gunner);m.SetWeapon(0);
            Check(m.Role==SFDriveRole.Driver&&m.Weapon==1&&!m.Reload(),"scenic mode rejects gunner weapons and reload commands");
            Tick(m,40,throttle);Check(m.Distance==0,"motorcycle throttle cannot bypass mounting");
            m.Board();Tick(m,40,default);Tick(m,60,throttle);
            Check(m.Speed==SFDriveModel.MaxSpeed&&m.Distance>0,"motorcycle accelerates after mounting");
            Tick(m,20,new SFCommand{punch=true,crouch=true});Check(m.Speed==0,"motorcycle braking wins over throttle");
            float distance=m.Distance,time=m.Clock;m.Tick(.2f,throttle,true);
            Check(m.Distance==distance&&m.Clock==time,"motorcycle pause freezes speed and stage clock");
            Tick(m,90,new SFCommand{move=Vector2.up});Check(m.Lane==1,"motorcycle reaches upper lane");
            Tick(m,90,new SFCommand{move=Vector2.down});Check(m.Lane==-1,"motorcycle reaches lower lane");
            Tick(m,1100,new SFCommand{punch=true,swap=true,shoot=true,reload=true,cycleWeapon=true});
            Check(m.Phase==SFDrivePhase.Complete&&m.Speed==0,"motorcycle route parks then dismounts even with mixed action inputs");
            Check(m.Impacts==1&&m.Integrity==80,"only crossed roadwork lane damages the motorcycle once");
            Check(m.Fired==0&&m.HostileShots==0&&m.Magazines[0].Total==85&&m.Magazines[1].Total==120,"scenic ride cannot manufacture combat or consume ammunition");
            int selected=g.Selected;bool touch=g.TouchControls;
            int audioSources=g.GetComponents<AudioSource>().Length;
            for(int rider=0;rider<2;rider++)
            {
                g.ReturnToLoadout(0);g.Selected=rider;g.Player.health-=17;g.Elapsed=17.25f;g.Player.Teleport(new Vector2(5.5f,.2f));
                var originalPosition=g.Player.transform.position;float health=g.Player.health,elapsed=g.Elapsed;int ammo=g.Player.TotalAmmo,missions=g.CompletedMissions;
                Check(g.StartDrive(true)&&g.Drive.Motorcycle,"selected rider "+rider+" enters optional motorcycle ride");
                Check(!g.StartDrive(true)&&!g.StartDrive(),"motorcycle ownership prevents duplicate entry");
                Check(g.MotorcycleRider==(rider==0?"FERNANDO":"STEFANIE"),"motorcycle artwork follows selected hero");
                var art=Resources.Load<SFArt>("SF/vehicle_bike_"+(rider==0?"fernando":"stefanie"));
                Check(art!=null&&art.frames.Length==4&&art.frames.All(x=>x!=null),"four distinct motorcycle stages import for rider "+rider);
                Check(g.Audio.VehicleSources[0].clip.name=="motorcycle_idle"&&g.Audio.VehicleSources[0].clip.frequency==48000,"motorcycle uses licensed recorded mono engine");
                g.MotorcyclePose(out int a,out int b,out float blend);Check(a==0&&b==0,"rider waits beside stationary motorcycle");
                yield return View("bike-"+rider+"-ready");
                g.TickDrive(.05f,new SFCommand{interact=true});for(int n=0;n<18;n++)g.TickDrive(.05f,default);
                g.MotorcyclePose(out a,out b,out blend);Check(a==1,"mount stage includes visible leg over saddle");
                Check(g.Drive.Distance==0&&g.Drive.Speed==0,"mounting never moves the motorcycle");
                yield return View("bike-"+rider+"-mount");
                for(int n=0;n<30;n++)g.TickDrive(.05f,default);
                g.MotorcyclePose(out a,out b,out blend);Check(a==2,"stopped mounted motorcycle uses foot-down stage");
                yield return View("bike-"+rider+"-seated");
                for(int n=0;n<70;n++)g.TickDrive(.05f,throttle);
                g.MotorcyclePose(out a,out b,out blend);Check(a==3,"moving motorcycle uses both feet on pegs");
                Vector3 rear=g.MotorcycleAxleWorld(3,false),front=g.MotorcycleAxleWorld(3,true);
                Check(Mathf.Abs(Vector3.Distance(rear,front)-2.15f)<.001f&&Mathf.Abs(rear.y-front.y)<.001f,"authored axle calibration maintains wheelbase and level wheels");
                Check(Mathf.Abs((rear.x+front.x)*.5f-SFDriveModel.PlayerX)<.001f,"motorcycle remains registered at road anchor");
                Check(g.Audio.VehicleCueCount==0&&g.Audio.VehicleSources[0].volume>0,"motorcycle engine runs without pickup doors or ignition cues");
                yield return View("bike-"+rider+"-cruise");
                g.PauseForInterruption();distance=g.Drive.Distance;time=g.Drive.Clock;g.TickDrive(.2f,throttle);
                Check(g.Drive.Distance==distance&&g.Drive.Clock==time&&g.Audio.VehiclePaused,"focus interruption freezes motorcycle and engine");
                g.SetState(SFState.Playing);g.EnableTouch("1");g.RefreshTouchLayout(900,1600);
                Check(g.State==SFState.Paused&&g.LayoutBlocked,"portrait touch layout suspends motorcycle");
                g.RefreshTouchLayout(1600,900);g.EnableTouch("0");g.SetState(SFState.Playing);
                g.SwitchDriveRole();Check(g.Drive.Role==SFDriveRole.Driver,"motorcycle role action cannot transfer to invisible gunner");
                for(int n=0;n<1200&&g.Drive.Phase==SFDrivePhase.Driving;n++)g.TickDrive(.05f,throttle);
                while(g.Drive.Phase==SFDrivePhase.Stopping)g.TickDrive(.05f,default);
                Check(g.Drive.Phase==SFDrivePhase.Dismounting&&g.Drive.Speed==0,"motorcycle parks before beginning dismount");
                for(int n=0;n<20;n++)g.TickDrive(.05f,default);yield return View("bike-"+rider+"-dismount");
                for(int n=0;n<30;n++)g.TickDrive(.05f,default);
                g.MotorcyclePose(out a,out b,out blend);Check(g.Drive.Phase==SFDrivePhase.Complete&&a==0,"motorcycle ride ends standing beside bike");
                yield return View("bike-"+rider+"-complete");
                g.TickDrive(.05f,new SFCommand{interact=true});yield return null;
                Check(!g.DriveActive&&g.State==SFState.Menu&&GameObject.Find("Serra motorcycle ride")==null,"motorcycle exit destroys ride objects and restores menu");
                Check(!g.Audio.VehicleActive&&g.Audio.VehicleSources.All(s=>!s.isPlaying),"motorcycle exit silences dedicated audio sources");
                Check(g.Player.health==health&&g.Player.TotalAmmo==ammo&&g.Player.transform.position==originalPosition&&g.Elapsed==elapsed&&g.CompletedMissions==missions,"motorcycle preserves rider foot health, ammo, position, elapsed time and mission progress");
            }
            g.Selected=selected;g.EnableTouch(touch?"1":"0");
            Check(g.GetComponents<AudioSource>().Length==audioSources,"motorcycles reuse fixed audio pool across reentry");
            Check(Resources.Load<TextAsset>("AudioCredits").text.Contains("119455")&&Resources.Load<TextAsset>("AudioCredits").text.Contains("CC0 1.0"),"motorcycle recorded audio has in-game provenance");
            Check(g.StartDrive()&&!g.Drive.Motorcycle&&g.Audio.VehicleSources[0].clip.name=="vehicle_engine","returning to convoy restores pickup combat and engine clip");g.EndDrive();
        }
    }
}
