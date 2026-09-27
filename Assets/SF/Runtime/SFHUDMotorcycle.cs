using UnityEngine;
namespace StefanieAndFernando
{
    public sealed partial class SFHUD
    {
        void MotorcycleHUD()
        {
            var r=game.Drive;bool paused=game.State==SFState.Paused;
            Box(0,0,width,132,ink);Text(game.JourneyTitle+" / MOTORCYCLES",32,18,width-245,34,27,white,true);
            Text("BMW GS ADVENTURE + ENERGICA EGO / LEAD: "+game.MotorcycleRider,34,58,width-245,27,19,game.Teal,true);
            Box(34,111,width-68,5,ink);Box(34,111,(width-68)*r.Distance/SFDriveModel.Length,5,game.Gold);
            if(Button(paused?"CONTINUE":"PAUSE",new Rect(width-198,20,164,66)))game.SetState(paused?SFState.Playing:SFState.Paused);
            Box(24,142,400,100,ink);Text(Mathf.RoundToInt(r.Speed*3.6f)+" km/h / CONDITION "+r.Integrity+"%",36,151,380,27,19,white,true);
            Text("LANE "+(2-r.TargetLane)+" / 3   /   COASTAL OVERLOOK",36,194,380,28,16,game.Teal);
            if(paused)
            {
                Box(width/2-280,268,560,340,ink);Text("TAKE A BREATH",width/2-250,290,500,52,30,white,true,TextAnchor.MiddleCenter);
                if(Button("CONTINUE RIDE",new Rect(width/2-230,369,460,74),true))game.SetState(SFState.Playing);
                if(Button("LEAVE ROAD",new Rect(width/2-230,481,460,74)))game.EndDrive();return;
            }
            bool ready=r.Phase==SFDrivePhase.Ready,complete=r.Phase==SFDrivePhase.Complete;
            if(ready||complete)
            {
                Box(width/2-375,270,750,240,ink);
                Text(ready?"A MOMENT ON THE OPEN ROAD":"SAFE AT THE OVERLOOK",width/2-350,288,700,43,28,white,true,TextAnchor.MiddleCenter);
                Text(ready?"Two riders together. Three lanes. Independent positioning and pedals.":"Parked and dismounted. Your mission state is preserved.",width/2-350,342,700,34,17,game.Teal,false,TextAnchor.MiddleCenter);
                if(ready&&Button("MOTORCYCLES / CHANGE VEHICLE",new Rect(width/2-230,555,460,60)))game.ChangeReadyVehicle();
                if(Button(ready?"MOUNT MOTORCYCLE":game.DriveExitLabel,new Rect(width/2-230,408,460,74),true))
                {if(ready)r.Board();else game.FinishDrive();}
            }
            else if(r.Phase!=SFDrivePhase.Driving)
                Text(r.Phase==SFDrivePhase.Boarding?"MOUNTING":r.Phase==SFDrivePhase.Stopping?"PARKING":"DISMOUNTING",width/2-250,270,500,45,25,white,true,TextAnchor.MiddleCenter);
            if(r.Phase==SFDrivePhase.Driving&&game.TouchControls)
            {
                var at=SFTouchInput.StickCenter;Box(at.x-100,at.y-100,200,200,new Color(.015f,.06f,.07f,.62f));
                Box(at.x-28+game.Touch.Stick.x*70,at.y-28+game.Touch.Stick.y*70,56,56,new Color(.37f,.86f,.83f,.7f));
                Text("STICK: POSITION / LANE",22,798,260,30,15,white,true,TextAnchor.MiddleCenter);
                foreach(int i in new[]{0,2})
                {var rect=SFTouchInput.Control(i,width);Box(rect.x,rect.y,rect.width,rect.height,game.Touch.Held(i)?game.Teal:ink);Text(i==0?"THROTTLE":"BRAKE",rect.x+4,rect.y,rect.width-8,rect.height,15,white,true,TextAnchor.MiddleCenter);}
            }
            Box(0,845,width,55,ink);
            Text(game.GamepadActive?"A mount / RT throttle / LT brake / stick position + lanes / START pause":game.TouchControls?"Stick: position and lanes. Hold THROTTLE or BRAKE.":"E / SPACE mount and exit / A-D position / W-S lanes / SHIFT gas / CTRL brake / ESC pause",20,855,width-40,37,16,white,false,TextAnchor.MiddleCenter);
        }
    }
}
