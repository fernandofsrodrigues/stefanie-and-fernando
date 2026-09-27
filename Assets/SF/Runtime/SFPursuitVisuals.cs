using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        sealed class PursuitVisual
        {
            public MeshRenderer[] Wheels;
            public SpriteRenderer Shadow,WarningBack,WarningFill,Muzzle;
            public SpriteRenderer[] Smoke;
            public SpriteRenderer[] Scars,Fireball,Sparks;
            public SpriteRenderer Impact;
            public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();
        }
        readonly List<PursuitVisual> pursuitVisuals=new List<PursuitVisual>();
        internal int PursuitVisualCount=>pursuitVisuals.Count;
        internal int PursuitMeshCount=>pursuitVisuals.Count*2;
        internal int PursuitVisibleCount{get{int n=0;foreach(var body in driveEnemies)if(body.enabled)n++;return n;}}
        internal bool PursuitWarningVisible(int i)=>pursuitVisuals[i].WarningFill.enabled;
        internal bool PursuitMuzzleVisible(int i)=>pursuitVisuals[i].Muzzle.enabled;
        internal float PursuitWheelAngle(int i)=>pursuitVisuals[i].Wheels[0].transform.localEulerAngles.z;
        internal Vector3 PursuitWheelPosition(int i,int wheel)=>pursuitVisuals[i].Wheels[wheel].transform.position;
        internal int PursuitSmokeVisible(int i){int n=0;foreach(var s in pursuitVisuals[i].Smoke)if(s.enabled)n++;return n;}
        internal Color PursuitBodyColor(int i)=>driveEnemies[i].color;

        void CreatePursuitVisuals(SpriteRenderer owner,SFConvoyEnemy e)
        {
            bool sedan=e.Id%2==0;
            // Normalized centers and rim radii measured in the unchanged imported source bounds.
            float rear=sedan?.208f:.191f,front=sedan?.806f:.838f,y=sedan?.2185f:.160f,r=sedan?.047f:.0355f;
            if(e.Technical){rear=e.FromAhead?.254f:.227f;front=.829f;y=.164f;r=.036f;}
            var v=new PursuitVisual{
                Wheels=new[]{CreateDriveWheel(owner,owner.sprite,rear,y,r,e.Name+" rear rim"),CreateDriveWheel(owner,owner.sprite,front,y,r,e.Name+" front rim")},
                Shadow=DriveSprite(e.Name+" contact",Ellipse,0),
                WarningBack=DriveSprite(e.Name+" attack timer back",Pixel,0),
                WarningFill=DriveSprite(e.Name+" attack timer",Pixel,0),
                Muzzle=DriveSprite(e.Name+" muzzle",Ellipse,0),
                Smoke=new SpriteRenderer[3]
            };
            for(int n=0;n<v.Smoke.Length;n++)v.Smoke[n]=DriveSprite(e.Name+" engine smoke "+n,Ellipse,0);
            CreateVehicleDamageVisuals(v,e);
            pursuitVisuals.Add(v);
        }
        internal Vector3 PursuitGunMuzzle(int i)
        {
            var sr=driveEnemies[i];bool sedan=Drive.Enemies[i].Id%2==0;
            var bounds=sr.sprite.bounds;
            var e=Drive.Enemies[i];
            float x=e.Technical?(e.FromAhead?.008f:.474f):(sedan?.591f:.726f),y=e.Technical?.90f:(sedan?.758f:.629f);
            return sr.transform.TransformPoint(new Vector3(bounds.min.x+bounds.size.x*x,bounds.min.y+bounds.size.y*y,0));
        }
        void RenderPursuitVisuals(int i)
        {
            var e=Drive.Enemies[i];var body=driveEnemies[i];var v=pursuitVisuals[i];
            bool visible=e.Active&&e.X>-20&&e.X<24&&(Drive.Phase==SFDrivePhase.Driving||Drive.Phase==SFDrivePhase.Stopping||Drive.Phase==SFDrivePhase.Recovery);
            bool fighting=Drive.Phase==SFDrivePhase.Driving;
            float alpha=Mathf.Clamp01((e.X+20)/3),wear=Mathf.Clamp01(1-e.Health/(e.Technical?160:100));
            // Arrival dissolves to a different location; any late straggler must leave with
            // the forest plate instead of remaining clipped at the safe overlook.
            if(Drive.Phase==SFDrivePhase.Stopping)alpha*=1-Mathf.Clamp01(Drive.Clock);
            Color tint=e.Flash>0&&!e.Resolved?new Color(1,.65f,.3f,alpha):Color.Lerp(Color.white,new Color(.62f,.62f,.60f),wear*.65f);
            tint.a=alpha;body.color=tint;body.enabled=visible;
            foreach(var wheel in v.Wheels)
            {
                wheel.enabled=visible;wheel.sortingOrder=body.sortingOrder+1;
                wheel.transform.localRotation=Quaternion.Euler(0,0,e.WheelAngle);
                wheel.GetPropertyBlock(v.Properties);v.Properties.SetColor("_RendererColor",tint);wheel.SetPropertyBlock(v.Properties);
            }
            float width=e.Technical?4.3f:e.Id%2==0?4.1f:5.1f;
            v.Shadow.enabled=visible;v.Shadow.sortingOrder=body.sortingOrder-1;
            v.Shadow.transform.position=new Vector3(e.X,DriveRoadY(e.Lane)+.04f,0);
            v.Shadow.transform.localScale=new Vector3(width*.93f,.16f,1);v.Shadow.color=new Color(0,0,0,.25f*alpha);
            bool warning=visible&&fighting&&!e.Resolved&&e.Warning;
            v.WarningBack.enabled=v.WarningFill.enabled=warning;
            if(warning)
            {
                float progress=Mathf.Clamp01(1-e.AttackClock/1.15f),barWidth=.92f;
                Vector3 center=body.transform.position+Vector3.up*(body.sprite.bounds.size.y*body.transform.localScale.y+.18f);
                v.WarningBack.transform.position=center;v.WarningBack.transform.localScale=new Vector3(barWidth+.06f,.12f,1);
                v.WarningBack.color=new Color(.12f,.08f,.05f,.9f);v.WarningBack.sortingOrder=55;
                v.WarningFill.transform.position=center+Vector3.left*(barWidth*(1-progress)*.5f);
                v.WarningFill.transform.localScale=new Vector3(barWidth*progress,.075f,1);
                v.WarningFill.color=progress>.72f?new Color(1,.27f,.12f):new Color(1,.75f,.18f);v.WarningFill.sortingOrder=56;
            }
            v.Muzzle.enabled=visible&&fighting&&!e.Resolved&&e.MuzzleTime>0;
            if(v.Muzzle.enabled)
            {
                v.Muzzle.transform.position=PursuitGunMuzzle(i)+(e.FromAhead?Vector3.left:Vector3.right)*.025f;
                v.Muzzle.transform.localScale=new Vector3(.19f,.065f,1);v.Muzzle.sortingOrder=body.sortingOrder+2;
                v.Muzzle.color=new Color(1,.76f,.4f,Mathf.Clamp01(e.MuzzleTime/.09f));
            }
            float damage=Mathf.Clamp01(((e.Technical?120:70)-e.Health)/(e.Technical?120:70));
            for(int n=0;n<v.Smoke.Length;n++)
            {
                var smoke=v.Smoke[n];smoke.enabled=visible&&damage>0;
                if(!smoke.enabled)continue;
                float life=Mathf.Repeat((e.Age+e.RetreatTime)*.85f+n/3f,1),size=Mathf.Lerp(.09f,.33f,life);
                smoke.transform.position=body.transform.position+new Vector3(width*.35f-life*.30f,body.sprite.bounds.size.y*body.transform.localScale.y*.51f+life*.6f,0);
                smoke.transform.localScale=new Vector3(size*1.25f,size,1);smoke.sortingOrder=body.sortingOrder+2;
                smoke.color=new Color(.19f,.21f,.22f,Mathf.Sin(life*Mathf.PI)*damage*.32f*alpha);
            }
            RenderVehicleDamage(v,e,body,visible,alpha,width);
        }
    }
}
