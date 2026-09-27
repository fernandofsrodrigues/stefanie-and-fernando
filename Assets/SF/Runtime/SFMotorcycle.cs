using UnityEngine;
namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        SFArt bikeArt,buddyArt;int bikeRider;
        SpriteRenderer buddyBike,buddyGhost,buddyShadow;
        readonly System.Collections.Generic.List<Transform> buddyWheels=new System.Collections.Generic.List<Transform>();
        internal bool BuddyVisible=>buddyBike!=null&&buddyBike.enabled&&buddyBike.gameObject.activeInHierarchy;
        public string MotorcycleRider=>bikeRider==0?"FERNANDO":"STEFANIE";
        // Rear and front axle coordinates normalized within each imported sprite bounds.
        static readonly Vector4[][] BikeAxles=new Vector4[][]{new[]{new Vector4(0.1632000f,0.1983806f,0.8448000f,0.2004049f),new Vector4(0.1666667f,0.1840491f,0.8472222f,0.1840491f),new Vector4(0.1626794f,0.1890756f,0.8452951f,0.1953782f),new Vector4(0.1705426f,0.1941545f,0.8527132f,0.1983299f)},new[]{new Vector4(0.1560976f,0.2243187f,0.8569106f,0.2243187f),new Vector4(0.1604538f,0.2189474f,0.8557536f,0.2147368f),new Vector4(0.1585761f,0.2141372f,0.8511327f,0.2141372f),new Vector4(0.1515152f,0.2136564f,0.8437002f,0.2136564f)}};
        bool LoadMotorcycleArt()
        {
            bikeRider=Mathf.Clamp(Selected,0,1);
            bikeArt=Resources.Load<SFArt>("SF/vehicle_bike_"+(bikeRider==0?"fernando":"stefanie"));
            buddyArt=Resources.Load<SFArt>("SF/vehicle_bike_"+(bikeRider==0?"stefanie":"fernando"));
            if(bikeArt!=null&&bikeArt.frames.Length==4&&buddyArt!=null&&buddyArt.frames.Length==4)return true;
            Notify("MOTORCYCLE ART UNAVAILABLE");return false;
        }
        void CreateMotorcycleWheels()
        {
            driveShadow.transform.localScale=new Vector3(3.05f,.22f,1);
            buddyBike=DriveSprite("Following partner motorcycle",buddyArt.Frame(0),10);
            buddyGhost=DriveSprite("Following partner transition",buddyArt.Frame(0),11);
            buddyShadow=DriveSprite("Partner motorcycle shadow",Ellipse,0);buddyShadow.color=new Color(0,0,0,.32f);buddyShadow.transform.localScale=new Vector3(3.05f,.22f,1);
            for(int i=0;i<4;i++){var ax=BikeAxles[1-bikeRider][i];buddyWheels.Add(CreateDriveWheel(buddyBike,buddyArt.Frame(i),ax.x,ax.y,.040f,"Partner rear rim").transform);buddyWheels.Add(CreateDriveWheel(buddyBike,buddyArt.Frame(i),ax.z,ax.w,.040f,"Partner front rim").transform);}
            for(int frame=0;frame<4;frame++)
            {
                var axle=BikeAxles[bikeRider][frame];var sprite=bikeArt.Frame(frame);
                // Only the small inner hub rotates. Forks, calipers and outer tires stay in authored art.
                MakeDriveWheel(sprite,axle.x,axle.y,.040f);
                MakeDriveWheel(sprite,axle.z,axle.w,.040f);
            }
        }
        internal void MotorcyclePose(out int a,out int b,out float blend)
        {
            a=b=0;blend=0;var phase=Drive.Phase;
            if(phase==SFDrivePhase.Ready||phase==SFDrivePhase.Complete)return;
            if(phase==SFDrivePhase.Boarding||phase==SFDrivePhase.Dismounting)
            {
                float time=phase==SFDrivePhase.Boarding?Drive.Clock:1.8f-Drive.Clock;
                float step=Mathf.Clamp((time-.25f)/.46f,0,3);a=Mathf.FloorToInt(step);b=Mathf.Min(3,a+1);
                blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.70f,1,step-a));return;
            }
            a=b=Drive.Speed>.15f?3:2;
        }
        Vector2 MotorcycleAxle(Sprite sprite,float x,float y)=>new Vector2(sprite.bounds.min.x+sprite.bounds.size.x*x,sprite.bounds.min.y+sprite.bounds.size.y*y);
        void SetMotorcyclePose(SpriteRenderer sr,int frame,float alpha)
        {
            SetBikePose(sr,frame,alpha,bikeArt,bikeRider,Drive.X,Drive.Lane);
        }
        void SetBikePose(SpriteRenderer sr,int frame,float alpha,SFArt art,int rider,float x,float lane)
        {
            var sprite=art.Frame(frame);var axle=BikeAxles[rider][frame];
            Vector2 rear=MotorcycleAxle(sprite,axle.x,axle.y),front=MotorcycleAxle(sprite,axle.z,axle.w),span=front-rear;
            float scale=2.15f/span.magnitude;var rotation=Quaternion.Euler(0,0,-Mathf.Atan2(span.y,span.x)*Mathf.Rad2Deg);
            sr.sprite=sprite;sr.enabled=alpha>0;sr.color=new Color(1,1,1,alpha);
            sr.transform.localScale=Vector3.one*scale;sr.transform.localRotation=rotation;
            sr.transform.position=new Vector3(x,DriveRoadY(lane)+.47f,0)-rotation*((Vector3)((rear+front)*.5f)*scale);
            sr.sortingOrder=20-Mathf.RoundToInt(lane*5)+(sr==driveGhost||sr==buddyGhost?1:0);
        }
        void RenderBuddyBike(int a,int b,float blend)
        {
            SetBikePose(buddyBike,a,1-blend,buddyArt,1-bikeRider,Drive.BuddyX,Drive.BuddyLane);
            SetBikePose(buddyGhost,b,blend,buddyArt,1-bikeRider,Drive.BuddyX,Drive.BuddyLane);
            buddyShadow.transform.position=new Vector3(Drive.BuddyX,DriveRoadY(Drive.BuddyLane)+.05f,0);
            for(int i=0;i<buddyWheels.Count;i++)
            {
                bool ghost=blend>=.5f;var w=buddyWheels[i];bool active=i/2==(ghost?b:a)&&(Drive.Phase==SFDrivePhase.Driving||Drive.Phase==SFDrivePhase.Stopping||Drive.Phase==SFDrivePhase.Recovery);
                w.gameObject.SetActive(active);w.SetParent(ghost?buddyGhost.transform:buddyBike.transform,false);w.localRotation=Quaternion.Euler(0,0,Drive.WheelAngle);w.GetComponent<MeshRenderer>().sortingOrder=buddyBike.sortingOrder+2;
            }
        }
        internal Vector3 MotorcycleAxleWorld(int frame,bool front)
        {
            var a=BikeAxles[bikeRider][frame];return driveTruck.transform.TransformPoint(MotorcycleAxle(bikeArt.Frame(frame),front?a.z:a.x,front?a.w:a.y));
        }
    }
}
