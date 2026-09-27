using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace StefanieAndFernando
{
    public sealed partial class SFDriveCheck
    {
        IEnumerator CheckRoadUpgrade()
        {
            var m=new SFDriveModel(false);m.Board();Tick(m,40,default);
            Tick(m,20,new SFCommand{move=Vector2.left});
            Check(m.X<4&&m.Speed==0&&m.Distance==0,"fore-aft positioning does not press accelerator or advance road progress");
            float x=m.X;Tick(m,60,new SFCommand{punch=true});
            Check(m.Speed==11&&m.X==x,"accelerator changes road speed without steering position");
            Tick(m,20,new SFCommand{move=Vector2.left,punch=true});
            Check(m.X<x&&m.Speed==11,"backward screen movement does not become brake");
            Tick(m,20,new SFCommand{move=Vector2.right,punch=true,crouch=true});
            Check(m.Speed==0&&m.Braking,"brake wins over accelerator while positioning remains available");
            Tick(m,200,new SFCommand{move=Vector2.right});Check(m.X==m.MaxX,"forward position has a safe viewport limit");
            Tick(m,200,new SFCommand{move=Vector2.left});Check(m.X==m.MinX,"rearward position has a safe viewport limit");
            float d=m.Distance;x=m.X;float angle=m.WheelAngle;
            m.Tick(.25f,new SFCommand{move=Vector2.right,punch=true},true);
            Check(m.X==x&&m.Distance==d&&m.WheelAngle==angle,"pause freezes independent position and wheel angle");
            m.SetViewport(6.875f);Check(m.X>=-4.075f&&m.X<=4.075f,"narrow landscape viewport keeps truck inside frame");
            var bike=new SFDriveModel(false,true);bike.SetViewport(6.875f);bike.Board();Tick(bike,40,default);Tick(bike,200,new SFCommand{move=Vector2.left});
            Check(bike.BuddyX-1.3f>=-6.875f&&bike.X<4,"both riders fit at narrow viewport rear boundary");
            var small=new SFDriveModel(false);var large=new SFDriveModel(false);small.Board();large.Board();Tick(small,40,default);Tick(large,40,default);
            var diagonal=new SFCommand{move=new Vector2(-.5f,1),punch=true};
            for(int n=0;n<200;n++)small.Tick(.01f,diagonal,false);for(int n=0;n<8;n++)large.Tick(.25f,diagonal,false);
            Check(Mathf.Abs(small.X-large.X)<.001f&&Mathf.Abs(small.Distance-large.Distance)<.1f,"position and pedals remain consistent across frame sizes");
            // Moving out of a locked firing zone, and returning before the next warning, is meaningful.
            var evade=new SFDriveModel(false);evade.Board();Tick(evade,40,default);foreach(var mag in evade.Magazines)mag.Clear();
            evade.Enemies.Add(new SFConvoyEnemy(0,0,0));Tick(evade,48,default);
            Check(evade.Enemies[0].Warning,"pursuer announces a locked firing zone");
            Tick(evade,22,new SFCommand{move=Vector2.left});
            Check(evade.HostileShots==1&&evade.Integrity==100,"fore-aft dodge escapes a previously locked firing zone");
            foreach(bool ram in new[]{false,true})
            {
                var gun=new SFDriveModel(false,false,ram);gun.SetRole(SFDriveRole.Gunner);gun.Board();Tick(gun,40,default);
                gun.Enemies.Add(new SFConvoyEnemy(0,0,0){X=0,Health=1000});Tick(gun,2,default);
                Tick(gun,15,new SFCommand{shoot=true});int fired=gun.Fired;
                Check(ram?fired==1:fired>=4,(ram?"Ram MR762A1":"BYD SCAR-H")+" retains distinct trigger behavior");
                Tick(gun,1,default);Tick(gun,1,new SFCommand{shoot=true});
                Check(!ram||gun.Fired==2,"MR762A1 fires again after release and a fresh press");
                Check(gun.Magazines[1].Capacity==20&&gun.Magazines[1].Total==120-gun.Fired,"convoy rifle magazine and reserve conserve every round");
            }
            g.ReturnToLoadout(0);Check(g.StartDrive(),"road choice starts with BYD");
            var key=InputSystem.AddDevice<Keyboard>();key.MakeCurrent();
            InputSystem.QueueStateEvent(key,new KeyboardState(Key.A,Key.W,Key.LeftShift));InputSystem.Update();var c=g.ReadDriveCommand();
            Check(c.move==new Vector2(-1,1)&&c.punch&&!c.crouch,"keyboard steering and Shift pedal work simultaneously");
            InputSystem.QueueStateEvent(key,new KeyboardState(Key.D,Key.LeftCtrl));InputSystem.Update();c=g.ReadDriveCommand();
            Check(c.move.x==1&&!c.punch&&c.crouch,"keyboard Ctrl brake remains independent of D positioning");
            InputSystem.RemoveDevice(key);
            var roadPad=InputSystem.AddDevice<Gamepad>();roadPad.MakeCurrent();
            InputSystem.QueueStateEvent(roadPad,new GamepadState().WithButton(GamepadButton.South));InputSystem.Update();g.Pad.Poll();
            InputSystem.QueueStateEvent(roadPad,new GamepadState());InputSystem.Update();g.Pad.Poll();
            InputSystem.QueueStateEvent(roadPad,new GamepadState{rightTrigger=1,leftStick=new Vector2(-.6f,.8f)});InputSystem.Update();g.Pad.Poll();c=g.ReadDriveCommand();
            Check(g.GamepadActive&&c.punch&&c.move.x<-.5f&&c.move.y>.7f,"controller position and lane remain independent of RT accelerator");
            InputSystem.QueueStateEvent(roadPad,new GamepadState{leftTrigger=1,leftStick=Vector2.right});InputSystem.Update();g.Pad.Poll();c=g.ReadDriveCommand();
            Check(c.crouch&&!c.punch&&c.move.x>.9f,"controller can brake while moving forward in road space");
            InputSystem.QueueStateEvent(roadPad,new GamepadState().WithButton(GamepadButton.South));InputSystem.Update();g.Pad.Poll();c=g.ReadDriveCommand();
            Check(g.DriveMenuActive&&!c.jump&&!c.interact,"controller menu selection cannot also board behind the focused button");
            InputSystem.RemoveDevice(roadPad);g.Pad.Poll();
            var roadTouch=InputSystem.AddDevice<Touchscreen>();roadTouch.MakeCurrent();g.EnableTouch("1");
            float roadWidth=Screen.width*900f/Screen.height;
            var stickAt=SFTouchInput.StickCenter+new Vector2(-54,-72);var gasAt=SFTouchInput.Control(0,roadWidth).center;
            Vector2 TouchPoint(Vector2 p)=>new Vector2(p.x,900-p.y)*Screen.height/900f;
            InputSystem.QueueStateEvent(roadTouch,new TouchState{touchId=701,phase=UnityEngine.InputSystem.TouchPhase.Began,position=TouchPoint(stickAt)});
            InputSystem.QueueStateEvent(roadTouch,new TouchState{touchId=702,phase=UnityEngine.InputSystem.TouchPhase.Began,position=TouchPoint(gasAt)});InputSystem.Update();c=g.ReadDriveCommand();
            Check(c.punch&&c.move.x<-.5f&&c.move.y>.7f,"two touch contacts can accelerate and position diagonally together");
            InputSystem.QueueStateEvent(roadTouch,new TouchState{touchId=703,phase=UnityEngine.InputSystem.TouchPhase.Began,position=TouchPoint(SFTouchInput.Control(2,roadWidth).center)});InputSystem.Update();c=g.ReadDriveCommand();
            Check(c.punch&&c.crouch&&c.move.x<-.5f,"touch brake remains available alongside held accelerator and steering");
            InputSystem.RemoveDevice(roadTouch);g.EnableTouch("0");
            g.ChangeReadyVehicle();Check(g.Drive.Ram&&g.ConvoyDriver=="FERNANDO"&&g.ConvoyGunner=="STEFANIE","Ram selection has matching crew roles");
            g.RenderDrive();yield return View("ram-ready");
            g.Drive.Board();for(int n=0;n<45;n++)g.TickDrive(.05f,default);
            g.Drive.SetRole(SFDriveRole.Gunner);
            // Await a reachable pursuer. Time since boarding alone does not imply gun range.
            for(int n=0;n<240;n++)
            {
                g.TickDrive(.05f,default);
                if(g.Drive.Target>=0&&Mathf.Abs(g.Drive.X-g.Drive.Enemies[g.Drive.Target].X)<g.Drive.Range-.2f)break;
            }
            g.TickDrive(.05f,new SFCommand{shoot=true});yield return View("ram-rifle");
            Check(g.DriveVisiblePose.name.StartsWith("vehicle_ram")&&g.DriveVisibleWheels==2,"Ram combat preserves correct body artwork and two registered rims");
            int ammo=g.Drive.Magazine.Total;Check(g.Drive.Fired>0&&g.Drive.Reload(),"Ram reload starts only after a reachable shot consumes a round");g.TickDrive(.2f,default);yield return View("ram-reload");
            Check(g.Drive.Reloading&&g.DriveVisiblePose.name=="vehicle_ram_0"&&g.Drive.Magazine.Total==ammo,"Ram reload lowers weapon without a mismatched truck or premature ammo transfer");
            g.Drive.SetWeapon(0);g.TickDrive(.2f,default);g.TickDrive(.05f,new SFCommand{shoot=true});yield return View("ram-pistol");
            g.EndDrive();Check(g.StartDrive(true),"two-rider journey enters");g.Drive.Board();for(int n=0;n<120;n++)g.TickDrive(.05f,new SFCommand{punch=true,move=Vector2.left});
            Check(g.BuddyVisible&&g.Drive.BuddyX<g.Drive.X,"both named motorcycle riders are visible together");yield return View("both-riders-back");
            for(int n=0;n<120;n++)g.TickDrive(.05f,new SFCommand{punch=true,move=Vector2.right});yield return View("both-riders-front");
            g.EndDrive();
            g.ReturnToLoadout(0);g.SetState(SFState.Won);float health=g.Player.health;int rounds=g.Player.TotalAmmo;
            Check(g.StartJourney()&&g.JourneyDestination==2&&g.RouteIndex==0,"Curitiba completion connects to Rio through a road leg");
            g.ChangeReadyVehicle();Check(g.Drive.Ram&&g.JourneyDestination==2,"changing vehicle preserves campaign destination");
            g.EndDrive();Check(g.State==SFState.Won&&g.RouteIndex==0&&g.Player.health==health&&g.Player.TotalAmmo==rounds,"cancelled journey restores completed foot mission without reset");
            Check(g.StartJourney(),"cancelled road journey can be resumed");g.Drive.Board();g.Drive.SetRole(SFDriveRole.Gunner);
            for(int n=0;n<1500&&g.Drive.Phase!=SFDrivePhase.Complete;n++)g.TickDrive(.05f,default);
            Check(g.Drive.Phase==SFDrivePhase.Complete,"campaign road leg can finish");g.FinishDrive();
            Check(!g.DriveActive&&g.RouteIndex==2&&g.State==SFState.Menu,"road arrival opens destination loadout once");
            g.SetState(SFState.Won);g.StartJourney();Check(!g.DriveActive&&g.RouteIndex==1,"Rio to Miami uses an overseas chapter cut");
            g.SetState(SFState.Won);g.StartJourney();Check(g.JourneyDestination==4,"Miami road leg leads to Fort Liberty");g.EndDrive();
            g.ReturnToLoadout(4);g.SetState(SFState.Won);g.StartJourney();Check(g.JourneyDestination==3,"Fort Liberty road leg leads to Camp Lejeune");g.EndDrive();
            g.ReturnToLoadout(0);
        }
    }
}
