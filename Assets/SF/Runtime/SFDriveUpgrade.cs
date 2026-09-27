using UnityEngine;
namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        SFArt ramArt;
        int journeyNext=-1;
        static readonly int[] JourneyOrder={2,4,1,5,3,0};
        public string ConvoyVehicle=>Drive!=null&&Drive.JLTV?"DESERT JLTV":Drive!=null&&Drive.Ram?"RAM RAMPAGE R/T":"BYD SHARK 6";
        public string ConvoyDriver=>Drive!=null&&Drive.Ram?"FERNANDO":"STEFANIE";
        public string ConvoyGunner=>Drive!=null&&Drive.Ram?"STEFANIE":"FERNANDO";
        // The course's only road is the Al Anbar interlude: a travel cut to Iraq, then an in-country convoy (never a road from Rio).
        public string JourneyTitle=>CourseMode?"INTERLUDE  /  AL ANBAR DESERT CONVOY":journeyNext<0?"SERRA / ROAD TRIP":RouteIndex==0?"ROAD TO RIO":"ROAD LEG / "+RouteName+" > "+RouteNames[journeyNext];
        public string DriveExitLabel=>journeyNext<0?"BACK TO MISSIONS":"CONTINUE TO "+RouteNames[journeyNext];
        internal int JourneyDestination=>journeyNext;
        internal bool DriveMenuActive=>DriveActive&&(Drive.Phase==SFDrivePhase.Ready||Drive.Phase==SFDrivePhase.Recovery||Drive.Phase==SFDrivePhase.Complete);
        Sprite DriveCruisePose=>Drive.JLTV?jltvBodyArt.Frame(0):Drive.Ram?ramArt.Frame(0):driveArt.Frame(0);
        Sprite DriveGunPose(int weapon)=>Drive.Ram?ramArt.Frame(weapon+1):gunArt.Frame(weapon);
        Vector2 CurrentGunnerOffset(int weapon,Vector2 p)
        {
            if(!Drive.Ram)return DriveGunnerOffset(weapon,p);
            float torso=RecoilBand(p.x,.33f,.35f,.42f,.46f)*RecoilBand(p.y,.64f,.7f,.89f,.94f);
            float hands=RecoilBand(p.x,weapon==0?.25f:.13f,weapon==0?.28f:.16f,.37f,.41f)*RecoilBand(p.y,.65f,.73f,.84f,.89f);
            return new Vector2(.045f,.021f)*Mathf.Max(torso*.3f,hands);
        }
        public bool StartJourney()
        {
            if(State!=SFState.Won||DriveActive||CourseMode)return false;
            int next=JourneyOrder[RouteIndex];
            // Overseas chapters use a travel cut, never a fictitious road across the ocean.
            if(RouteIndex==2||RouteIndex==3||RouteIndex==5){ReturnToLoadout(next);return true;}
            if(!StartDrive())return false;
            journeyNext=next;return true;
        }
        public void FinishDrive()
        {
            if(!DriveActive||Drive.Phase!=SFDrivePhase.Complete)return;
            // Course: EndDrive frees the road meshes and sprites first, then the next chapter loads (Rio's interlude opens Al Anbar).
            int next=journeyNext;EndDrive();if(CourseMode){if(next>=0)GoToChapter(Chapter.next);return;}if(next>=0)ReturnToLoadout(next);
        }
        public void ChangeReadyVehicle()
        {
            // The course interlude is locked to the Desert JLTV; its ready card offers SKIP DRIVE instead of CHANGE.
            if(CourseMode)return;
            if(!DriveActive||Drive.Phase!=SFDrivePhase.Ready)return;
            int next=journeyNext;bool ram=!Drive.Motorcycle&&!Drive.Ram&&!Drive.JLTV,jltv=Drive.Ram,bike=Drive.JLTV;var role=Drive.Role;
            EndDrive();if(StartDrive(bike,ram,jltv)){journeyNext=next;if(!bike)Drive.SetRole(role);}
        }
    }
}
