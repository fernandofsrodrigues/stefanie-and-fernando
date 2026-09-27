using UnityEngine;
using UnityEngine.InputSystem;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        bool driveNeedsTriggerRelease;
        bool DriveFireHeld()=>GamepadActive?Pad.Device.rightTrigger.isPressed:TouchControls?Touch.Held(0):
            (Mouse.current!=null&&Mouse.current.leftButton.isPressed)||(Keyboard.current!=null&&Keyboard.current.fKey.isPressed);
        public void SwitchDriveRole()
        {
            if(!DriveActive||Drive.Motorcycle)return;
            driveNeedsTriggerRelease=DriveFireHeld();
            Drive.SetRole(Drive.Role==SFDriveRole.Driver?SFDriveRole.Gunner:SFDriveRole.Driver);
            Touch.Reset();Pad.ResetHeld();
        }
        internal SFCommand ReadDriveCommand()
        {
            SFCommand c;
            if(GamepadActive)
            {
                c=Pad.Read(true,true,false,Time.deltaTime);
                c.cycleWeapon=c.toggleArmed;c.toggleArmed=false;
                c.interact|=c.grapple;c.grapple=false;
                if(Drive.Role==SFDriveRole.Driver){c.punch=c.shoot;c.shoot=false;}
                // A activates the focused road menu button, without also boarding behind it.
                if(DriveMenuActive)c.jump=c.interact=false;
            }
            else if(TouchControls)
            {
                c=Touch.Read(Player,Screen.width*900f/Screen.height,true);
                if(Drive.Role==SFDriveRole.Driver){c.punch=c.shoot;c.shoot=false;}
            }
            else
            {
                c=SFInput.Read(true,SwapThumbs);
                if(Drive.Role==SFDriveRole.Driver)c.punch=c.run;
                // Road weapon slots are explicit: 1 pistol, 2 rifle. Q also cycles.
                var mouse=Mouse.current;
                if(mouse!=null)
                {
                    Vector2 p=mouse.position.ReadValue();
                    bool ui=p.y<Screen.height*.10f||p.y>Screen.height*.72f;
                    if(ui)c.shoot=Keyboard.current!=null&&Keyboard.current.fKey.isPressed;
                    else if(mouse.leftButton.wasPressedThisFrame&&Drive.Role==SFDriveRole.Gunner)
                    {
                        var world=Camera.ScreenToWorldPoint(p);int best=-1;float distance=100;
                        foreach(var e in Drive.Enemies)
                        {
                            if(!e.Active||e.Resolved)continue;
                            float d=Vector2.Distance(world,new Vector2(e.X,DriveRoadY(e.Lane)+.75f));
                            if(d<2.2f&&d<distance){best=e.Id;distance=d;}
                        }
                        if(best>=0)Drive.SelectTarget(best);
                    }
                }
            }
            if(driveNeedsTriggerRelease)
            {
                if(DriveFireHeld())c.shoot=c.punch=false;
                else driveNeedsTriggerRelease=false;
            }
            if(Drive.Role==SFDriveRole.Gunner)c.aim|=c.crouch;
            if(c.swap){SwitchDriveRole();c=default;}
            return c;
        }
        internal static float DriveRoadY(float lane)=>-3.85f+lane*1.1f;
    }
}
