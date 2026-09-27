using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    public enum SFDrivePhase { Ready, Boarding, Driving, Stopping, Dismounting, Complete, Recovery }

    public sealed partial class SFGame
    {
        public SFDriveModel Drive {get;private set;}
        public bool DriveActive=>Drive!=null;
        public void RunDriveQA(){if(LocalQAAllowed&&!DriveActive&&GetComponent<SFDriveCheck>()==null){SmokeMode=true;gameObject.AddComponent<SFDriveCheck>();}}
        Transform driveRoot;
        SpriteRenderer driveTruck,driveGhost,driveDeparture,driveArrival,driveShadow;
        readonly List<SpriteRenderer> driveBarriers=new List<SpriteRenderer>(),driveMarks=new List<SpriteRenderer>();
        readonly List<Mesh> driveMeshes=new List<Mesh>();
        readonly List<Transform> driveWheels=new List<Transform>();
        readonly List<Sprite> driveWheelSprites=new List<Sprite>();
        readonly Dictionary<GameObject,bool> driveVisibility=new Dictionary<GameObject,bool>();
        SFArt driveArt,boardArt,gunArt;
        readonly List<SpriteRenderer> driveEnemies=new List<SpriteRenderer>(),driveTraces=new List<SpriteRenderer>(),driveWarnings=new List<SpriteRenderer>();
        SFState driveReturnState;
        Vector3 driveOldCamera;float driveOldSize;
        // Arrival plate colour. White everywhere except the course interlude's Serra fallback, so the campaign arrival is unchanged.
        Color driveTint=Color.white;
        public bool StartDrive(bool motorcycle=false,bool ram=false,bool jltv=false)
        {
            if(motorcycle){ram=false;jltv=false;}if(jltv)ram=false;
            // The course opens only the JLTV interlude, and only from the Won card of a chapter with driveAfter (Rio). The campaign runs with CourseMode false.
            if((CourseMode&&!(State==SFState.Won&&Chapter.driveAfter&&jltv))||DriveActive||LayoutBlocked||(State!=SFState.Menu&&State!=SFState.Won))return false;
            driveArt=Resources.Load<SFArt>("SF/vehicle_pickup_drive");boardArt=Resources.Load<SFArt>("SF/vehicle_pickup_board");
            gunArt=Resources.Load<SFArt>("SF/vehicle_pickup_guns");
            ramArt=ram?Resources.Load<SFArt>("SF/vehicle_ram"):null;
            if(ram&&(ramArt==null||ramArt.frames.Length!=3)){Notify("RAM ART UNAVAILABLE");return false;}
            if(jltv&&!LoadJLTVArt())return false;
            LoadDriveWeaponArt();
            var departure=Resources.Load<SFArt>("SF/plate_serra_convoy");var arrival=Resources.Load<SFArt>("SF/plate_serra_overlook_hd");
            // Course interlude: desert plates when they exist, otherwise the Serra plates above under a dust tint. The campaign keeps both plates untinted.
            driveTint=Color.white;var departureTint=Color.white;if(CourseMode)CourseDrivePlates(ref departure,ref arrival,ref departureTint);
            if(driveArt==null||gunArt==null||boardArt==null||departure==null||arrival==null||boardArt.frames.Length!=2||!DriveReloadArtReady){Notify("ROAD ART UNAVAILABLE");return false;}
            if(motorcycle&&!LoadMotorcycleArt())return false;
            driveReturnState=State;driveOldCamera=Camera.transform.position;driveOldSize=Camera.orthographicSize;
            Drive=new SFDriveModel(!motorcycle,motorcycle,ram,jltv);Drive.SetViewport(Camera.aspect*5.625f);driveRoot=new GameObject(motorcycle?"Serra motorcycle ride":"Serra pickup interlude").transform;
            // Actor shadows are separate objects; preserve their prior active state as well as World.
            driveVisibility.Clear();driveVisibility[World.gameObject]=World.gameObject.activeSelf;
            foreach(var r in FindObjectsByType<Renderer>())driveVisibility[r.gameObject]=r.gameObject.activeSelf;
            foreach(var a in Actors)if(a.shadow!=null)driveVisibility[a.shadow.gameObject]=a.shadow.gameObject.activeSelf;
            foreach(var pair in driveVisibility)pair.Key.SetActive(false);
            SetState(SFState.Playing);ShowHelp=false;NoticeTime=SpeechTime=0;
            driveDeparture=DriveSprite("Forest approach",departure.Frame(0),-100);
            if(CourseMode)driveDeparture.color=departureTint;
            driveArrival=DriveSprite("Coastal overlook",arrival.Frame(0),-99);
            driveShadow=DriveSprite("Truck contact shadow",Ellipse,0);driveShadow.color=new Color(0,0,0,.32f);driveShadow.transform.localScale=new Vector3(4.8f,.28f,1);
            driveTruck=DriveSprite("Pickup and crew",driveArt.Frame(0),10);driveGhost=DriveSprite("Pickup transition",boardArt.Frame(0),11);
            CreateRoadMedian();
            for(int i=0;i<SFDriveModel.BarrierDistances.Length;i++)
            {var sr=DriveSprite("Roadwork barrier "+i,Resources.Load<SFArt>("SF/props_solid").Frame(1),8);sr.transform.localScale=Vector3.one*(1.2f/sr.sprite.bounds.size.x);driveBarriers.Add(sr);}
            var enemyArt=Resources.Load<SFArt>("SF/vehicle_pursuit");
            foreach(var enemy in Drive.Enemies)
            {
                var sr=DriveSprite(enemy.Name,enemy.Technical?technicalArt.Frame(enemy.FromAhead?1:0):enemyArt.Frame(enemy.Id%2),6);sr.transform.localScale=Vector3.one*((enemy.Technical?4.3f:enemy.Id%2==0?4.1f:5.1f)/sr.sprite.bounds.size.x);driveEnemies.Add(sr);
                var warning=DriveSprite("Threat lane "+enemy.Id,Pixel,2);driveWarnings.Add(warning);
                CreatePursuitVisuals(sr,enemy);
            }
            for(int i=0;i<16;i++)driveTraces.Add(DriveSprite("Vehicle tracer "+i,Pixel,60));
            if(motorcycle)CreateMotorcycleWheels();
            else
            {
            if(jltv){CreateJLTVVisuals();}
            else if(ram){foreach(var pose in ramArt.frames){MakeDriveWheel(pose,.201f,.22f,.044f);MakeDriveWheel(pose,.817f,.22f,.044f);}}
            else {MakeDriveWheel(driveArt.Frame(0),.188f,.218f,.054f);MakeDriveWheel(driveArt.Frame(0),.819f,.218f,.054f);}
            if(!ram&&!jltv)foreach(var pose in gunArt.frames){MakeDriveWheel(pose,.183f,.187f,.045f);MakeDriveWheel(pose,.823f,.187f,.045f);}
            CreateDriveWeaponVisuals();
            if(!jltv)CreateDriveRecoilVisuals();
            }
            Audio.StartAmbience();Audio.BeginVehicleAudio(motorcycle);RenderDrive();return true;
        }
        SpriteRenderer DriveSprite(string name,Sprite sprite,int order)
        {var sr=CreateSprite(name,sprite,Color.white,order);sr.transform.SetParent(driveRoot,false);return sr;}
        void MakeDriveWheel(Sprite sprite,float x,float y,float radiusRatio)
        {
            var wheel=CreateDriveWheel(driveTruck,sprite,x,y,radiusRatio,"Rotating pickup rim");
            driveWheels.Add(wheel.transform);driveWheelSprites.Add(sprite);
        }
        MeshRenderer CreateDriveWheel(SpriteRenderer owner,Sprite sprite,float x,float y,float radiusRatio,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(owner.transform,false);
            float radius=sprite.bounds.size.x*radiusRatio;var center=new Vector2(sprite.bounds.min.x+sprite.bounds.size.x*x,sprite.bounds.min.y+sprite.bounds.size.y*y);
            go.transform.localPosition=center;var mesh=new Mesh();driveMeshes.Add(mesh);
            var vertices=new Vector3[34];var uv=new Vector2[34];var colors=new Color[34];var tris=new int[96];
            for(int i=0;i<34;i++)
            {
                Vector2 v=i==0?Vector2.zero:new Vector2(Mathf.Cos((i-1)*Mathf.PI/16),Mathf.Sin((i-1)*Mathf.PI/16))*radius;
                vertices[i]=v;colors[i]=Color.white;var pixel=(center+v)*sprite.pixelsPerUnit+sprite.pivot;
                uv[i]=new Vector2((sprite.rect.x+pixel.x)/sprite.texture.width,(sprite.rect.y+pixel.y)/sprite.texture.height);
                if(i>1){int n=(i-2)*3;tris[n]=0;tris[n+1]=i-1;tris[n+2]=i;}
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=tris;mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=Unlit;r.sortingOrder=12;var props=new MaterialPropertyBlock();props.SetTexture("_MainTex",sprite.texture);props.SetColor("_RendererColor",Color.white);r.SetPropertyBlock(props);
            return r;
        }
        public void EndDrive()
        {
            if(!DriveActive)return;
            ClearJLTVVisuals();ClearVehicleDamageVisuals();Audio.StopVehicleAudio();var restore=driveReturnState;Drive=null;journeyNext=-1;buddyWheels.Clear();buddyBike=buddyGhost=buddyShadow=null;
            if(driveRoot!=null){driveRoot.gameObject.SetActive(false);Destroy(driveRoot.gameObject);}
            foreach(var mesh in driveMeshes)Destroy(mesh);driveMeshes.Clear();driveWheels.Clear();driveWheelSprites.Clear();driveMarks.Clear();driveBarriers.Clear();driveEnemies.Clear();driveWarnings.Clear();driveTraces.Clear();
            pursuitVisuals.Clear();
            driveRecoilRigs.Clear();
            foreach(var pair in driveVisibility)if(pair.Key!=null)pair.Key.SetActive(pair.Value);driveVisibility.Clear();
            Camera.transform.position=driveOldCamera;Camera.orthographicSize=driveOldSize;SetState(restore,playResultCue:false); // Restore the foot result; this is not another mission victory.
        }
        internal void TickDrive(float dt,SFCommand command)
        {
            if(!DriveActive)return;
            if(State==SFState.Playing&&Drive.Phase==SFDrivePhase.Complete&&(command.interact||command.jump)){FinishDrive();return;}
            Drive.SetViewport(Camera.aspect*5.625f);
            int impacts=Drive.Impacts,fired=Drive.Fired,hostile=Drive.HostileShots,missiles=Drive.MissileImpacts;Drive.Tick(dt,command,State!=SFState.Playing||LayoutBlocked);
            Audio.TickVehicleAudio(Drive,dt,State!=SFState.Playing||LayoutBlocked);
            if(Drive.Impacts>impacts)Audio.PlayAt("heavy",0,.5f);
            if(Drive.Fired>fired){Audio.PlayAt(Drive.JLTV?"rifle":Drive.LastShotWeapon==0?"pistol":"rifle",0,.55f);if(Drive.JLTV)Audio.PlayAt("heavy",0,.25f);}
            if(Drive.HostileShots>hostile)Audio.PlayAt("pistol",-5,.23f);
            if(Drive.MissileImpacts>missiles){Audio.PlayAt("heavy",Drive.LastMissileImpactX,.7f);Audio.PlayAt("shotgun",Drive.LastMissileImpactX,.38f);}
            RenderDrive();
        }
        internal void RenderDrive()
        {
            if(!DriveActive)return;
            Camera.orthographicSize=5.625f;Camera.transform.position=new Vector3(0,0,-10);
            float plateHeight=Mathf.Max(11.5f,Camera.aspect*11.25f/3f+.25f);
            driveDeparture.transform.localScale=Vector3.one*(plateHeight/driveDeparture.sprite.bounds.size.y);
            float travel=Mathf.Max(0,driveDeparture.sprite.bounds.size.x*driveDeparture.transform.localScale.x-Camera.aspect*11.25f);
            driveDeparture.transform.position=new Vector3(Mathf.Lerp(travel*.5f,-travel*.5f,Drive.Distance/SFDriveModel.Length),(plateHeight-11.25f)*.5f,0);
            // One continuous panorama is traversed; do not dissolve mismatched roads underneath vehicles.
            driveArrival.enabled=true;
            driveArrival.transform.localScale=Vector3.one*(plateHeight/driveArrival.sprite.bounds.size.y);
            // Course only: a 16:9 stop plate (desert arrival or plate_5_0) also scales to cover the full width of a wide view.
            if(CourseMode)driveArrival.transform.localScale=Vector3.one*Mathf.Max(plateHeight/driveArrival.sprite.bounds.size.y,Camera.aspect*11.25f/driveArrival.sprite.bounds.size.x);
            driveArrival.transform.position=new Vector3(0,(plateHeight-11.25f)*.5f,0);
            float arrivalAlpha=Drive.Phase==SFDrivePhase.Stopping?Mathf.SmoothStep(0,1,Drive.Clock/1.2f):Drive.Phase==SFDrivePhase.Dismounting||Drive.Phase==SFDrivePhase.Complete?1:0;
            driveArrival.color=new Color(driveTint.r,driveTint.g,driveTint.b,arrivalAlpha);
            var phase=Drive.Phase;float t=Drive.Clock;
            bool engaging=phase==SFDrivePhase.Driving&&(Drive.AimHoldTime>0||Drive.Target>=0&&Drive.Enemies[Drive.Target].Active&&!Drive.Enemies[Drive.Target].Resolved);
            Sprite a=engaging&&!Drive.JLTV?DriveGunPose(Drive.Weapon):DriveCruisePose,b=a;float blend=0;
            if(Drive.JLTV){a=b=DriveCruisePose;}
            else if(Drive.Ram){if(phase!=SFDrivePhase.Driving||Drive.Reloading)a=b=DriveCruisePose;}
            else if(phase==SFDrivePhase.Ready||phase==SFDrivePhase.Complete){a=boardArt.Frame(0);}
            else if(phase==SFDrivePhase.Boarding||phase==SFDrivePhase.Dismounting)
            {
                float q=phase==SFDrivePhase.Boarding?t:1.8f-t;
                if(q<.9f){a=boardArt.Frame(0);b=boardArt.Frame(1);blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.7f,q));}
                else{a=boardArt.Frame(1);b=driveArt.Frame(0);blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.1f,1.55f,q));}
            }
            else if(phase==SFDrivePhase.Driving&&Drive.Reloading)DriveReloadPose(out a,out b,out blend);
            if(Drive.Motorcycle)
            {
                MotorcyclePose(out int first,out int second,out blend);
                a=bikeArt.Frame(first);b=bikeArt.Frame(second);
                SetMotorcyclePose(driveTruck,first,1-blend);SetMotorcyclePose(driveGhost,second,blend);
                RenderBuddyBike(first,second,blend);
            }
            else
            {
                SetDriveTruck(driveTruck,a,1-blend);SetDriveTruck(driveGhost,b,blend);
                RenderDriveRecoil();if(Drive.JLTV)RenderJLTVVisuals();
                RenderDriveMuzzle();
            }
            for(int wi=0;wi<driveWheels.Count;wi++)
            {
                var wheel=driveWheels[wi];bool ghost=blend>=.5f;
                wheel.gameObject.SetActive(driveWheelSprites[wi]==(ghost?b:a)&&(phase==SFDrivePhase.Driving||phase==SFDrivePhase.Stopping||phase==SFDrivePhase.Recovery));
                wheel.SetParent(ghost?driveGhost.transform:driveTruck.transform,false);
                wheel.localRotation=Quaternion.Euler(0,0,Drive.WheelAngle);
                wheel.GetComponent<MeshRenderer>().sortingOrder=driveTruck.sortingOrder+2;
            }
            float baseY=DriveRoadY(Drive.Lane);
            driveShadow.transform.position=new Vector3(Drive.X,baseY+.05f,0);
            for(int i=0;i<driveBarriers.Count;i++)
            {
                var sr=driveBarriers[i];float dx=SFDriveModel.BarrierDistances[i]-Drive.Distance;
                sr.transform.position=new Vector3(SFDriveModel.PlayerX+dx,DriveRoadY(SFDriveModel.BarrierLanes[i]),0);
                sr.sortingOrder=20-Mathf.RoundToInt(SFDriveModel.BarrierLanes[i]*5);sr.enabled=Mathf.Abs(dx)<22;
                sr.color=Drive.Contacted.Contains(i)?new Color(.65f,.64f,.59f):Color.white;
            }
            RenderRoadMedian();
            for(int i=0;i<Drive.Enemies.Count;i++)
            {
                var e=Drive.Enemies[i];var sr=driveEnemies[i];sr.enabled=e.Active;
                sr.transform.position=new Vector3(e.X,DriveRoadY(e.Lane),0);

                sr.sortingOrder=20-e.Lane*5;
                RenderPursuitVisuals(i);
                var warning=driveWarnings[i];warning.enabled=e.Active&&!e.Resolved&&e.Warning&&phase==SFDrivePhase.Driving;
                warning.transform.position=new Vector3(e.LockedX,DriveRoadY(e.LockedLane)+.22f,0);warning.transform.localScale=new Vector3(4.3f,.85f,1);warning.color=new Color(1,.24f,.1f,.12f+.10f*Mathf.Sin(Drive.Clock*18));
            }
            for(int i=0;i<driveTraces.Count;i++)
            {
                var sr=driveTraces[i];sr.enabled=i<Drive.Shots.Count;if(!sr.enabled)continue;
                var shot=Drive.Shots[i];var from=shot.Hostile&&shot.SourceEnemy>=0?PursuitGunMuzzle(shot.SourceEnemy):shot.Hostile?new Vector3(shot.From.x,DriveRoadY(shot.From.y)+1.25f,0):DriveGunMuzzle(shot.Weapon,shot.From.y);var to=new Vector3(shot.To.x,DriveRoadY(shot.To.y)+1.05f,0);var delta=to-from;
                sr.transform.position=(from+to)*.5f;sr.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);sr.transform.localScale=new Vector3(delta.magnitude,.025f,1);sr.color=shot.Hostile?new Color(1,.36f,.2f,.9f):new Color(1,.85f,.35f,.95f);
            }
        }
        void SetDriveTruck(SpriteRenderer sr,Sprite sprite,float alpha)
        {
            sr.enabled=true;sr.sprite=sprite;sr.transform.localScale=Vector3.one*(5.2f/sprite.bounds.size.x);
            float sway=Drive.Speed>0?Mathf.Sin(Drive.Distance*2.3f)*.012f:0;
            sr.transform.position=new Vector3(Drive.X,DriveRoadY(Drive.Lane)+sway,0);sr.color=new Color(1,1,1,alpha);sr.sortingOrder=20-Mathf.RoundToInt(Drive.Lane*5)+(sr==driveGhost?1:0);
        }
    }
}
