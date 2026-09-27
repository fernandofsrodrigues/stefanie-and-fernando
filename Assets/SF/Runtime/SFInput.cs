using UnityEngine;
using UnityEngine.InputSystem;

namespace StefanieAndFernando
{
    // One map for gameplay and runtime input-device tests. Mouse Back/Forward are swappable.
    public static class SFInput
    {
        public static SFCommand Read(bool armed,bool swapThumbs)
        {
            var k=Keyboard.current;var m=Mouse.current;var c=new SFCommand();
            if(k!=null)
            {
                c.move=new Vector2((k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0),(k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0));
                c.jump=k.spaceKey.wasPressedThisFrame;c.crouch=k.leftCtrlKey.isPressed||k.rightCtrlKey.isPressed;
                c.reload=k.rKey.wasPressedThisFrame;c.toggleArmed=k.qKey.wasPressedThisFrame;c.interact=k.eKey.wasPressedThisFrame;c.assist=k.vKey.wasPressedThisFrame;c.swap=k.tabKey.wasPressedThisFrame;
                c.weapon=k.digit1Key.wasPressedThisFrame?1:k.digit2Key.wasPressedThisFrame?2:0;
                c.punch=!armed&&(k.jKey.isPressed||k.jKey.wasPressedThisFrame);c.kick=!armed&&k.kKey.wasPressedThisFrame;
                c.shoot=armed&&(k.fKey.isPressed||k.fKey.wasPressedThisFrame);c.guard=k.lKey.isPressed;c.dash=k.xKey.wasPressedThisFrame;
                c.run=k.leftShiftKey.isPressed||k.rightShiftKey.isPressed;c.toggleWalk=k.capsLockKey.wasPressedThisFrame;
                c.walk=k.leftAltKey.isPressed||k.rightAltKey.isPressed;c.backup=k.nKey.wasPressedThisFrame;
                c.grapple=k.gKey.wasPressedThisFrame;c.ground=k.bKey.wasPressedThisFrame;
            }
            if(m!=null)
            {
                c.cycleWeapon=m.middleButton.wasPressedThisFrame;
                float scroll=m.scroll.y.ReadValue();c.zoom=scroll==0?0:Mathf.Sign(scroll);
                if(armed){c.shoot|=m.leftButton.isPressed||m.leftButton.wasPressedThisFrame;c.aim=m.rightButton.isPressed;}
                else{c.punch|=m.leftButton.isPressed||m.leftButton.wasPressedThisFrame;c.kick|=m.rightButton.wasPressedThisFrame;}
                c.grapple|=(swapThumbs?m.forwardButton:m.backButton).wasPressedThisFrame;
                c.ground|=(swapThumbs?m.backButton:m.forwardButton).wasPressedThisFrame;
            }
            return c;
        }
    }
}
