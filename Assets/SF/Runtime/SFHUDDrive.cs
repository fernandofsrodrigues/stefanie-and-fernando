using UnityEngine;
namespace StefanieAndFernando
{
    public sealed partial class SFHUD
    {
        void DriveHUD()
        {
            if(game.Drive.Motorcycle){MotorcycleHUD();return;}
            var r=game.Drive;bool driver=r.Role==SFDriveRole.Driver,paused=game.State==SFState.Paused;
            Box(0,0,width,132,ink);Text(game.JourneyTitle,32,18,width-240,34,27,white,true);
            Text((driver?"YOU DRIVE / "+game.ConvoyGunner+" COVERS":"YOU SHOOT / "+game.ConvoyDriver+" DRIVES")+" / "+game.ConvoyVehicle,34,58,width-245,27,19,game.Teal,true);
            Box(34,111,width-68,5,ink);Box(34,111,(width-68)*r.Distance/SFDriveModel.Length,5,game.Gold);
            if(Button(paused?"CONTINUE":"PAUSE",new Rect(width-198,20,164,66)))game.SetState(paused?SFState.Playing:SFState.Paused);
            Box(24,142,400,104,ink);Text(Mathf.RoundToInt(r.Speed*3.6f)+" km/h / CONDITION "+r.Integrity+"%",36,151,380,26,19,white,true);
            Text("LANE "+(2-r.TargetLane)+" / 3  |  CLEARED "+r.Defeated+" / 6",36,182,380,26,17,game.Gold);
            Text(r.Distance>=240?"CHECKPOINT SAVED":game.CourseMode?"DESTINATION  /  FORWARD BASE":"DESTINATION / COASTAL OVERLOOK",36,215,380,24,14,game.Teal);
            Box(width-460,142,436,r.Reloading?127:104,ink);Text(r.WeaponName,width-446,151,408,28,19,white,true);
            Text(r.Magazine.Loaded+" / "+r.Magazine.Capacity+"   RESERVE "+r.Magazine.Reserve,width-446,184,408,26,18,game.Gold);
            if(r.JLTV&&r.Weapon==1&&!r.Reloading)Text("LOCK "+Mathf.RoundToInt(r.LockTime*100)+"% / HOLD FIRE",width/2-175,216,350,28,16,game.Gold,true,TextAnchor.MiddleCenter);
            if(r.Reloading)
            {
                Text("RELOAD / "+r.ReloadStage+"  "+r.ReloadTime.ToString("0.0")+"s",width-446,211,408,22,14,game.Gold);
                Box(width-446,237,408,4,new Color(.2f,.24f,.27f));Box(width-446,237,408*r.ReloadProgress,4,game.Gold);
            }
            var target=r.Target>=0?r.Enemies[r.Target]:null;
            Text(target==null?"ROAD CLEAR":target.Name+" / "+Mathf.CeilToInt(target.Health/(target.Technical?160:100)*100)+"%"+(Mathf.Abs(r.X-target.X)>r.Range?" / OUT OF RANGE":""),width-446,r.Reloading?244:215,408,24,14,game.Teal);
            if(paused)
            {
                Box(width/2-280,268,560,352,ink);Text("TAKE A BREATH",width/2-250,287,500,55,30,white,true,TextAnchor.MiddleCenter);
                if(Button("CONTINUE RUN",new Rect(width/2-230,369,460,74),true))game.SetState(SFState.Playing);
                if(Button("LEAVE ROAD",new Rect(width/2-230,481,460,74)))game.EndDrive();return;
            }
            bool ready=r.Phase==SFDrivePhase.Ready,recovery=r.Phase==SFDrivePhase.Recovery,complete=r.Phase==SFDrivePhase.Complete;
            if(ready||recovery||complete)
            {
                Box(width/2-375,270,750,275,ink);
                Text(ready?"CHOOSE YOUR ROLE":recovery?"REGROUP AT THE CHECKPOINT":game.CourseMode?"SAFE AT THE FORWARD BASE":"SAFE AT THE OVERLOOK",width/2-350,285,700,42,29,white,true,TextAnchor.MiddleCenter);
                Text(complete?r.Defeated+" pursuers stopped / "+r.Escaped+" left behind":"Three lanes. Position freely. Watch marked firing zones.",width/2-350,335,700,31,17,game.Teal,false,TextAnchor.MiddleCenter);
                // Course interlude: the vehicle is locked, so CHANGE's slot skips the drive and cuts straight to the next chapter.
                if(ready&&game.CourseMode){if(Button("SKIP DRIVE  /  AL ANBAR",new Rect(width/2-230,555,460,60)))game.SkipCourseDrive();}
                else if(ready&&Button(game.ConvoyVehicle+"  /  CHANGE",new Rect(width/2-230,555,460,60)))game.ChangeReadyVehicle();
                if(ready&&Button(driver?"ROLE: DRIVER  >  GUNNER":"ROLE: GUNNER  >  DRIVER",new Rect(width/2-230,381,460,62)))game.SwitchDriveRole();
                if(Button(ready?(r.JLTV?"BOARD JLTV":"BOARD PICKUP"):recovery?"RETRY CHECKPOINT":game.DriveExitLabel,new Rect(width/2-230,ready?457:397,460,68),true))
                {if(ready)r.Board();else if(recovery)r.Retry();else game.FinishDrive();}
            }
            else if(r.Phase!=SFDrivePhase.Driving)Text(r.Phase==SFDrivePhase.Boarding?"SETTLING IN":r.Phase==SFDrivePhase.Stopping?"ARRIVING":"STRETCH YOUR LEGS",width/2-250,270,500,45,25,white,true,TextAnchor.MiddleCenter);
            if(r.Phase==SFDrivePhase.Driving)
            {
                foreach(var e in r.Enemies)
                {
                    if(!e.Active||e.Resolved)continue;
                    var p=game.Camera.WorldToScreenPoint(new Vector3(e.X,SFGame.DriveRoadY(e.Lane)+1.7f,0));float x=p.x*900/Screen.height,y=900-p.y*900/Screen.height;
                    Box(x-91,y-34,182,31,ink);Text((e.Id==r.Target?"> ":"")+e.Name+(e.Warning?" / FIRE!":""),x-88,y-33,176,29,12,e.Warning?new Color(1,.5f,.28f):game.Teal,true,TextAnchor.MiddleCenter);
                }
                if(game.TouchControls)
                {
                    var at=SFTouchInput.StickCenter;Box(at.x-100,at.y-100,200,200,new Color(.015f,.06f,.07f,.62f));
                    Box(at.x-28+game.Touch.Stick.x*70,at.y-28+game.Touch.Stick.y*70,56,56,new Color(.37f,.86f,.83f,.7f));
                    Text(driver?"STICK: POSITION / LANE":"UP / DOWN: TARGET",22,798,260,30,15,white,true,TextAnchor.MiddleCenter);
                    foreach(int i in new[]{0,2,6,8,13,15})
                    {var rect=SFTouchInput.Control(i,width);Box(rect.x,rect.y,rect.width,rect.height,game.Touch.Held(i)?game.Teal:ink);
                        string label=i==0?(driver?"THROTTLE":"FIRE"):i==2?(driver?"BRAKE":"AIM"):i==6?"WEAPON":i==8?"ROLE":i==13?"TARGET":"RELOAD";
                        Text(label,rect.x+4,rect.y,rect.width-8,rect.height,15,white,true,TextAnchor.MiddleCenter);}
                }
                else if(Button(driver?"SWITCH TO GUNNER":"SWITCH TO DRIVER",new Rect(width/2-150,145,300,61)))game.SwitchDriveRole();
            }
            Box(0,845,width,55,ink);
            string help=game.GamepadActive?(driver?"RT throttle / LT brake / stick position + lanes / D-pad LEFT role":"RT fire / LB aim / X reload / Y weapon / RB target / LEFT role"):game.TouchControls?"Choose DRIVER or GUNNER at any time. Red lanes warn of incoming fire.":driver?"A-D position / W-S lanes / SHIFT gas / CTRL brake / TAB role / ESC pause":"LMB or F fire / RMB aim / R reload / MMB or Q weapon / E target / TAB role";
            Text(help,20,855,width-40,37,16,white,false,TextAnchor.MiddleCenter);
        }
    }
}
