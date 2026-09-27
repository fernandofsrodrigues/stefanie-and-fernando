using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace StefanieAndFernando
{
    // Standard Input System Gamepad layout. Button names follow physical Xbox positions.
    // Commands never merge with a parked mouse cursor or stale touch contacts.
    public sealed class SFGamepadInput
    {
        public Gamepad Device {get;private set;}
        public bool Active {get;private set;}
        public bool WaitingForNeutral {get;private set;}
        Vector2 lastLeft,lastRight;
        bool takeoverReady=true;
        public void ResetHeld(){WaitingForNeutral=true;}
        public static bool Neutral(Gamepad p)
        {
            if(p==null)return true;
            if(p.leftStick.ReadValue().magnitude>.15f||p.rightStick.ReadValue().magnitude>.15f||p.leftTrigger.ReadValue()>.12f||p.rightTrigger.ReadValue()>.12f)return false;
            foreach(var c in p.allControls)if(c is ButtonControl b&&b.isPressed)return false;
            return true;
        }
        public bool Poll()
        {
            bool disconnected=Active&&(Device==null||!Device.added||!Device.enabled);
            if(disconnected){Active=false;Device=null;ResetHeld();takeoverReady=true;}
            var p=Gamepad.current;
            if(p==null||!p.added||!p.enabled)return disconnected;
            bool pressed=false;foreach(var c in p.allControls)if(c is ButtonControl b&&b.wasPressedThisFrame){pressed=true;break;}
            Vector2 left=p.leftStick.ReadValue(),right=p.rightStick.ReadValue();
            bool moved=(left-lastLeft).magnitude>.22f||(right-lastRight).magnitude>.22f;
            lastLeft=left;lastRight=right;
            bool neutral=Neutral(p);if(neutral)takeoverReady=true;
            var k=Keyboard.current;var m=Mouse.current;var t=Touchscreen.current;
            bool other=(k!=null&&k.anyKey.wasPressedThisFrame)||(m!=null&&(m.leftButton.wasPressedThisFrame||m.rightButton.wasPressedThisFrame||m.middleButton.wasPressedThisFrame||m.backButton.wasPressedThisFrame||m.forwardButton.wasPressedThisFrame||m.scroll.ReadValue().sqrMagnitude>0||m.delta.ReadValue().magnitude>3))||(t!=null&&t.primaryTouch.press.wasPressedThisFrame);
            if(other){Active=false;takeoverReady=neutral;ResetHeld();}
            else if(takeoverReady&&(pressed||moved))
            {
                if(Device!=p){Device=p;ResetHeld();}
                Active=true;
            }
            if(WaitingForNeutral&&neutral)WaitingForNeutral=false;
            return disconnected;
        }
        public SFCommand Read(bool armed,bool rifle,bool holding,float dt)
        {
            var p=Device;if(!Active||p==null||WaitingForNeutral)return default;
            bool combatArmed=armed&&!holding;
            var c=new SFCommand{move=Vector2.ClampMagnitude(p.leftStick.ReadValue(),1),run=p.leftStickButton.isPressed,
                jump=p.buttonSouth.wasPressedThisFrame,dash=p.buttonEast.wasPressedThisFrame,
                crouch=p.leftTrigger.isPressed,grapple=p.rightShoulder.wasPressedThisFrame,ground=p.rightStickButton.wasPressedThisFrame,
                kick=!combatArmed&&p.buttonWest.wasPressedThisFrame,reload=combatArmed&&p.buttonWest.wasPressedThisFrame,swap=p.dpad.left.wasPressedThisFrame,assist=p.dpad.right.wasPressedThisFrame,
                toggleWalk=p.dpad.down.wasPressedThisFrame};
            if(c.move.magnitude<.12f)c.move=Vector2.zero;
            if(combatArmed){c.shoot=p.rightTrigger.isPressed||p.rightTrigger.wasPressedThisFrame;c.aim=p.leftShoulder.isPressed;}
            else{c.punch=p.rightTrigger.isPressed||p.rightTrigger.wasPressedThisFrame;c.guard=p.leftShoulder.isPressed;}
            if(p.dpad.up.wasPressedThisFrame){if(p.leftShoulder.isPressed)c.backup=true;else c.interact=true;}
            if(p.buttonNorth.wasPressedThisFrame&&!holding){if(armed&&rifle)c.toggleArmed=true;else c.weapon=armed?2:1;}
            Vector2 aim=p.rightStick.ReadValue();
            if(combatArmed&&Mathf.Abs(aim.x)>.3f){c.aim=true;c.aimFacing=aim.x>0?1:-1;}
            c.zoom=Mathf.Abs(aim.y)>.35f?aim.y*Mathf.Min(dt,.05f)*3:0;
            return c;
        }
    }
}
