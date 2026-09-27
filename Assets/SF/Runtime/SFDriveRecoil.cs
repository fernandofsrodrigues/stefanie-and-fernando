using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        sealed class DriveRecoilRig
        {
            public Sprite Sprite;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public Vector3[] Rest,Vertices,Offsets;
            public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();
            public float Amount=-1;
        }
        readonly List<DriveRecoilRig> driveRecoilRigs=new List<DriveRecoilRig>();
        internal int DriveRecoilRigCount=>driveRecoilRigs.Count;
        internal int DriveRecoilVisibleCount=>driveRecoilRigs.FindAll(r=>r.Renderer.enabled).Count;
        static float RecoilBand(float value,float low,float enter,float leave,float high)
        {return Mathf.SmoothStep(0,1,Mathf.InverseLerp(low,enter,value))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(leave,high,value)));}
        // A small, non-destructive puppet deformation of the accepted aim art. The lower
        // truck, wheels, driver and roof are outside the weighted area.
        // Values are in normalized tightly bounded sprite space, origin bottom-left.
        internal static Vector2 DriveGunnerOffset(int weapon,Vector2 p)
        {
            float torso=RecoilBand(p.x,.338f,.362f,.426f,.454f)*RecoilBand(p.y,.674f,.727f,.855f,.897f);
            float hands=RecoilBand(p.x,.244f,.258f,.345f,.390f)*RecoilBand(p.y,weapon==0?.721f:.672f,weapon==0?.758f:.725f,.795f,.831f);
            float weight=Mathf.Max(torso*.38f,hands);
            return new Vector2(weapon==0?.042f:.060f,weapon==0?.028f:.019f)*weight;
        }
        void CreateDriveRecoilVisuals()
        {
            const int columns=128,rows=64;
            for(int weapon=0;weapon<2;weapon++)
            {
                var sprite=DriveGunPose(weapon);var rig=new DriveRecoilRig{Sprite=sprite,Mesh=new Mesh{name="Pickup gunner recoil "+weapon}};
                rig.Mesh.MarkDynamic();driveMeshes.Add(rig.Mesh);int count=(columns+1)*(rows+1);
                rig.Rest=new Vector3[count];rig.Vertices=new Vector3[count];rig.Offsets=new Vector3[count];var uv=new Vector2[count];var colors=new Color[count];var tris=new int[columns*rows*6];
                for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
                {
                    int i=y*(columns+1)+x;var p=new Vector2((float)x/columns,(float)y/rows);
                    var v=new Vector2(sprite.bounds.min.x+p.x*sprite.bounds.size.x,sprite.bounds.min.y+p.y*sprite.bounds.size.y);
                    rig.Rest[i]=rig.Vertices[i]=v;rig.Offsets[i]=CurrentGunnerOffset(weapon,p)*(sprite.bounds.size.x/5.2f);
                    var pixel=v*sprite.pixelsPerUnit+sprite.pivot;
                    uv[i]=new Vector2((sprite.rect.x+pixel.x)/sprite.texture.width,(sprite.rect.y+pixel.y)/sprite.texture.height);colors[i]=Color.white;
                    if(x==columns||y==rows)continue;int t=(y*columns+x)*6;
                    tris[t]=i;tris[t+1]=i+columns+1;tris[t+2]=i+1;tris[t+3]=i+1;tris[t+4]=i+columns+1;tris[t+5]=i+columns+2;
                }
                rig.Mesh.vertices=rig.Vertices;rig.Mesh.uv=uv;rig.Mesh.colors=colors;rig.Mesh.triangles=tris;rig.Mesh.RecalculateBounds();
                var go=new GameObject("Isolated gunner recoil "+weapon);go.transform.SetParent(driveRoot,false);go.AddComponent<MeshFilter>().sharedMesh=rig.Mesh;
                rig.Renderer=go.AddComponent<MeshRenderer>();rig.Renderer.sharedMaterial=Unlit;rig.Renderer.enabled=false;
                rig.Properties.SetTexture("_MainTex",sprite.texture);rig.Properties.SetColor("_RendererColor",Color.white);rig.Renderer.SetPropertyBlock(rig.Properties);
                driveRecoilRigs.Add(rig);
            }
        }
        void RenderDriveRecoil()
        {
            if(Drive.JLTV)return;
            for(int weapon=0;weapon<driveRecoilRigs.Count;weapon++)
            {
                var rig=driveRecoilRigs[weapon];SpriteRenderer owner=null;
                if(Drive.Phase==SFDrivePhase.Driving&&!Drive.Reloading&&Drive.RecoilAmount>0&&Drive.LastShotWeapon==weapon)
                {
                    if(driveTruck.sprite==rig.Sprite&&driveTruck.color.a>.99f)owner=driveTruck;
                    else if(driveGhost.sprite==rig.Sprite&&driveGhost.color.a>.99f)owner=driveGhost;
                }
                rig.Renderer.enabled=owner!=null;if(owner==null)continue;
                owner.enabled=false;rig.Renderer.transform.SetPositionAndRotation(owner.transform.position,owner.transform.rotation);
                rig.Renderer.transform.localScale=owner.transform.localScale;rig.Renderer.sortingOrder=owner.sortingOrder;
                float amount=Drive.RecoilAmount;if(Mathf.Approximately(amount,rig.Amount))continue;rig.Amount=amount;
                for(int i=0;i<rig.Vertices.Length;i++)rig.Vertices[i]=rig.Rest[i]+rig.Offsets[i]*amount;
                rig.Mesh.vertices=rig.Vertices;
            }
        }
        internal Vector3 DriveRecoilPoint(int weapon,Vector2 p)
        {
            var sprite=DriveGunPose(weapon);float sway=Drive.Speed>0?Mathf.Sin(Drive.Distance*2.3f)*.012f:0;
            Vector2 offset=Drive.Phase==SFDrivePhase.Driving&&!Drive.Reloading&&Drive.LastShotWeapon==weapon?CurrentGunnerOffset(weapon,p)*Drive.RecoilAmount:Vector2.zero;
            return new Vector3(Drive.X+(p.x-.5f)*5.2f+offset.x,DriveRoadY(Drive.Lane)+p.y*sprite.bounds.size.y*5.2f/sprite.bounds.size.x+sway+offset.y,0);
        }
        internal Vector3 DriveRecoilMeshDisplacement(int weapon,int index)
        {var rig=driveRecoilRigs[weapon];return (rig.Vertices[index]-rig.Rest[index])*(5.2f/rig.Sprite.bounds.size.x);}
    }
}
