using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        SFArt jltvBodyArt,jltvTurretArt,technicalArt;
        Sprite jltvPivotSprite;
        SpriteRenderer jltvTurret;
        readonly List<SpriteRenderer> jltvMissiles=new List<SpriteRenderer>();
        internal bool JLTVTurretVisible=>jltvTurret!=null&&jltvTurret.enabled;
        internal Vector3 JLTVMount=>driveTruck.transform.position+new Vector3(-.19f,1.92f,0);
        internal float JLTVTurretScaleX=>jltvTurret.transform.localScale.x;
        internal bool LoadJLTVArt()
        {
            jltvBodyArt=Resources.Load<SFArt>("SF/vehicle_jltv_body");
            jltvTurretArt=Resources.Load<SFArt>("SF/vehicle_jltv_turret");
            technicalArt=Resources.Load<SFArt>("SF/vehicle_technical");
            if(jltvBodyArt==null||jltvTurretArt==null||technicalArt==null||technicalArt.frames.Length!=2){Notify("JLTV ART UNAVAILABLE");return false;}
            return true;
        }
        void CreateJLTVVisuals()
        {
            var source=jltvTurretArt.Frame(0);
            jltvPivotSprite=Sprite.Create(source.texture,source.rect,new Vector2(.30f,0),source.pixelsPerUnit,0,SpriteMeshType.FullRect);
            jltvTurret=DriveSprite("Independent JLTV turret",jltvPivotSprite,25);
            MakeDriveWheel(jltvBodyArt.Frame(0),.209f,.215f,.043f);
            MakeDriveWheel(jltvBodyArt.Frame(0),.831f,.211f,.043f);
            for(int i=0;i<4;i++)jltvMissiles.Add(DriveSprite("Arcade guided projectile "+i,Pixel,60));
        }
        Vector3 JLTVGunDirection()=>jltvTurret.transform.TransformVector(Vector3.right).normalized;
        Vector3 JLTVCannonMuzzle(int weapon)=>jltvTurret.transform.TransformPoint(new Vector3(weapon==0?jltvPivotSprite.bounds.max.x:jltvPivotSprite.bounds.size.x*.095f,jltvPivotSprite.bounds.size.y*(weapon==0?.51f:.80f),0));
        void RenderJLTVVisuals()
        {
            float yaw=Mathf.Cos(Drive.TurretYaw*Mathf.Deg2Rad),scale=2.475f/jltvPivotSprite.bounds.size.x;
            jltvTurret.enabled=true;jltvTurret.sortingOrder=driveTruck.sortingOrder+3;
            jltvTurret.transform.localScale=new Vector3(scale*yaw,scale,1);
            jltvTurret.transform.localRotation=Quaternion.Euler(0,0,Drive.TurretPitch*(yaw<0?-1:1));
            jltvTurret.transform.position=JLTVMount-JLTVGunDirection()*Drive.RecoilAmount*.035f;
            for(int i=0;i<jltvMissiles.Count;i++)
            {
                var sr=jltvMissiles[i];sr.enabled=i<Drive.Projectiles.Count;if(!sr.enabled)continue;
                var p=Drive.Projectiles[i];float t=Mathf.Clamp01(p.Age/p.Duration);
                Vector3 from=new Vector3(p.From.x,DriveRoadY(p.From.y)+2.4f,0),to=new Vector3(p.To.x,DriveRoadY(p.To.y)+1.3f,0),delta=to-from;
                sr.transform.position=Vector3.Lerp(from,to,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.25f);
                sr.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                sr.transform.localScale=new Vector3(.34f,.06f,1);sr.color=new Color(1,.73f,.26f);
            }
        }
        void ClearJLTVVisuals()
        {
            if(jltvPivotSprite!=null)Destroy(jltvPivotSprite);
            jltvPivotSprite=null;jltvTurret=null;jltvMissiles.Clear();
        }
    }
}
