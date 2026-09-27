using UnityEngine;
namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        Sprite vehiclePuff;
        Texture2D vehiclePuffTexture;
        internal int VehicleEffectSlots=>pursuitVisuals.Count*22;
        internal int VehicleBlastVisible(int index){int n=0;foreach(var s in pursuitVisuals[index].Fireball)if(s.enabled)n++;return n;}
        internal int VehicleScarsVisible(int index){int n=0;foreach(var s in pursuitVisuals[index].Scars)if(s.enabled)n++;return n;}
        internal bool VehicleImpactVisible(int index)=>pursuitVisuals[index].Impact.enabled;
        internal Vector3 VehicleBlastPosition(int index)=>pursuitVisuals[index].Fireball[0].transform.position;

        void CreateVehicleDamageVisuals(PursuitVisual v,SFConvoyEnemy e)
        {
            // One small, original procedural puff, shared by the bounded per-vehicle effect pools.
            // No source vehicle pixels are altered, and no objects are allocated on impact.
            if(vehiclePuff==null)
            {
                const int size=64;var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=(x+.5f)/size*2-1,w=(y+.5f)/size*2-1;
                    float noise=Mathf.PerlinNoise(x*.14f,y*.14f),radius=Mathf.Sqrt(u*u+w*w);
                    float a=Mathf.SmoothStep(0,1,Mathf.Clamp01((1-radius+(noise-.5f)*.2f)*2.4f));
                    pixels[y*size+x]=new Color(1,1,1,a*(.73f+noise*.27f));
                }
                vehiclePuffTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Original vehicle impact puff",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                vehiclePuffTexture.SetPixels(pixels);vehiclePuffTexture.Apply(false,true);
                vehiclePuff=Sprite.Create(vehiclePuffTexture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            }
            v.Scars=new SpriteRenderer[3];v.Fireball=new SpriteRenderer[6];v.Sparks=new SpriteRenderer[12];
            for(int i=0;i<3;i++)v.Scars[i]=DriveSprite(e.Name+" body scorch "+i,vehiclePuff,0);
            for(int i=0;i<6;i++)v.Fireball[i]=DriveSprite(e.Name+" blast plume "+i,vehiclePuff,0);
            for(int i=0;i<12;i++)v.Sparks[i]=DriveSprite(e.Name+" impact fragment "+i,Pixel,0);
            v.Impact=DriveSprite(e.Name+" impact core",vehiclePuff,0);
            foreach(var s in v.Smoke)s.sprite=vehiclePuff;
        }
        void ClearVehicleDamageVisuals()
        {
            if(vehiclePuff!=null)Destroy(vehiclePuff);if(vehiclePuffTexture!=null)Destroy(vehiclePuffTexture);
            vehiclePuff=null;vehiclePuffTexture=null;
        }
        void RenderVehicleDamage(PursuitVisual v,SFConvoyEnemy e,SpriteRenderer body,bool visible,float alpha,float width)
        {
            float wear=Mathf.Clamp01(1-e.Health/(e.Technical?160:100));
            float height=body.sprite.bounds.size.y*body.transform.localScale.y;
            Vector3 hit=body.transform.position+new Vector3(width*.18f,height*.47f,0);
            for(int i=0;i<v.Scars.Length;i++)
            {
                var s=v.Scars[i];s.enabled=visible&&wear>=(i+1)*.24f;
                s.transform.position=body.transform.position+new Vector3(width*(.22f-i*.20f),height*(i==1?.39f:.48f),0);
                s.transform.localScale=new Vector3(.48f+i*.09f,.24f+i*.05f,1);s.transform.localEulerAngles=new Vector3(0,0,i*27-15);
                s.sortingOrder=body.sortingOrder+2;s.color=new Color(.055f,.043f,.035f,alpha*.82f);
            }
            float impact=Mathf.Clamp01(e.MountedHitTime/SFDriveModel.MountedHitDuration);
            bool blast=e.MissileDestroyed&&e.BlastTime>0;
            float t=1-Mathf.Clamp01(e.BlastTime/SFDriveModel.BlastDuration);
            v.Impact.enabled=visible&&(blast&&t<.38f||!e.MissileDestroyed&&impact>0);
            v.Impact.transform.position=hit;v.Impact.transform.localScale=Vector3.one*(blast?.6f+t*2:.24f+(1-impact)*.40f);
            v.Impact.sortingOrder=body.sortingOrder+7;v.Impact.color=new Color(1,.96f,.74f,alpha*(blast?Mathf.Clamp01(1-t*2.65f):impact));
            for(int i=0;i<v.Fireball.Length;i++)
            {
                var s=v.Fireball[i];s.enabled=visible&&blast;
                float angle=i*2.39996f,spread=.15f+t*1.5f,size=(.5f+Mathf.Min(1,t*4)*1.2f)*(i%2==0?1:.68f);
                s.transform.position=hit+new Vector3(Mathf.Cos(angle)*spread,Mathf.Sin(angle)*spread*.6f+t*.75f,0);
                s.transform.localScale=new Vector3(size,size*(1+t*.3f),1);s.transform.localEulerAngles=new Vector3(0,0,i*53);
                s.sortingOrder=body.sortingOrder+3+i%2;
                Color c=Color.Lerp(new Color(1,i%2==0?.70f:.35f,.035f),new Color(.11f,.115f,.12f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.24f,.70f,t)));
                c.a=alpha*Mathf.Clamp01((1-t)*2)*(i%2==0?.95f:.8f);s.color=c;
            }
            for(int i=0;i<v.Sparks.Length;i++)
            {
                var s=v.Sparks[i];s.enabled=visible&&(blast&&t<.72f||!e.MissileDestroyed&&impact>0);
                float life=blast?t:1-impact,angle=i*2.39996f+.25f,distance=(blast?3.1f:.62f)*life;
                Vector3 offset=new Vector3(Mathf.Cos(angle)*distance,Mathf.Abs(Mathf.Sin(angle))*distance-life*life*(blast?2.1f:.4f),0);
                s.transform.position=hit+offset;s.transform.localEulerAngles=new Vector3(0,0,angle*Mathf.Rad2Deg-life*220);
                s.transform.localScale=new Vector3(blast?.06f:.1f,blast?.035f:.018f,1);s.sortingOrder=body.sortingOrder+6;
                s.color=Color.Lerp(new Color(1,.84f,.3f,alpha),new Color(.20f,.15f,.10f,0),life/(blast?.72f:1));
            }
        }

        internal int RoadMedianPieces=>driveMarks.Count;
        internal Vector3 RoadMedianPosition=>driveMarks.Count>0?driveMarks[1].transform.position:Vector3.zero;
        void CreateRoadMedian()
        {
            // Far edge of the three lanes; decorative divider never becomes an invisible obstacle.
            for(int i=0;i<36;i++)
            {
                var s=DriveSprite(i<12?"Median rail":i<24?"Median post":"Median amber reflector",Pixel,-8);
                s.transform.localScale=i<12?new Vector3(4.02f,.11f,1):i<24?new Vector3(.075f,.48f,1):new Vector3(.15f,.08f,1);
                s.color=i<12?new Color(.49f,.52f,.47f):i<24?new Color(.25f,.29f,.27f):new Color(1,.73f,.20f);
                driveMarks.Add(s);
            }
        }
        void RenderRoadMedian()
        {
            bool moving=Drive.Phase==SFDrivePhase.Driving||Drive.Phase==SFDrivePhase.Recovery||Drive.Phase==SFDrivePhase.Stopping;
            float fade=Drive.Phase==SFDrivePhase.Stopping?1-Mathf.Clamp01(Drive.Clock):1;
            for(int i=0;i<driveMarks.Count;i++)
            {
                var s=driveMarks[i];s.enabled=moving;
                s.transform.position=new Vector3(Mathf.Repeat((i%12)*4-Drive.Distance+24,48)-24,DriveRoadY(1)+1.62f+(i<12?.21f:i<24?0:.23f),0);
                Color c=s.color;c.a=fade;s.color=c;
            }
        }
    }
}
