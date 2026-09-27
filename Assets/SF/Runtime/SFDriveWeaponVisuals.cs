using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        SFArt drivePistolReload,driveRifleReload;
        SpriteRenderer driveMuzzleGlow,driveMuzzleCore;
        bool DriveReloadArtReady=>drivePistolReload!=null&&driveRifleReload!=null&&drivePistolReload.frames.Length==2&&driveRifleReload.frames.Length==2;
        internal Sprite DriveVisiblePose=>driveGhost.color.a>=.5f?driveGhost.sprite:driveTruck.sprite;
        internal bool DriveMuzzleVisible=>driveMuzzleCore!=null&&driveMuzzleCore.enabled;
        internal Vector3 DriveMuzzlePosition=>driveMuzzleCore.transform.position;
        internal int DriveVisibleWheels=>driveWheels.FindAll(w=>w.gameObject.activeSelf).Count;
        void LoadDriveWeaponArt()
        {
            drivePistolReload=Resources.Load<SFArt>("SF/vehicle_pickup_reload_pistol");
            driveRifleReload=Resources.Load<SFArt>("SF/vehicle_pickup_reload_rifle");
        }
        void CreateDriveWeaponVisuals()
        {
            if(!Drive.Ram&&!Drive.JLTV)foreach(var art in new[]{drivePistolReload,driveRifleReload})foreach(var pose in art.frames)
            {MakeDriveWheel(pose,.183f,.187f,.045f);MakeDriveWheel(pose,.823f,.187f,.045f);}
            driveMuzzleGlow=DriveSprite("Pickup muzzle glow",Ellipse,61);
            driveMuzzleCore=DriveSprite("Pickup muzzle core",Ellipse,62);
        }
        void DriveReloadPose(out Sprite a,out Sprite b,out float blend)
        {
            var art=Drive.Weapon==0?drivePistolReload:driveRifleReload;
            float p=Drive.ReloadProgress;
            if(p<.12f){a=gunArt.Frame(Drive.Weapon);b=art.Frame(0);blend=Mathf.SmoothStep(0,1,p/.12f);}
            else if(p<.54f){a=art.Frame(0);b=art.Frame(1);blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46f,.54f,p));}
            else{a=art.Frame(1);b=gunArt.Frame(Drive.Weapon);blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,p));}
        }
        Vector3 DriveGunMuzzle(int weapon,float lane)
        {
            if(Drive.JLTV)return JLTVCannonMuzzle(weapon);
            // Authored muzzle coordinates in the accepted, tightly bounded side-view poses.
            float x=Drive.Ram?(weapon==0?.275f:.146f):(weapon==0?.272f:.262f),y=Drive.Ram?(weapon==0?.78f:.825f):(weapon==0?.783f:.780f);
            return DriveRecoilPoint(weapon,new Vector2(x,y))+Vector3.up*(DriveRoadY(lane)-DriveRoadY(Drive.Lane));
        }
        void RenderDriveMuzzle()
        {
            bool visible=Drive.Phase==SFDrivePhase.Driving&&!Drive.Reloading&&Drive.MuzzleTime>0&&Drive.LastShotWeapon==Drive.Weapon;
            driveMuzzleGlow.enabled=driveMuzzleCore.enabled=visible;if(!visible)return;
            float size=Drive.LastShotWeapon==0?.19f:.28f,fade=Mathf.Clamp01(Drive.MuzzleTime/.07f);
            Vector3 muzzle=DriveGunMuzzle(Drive.LastShotWeapon,Drive.Lane);
            driveMuzzleCore.transform.position=muzzle+(Drive.JLTV?JLTVGunDirection():Vector3.left)*size*.22f;
            driveMuzzleGlow.transform.position=driveMuzzleCore.transform.position;
            driveMuzzleCore.transform.localScale=new Vector3(size,size*.31f,1);
            driveMuzzleGlow.transform.localScale=new Vector3(size*1.65f,size*.8f,1);
            driveMuzzleCore.color=new Color(1,.94f,.72f,fade);driveMuzzleGlow.color=new Color(1,.61f,.2f,fade*.43f);
        }
    }
}
