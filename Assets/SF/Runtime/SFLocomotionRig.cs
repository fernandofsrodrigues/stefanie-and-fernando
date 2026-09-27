using UnityEngine;
using UnityEngine.Rendering;

namespace StefanieAndFernando
{
    // Inner and outer legs use separate artwork and poses. Phase is travelled distance,
    // so a blocked actor cannot walk in place. The far leg stays behind the near leg throughout.
    public sealed class SFLocomotionRig : MonoBehaviour
    {
        public struct Pose { public Vector2 hip,knee,ankle,contact; public bool planted; public float footAngle,clearance; }
        public static readonly Vector2 Heel=new Vector2(-.17f,-.215f),Toe=new Vector2(.40f,-.215f);
        public static Vector2 FootPoint(Pose pose,Vector2 ankleRelative)=>pose.ankle+Rotate(ankleRelative,pose.footAngle*Mathf.Deg2Rad);
        public static float CycleLength(bool run)=>CycleLength(run?1f:0f);
        public static float CycleLength(float runWeight)=>Mathf.Lerp(1.8f,2.8f,runWeight);
        public static Pose Solve(float phase,bool run)=>Solve(phase,run?1f:0f);
        public static Pose Solve(float phase,float runWeight)
        {
            runWeight=Mathf.Clamp01(runWeight);
            phase=Mathf.Repeat(phase,1);float stance=Mathf.Lerp(.60f,.40f,runWeight);
            float span=CycleLength(runWeight)*stance;
            bool planted=phase<stance;
            float swing=Mathf.Clamp01((phase-stance)/(1-stance));
            float x=planted?span*(.5f-phase/stance):span*(-.5f+swing);
            float lift=planted?0:Mathf.Sin(swing*Mathf.PI)*Mathf.Lerp(.18f,.42f,runWeight);
            // Rock onto the heel, settle flat, then leave from the toe. The contact anchor
            // follows -body travel; rotation moves the ankle around that fixed ground point.
            float heelAngle=Mathf.Lerp(14,12,runWeight),toeAngle=Mathf.Lerp(20,26,runWeight);
            float angle;
            if(planted)
            {
                float t=phase/stance;
                angle=t<.18f?heelAngle*(1-Mathf.SmoothStep(0,1,t/.18f)):
                    t>.76f?-toeAngle*Mathf.SmoothStep(0,1,(t-.76f)/.24f):0;
            }
            else angle=Mathf.Lerp(-toeAngle,heelAngle,Mathf.SmoothStep(0,1,swing));
            Vector2 pivot=angle>=0?Heel:Toe;
            Vector2 contact=new Vector2(x+pivot.x,lift);
            Vector2 ankle=contact-Rotate(pivot,angle*Mathf.Deg2Rad);
            // Lower the hips at contact, rise through passing; running has a short flight phase.
            float bob=Mathf.Lerp(.035f,.065f,runWeight)*Mathf.Sin(phase*Mathf.PI*4);
            Vector2 hip=new Vector2(0,Mathf.Lerp(1.28f,1.23f,runWeight)+bob);
            const float upper=.60f,lower=.64f;
            Vector2 d=ankle-hip;float length=Mathf.Clamp(d.magnitude,.03f,upper+lower-.001f);
            Vector2 axis=d.normalized;float along=(upper*upper-lower*lower+length*length)/(2*length);
            float bend=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            Vector2 knee=hip+axis*along+new Vector2(-axis.y,axis.x)*bend;
            return new Pose{hip=hip,knee=knee,ankle=ankle,contact=contact,planted=planted,footAngle=angle,clearance=lift};
        }
        public static float BodyLean(float phase,bool run,bool aimed,bool backward)=>BodyLean(phase,run?1f:0f,aimed,backward);
        public static float BodyLean(float phase,float runWeight,bool aimed,bool backward)
        {
            float sway=Mathf.Sin(phase*Mathf.PI*4);
            if(aimed)return sway*.28f;
            return (backward?1:-1)*Mathf.Lerp(1.2f,4,runWeight)+sway*Mathf.Lerp(.55f,1.2f,runWeight);
        }
        public Pose Near {get;private set;} public Pose Far {get;private set;}
        public float Phase {get;private set;} public bool Running {get;private set;}
        public int BodyMode {get;private set;} // 0 rifle, 1 unarmed, 2 pistol
        public bool Civilian {get;private set;}
        public bool Aimed {get;private set;} public bool Backpedaling {get;private set;}
        public Sprite UpperSprite {get;private set;}
        public Sprite NearLegSprite {get;private set;} public Sprite FarLegSprite {get;private set;}
        public float BodyLeanDegrees {get;private set;}
        public float RunWeight {get;private set;}
        public Vector3 BodyPivotWorld=>body.renderer.transform.TransformPoint(new Vector3(bodies[activeBody].hipX,bodies[activeBody].hipHeight,0));
        SFActor actor;Transform root;Part near,far,body;Body[] bodies;bool initialized;SortingGroup group;int activeBody=-1;
        sealed class Body {public Mesh mesh;public Sprite sprite;public Color[] colors;public float[] alpha;public MaterialPropertyBlock properties;public float hipX,hipHeight;}
        float travelledPhase,lastDistance=float.NaN;
        readonly SFGaitBlend gaitBlend=new SFGaitBlend();
        readonly Vector2 restHip=new Vector2(0,1.455f),restKnee=new Vector2(0,.855f),restAnkle=new Vector2(0,.215f);
        sealed class Part { public Mesh mesh;public MeshRenderer renderer;public MeshFilter filter;public Vector3[] rest,vertices;public Color[] colors; }

        public bool Initialize(SFActor owner,SFArt torsos,SFArt legs,bool civilian=false,SFArt sidearm=null)
        {
            if(torsos==null||torsos.frames.Length<(civilian?2:5)||legs==null||legs.frames.Length<2)return false;
            var actions=civilian?null:Resources.Load<SFArt>("SF/"+owner.identity+"_field_actions");
            var aimedRifle=civilian?null:Resources.Load<SFArt>("SF/"+owner.identity+"_field_rifle");
            if(civilian?(sidearm==null||sidearm.frames.Length<3):(actions==null||actions.frames.Length<8||aimedRifle==null||aimedRifle.frames.Length<6))return false;
            Civilian=civilian;
            var innerLegs=Resources.Load<SFArt>("SF/"+(civilian?"civil":"field")+"_inner_leg_parts");
            if(innerLegs==null||innerLegs.frames.Length<2)return false;
            actor=owner;root=transform;root.SetParent(actor.transform,false);
            group=gameObject.AddComponent<SortingGroup>();
            bool woman=actor.identity=="stefanie";
            var leg=legs.Frame(woman?1:0);
            var innerLeg=innerLegs.Frame(woman?1:0);NearLegSprite=leg;FarLegSprite=innerLeg;
            far=MakePart("Far leg",innerLeg);near=MakePart("Near leg",leg);
            BuildLeg(far,innerLeg,woman,true);BuildLeg(near,leg,woman,false);
            body=MakePart("Original upper body",torsos.Frame(0));
            if(civilian)bodies=new[]{BuildBody(torsos.Frame(0),3,woman),BuildBody(torsos.Frame(1),4,woman),
                BuildBody(sidearm.Frame(1),5,woman),BuildBody(sidearm.Frame(2),5,woman)};
            else bodies=new[]{BuildBody(torsos.Frame(0),0,woman),BuildBody(torsos.Frame(4),0,woman),
                BuildBody(actions.Frame(0),1,woman),BuildBody(actions.Frame(4),1,woman),
                BuildBody(actions.Frame(3),2,woman),BuildBody(actions.Frame(7),2,woman),
                BuildBody(aimedRifle.Frame(1),6,woman),BuildBody(aimedRifle.Frame(5),6,woman)};
            initialized=true;return true;
        }
        Part MakePart(string label,Sprite sprite)
        {
            var go=new GameObject(label);go.transform.SetParent(root,false);
            var part=new Part{renderer=go.AddComponent<MeshRenderer>(),mesh=new Mesh{name=label}};
            part.filter=go.AddComponent<MeshFilter>();part.filter.sharedMesh=part.mesh;part.renderer.sharedMaterial=actor.game.Unlit;
            var props=new MaterialPropertyBlock();props.SetTexture("_MainTex",sprite.texture);props.SetColor("_RendererColor",Color.white);
            part.renderer.SetPropertyBlock(props);return part;
        }
        Body BuildBody(Sprite sprite,int mode,bool woman)
        {
            var b=sprite.bounds;
            // Follow the belt line, retaining the hanging rear holster instead of slicing it off.
            float[] xs={b.min.x,-.53f,-.28f,-.27f,.10f,.28f,b.max.x};
            float[] ys={1.50f,1.19f,1.19f,1.43f,1.43f,1.46f,1.40f};
            float hipX=woman?-.27f:-.18f,hipHeight=1.36f;
            if(mode==1)
            {
                xs=new[]{b.min.x,-.61f,-.32f,-.31f,.12f,.34f,b.max.x};
                ys=new[]{1.54f,1.20f,1.20f,1.42f,1.42f,1.42f,1.42f};
                hipX=woman?-.10f:-.16f;hipHeight=1.40f;
            }
            else if(mode==2)
            {
                xs=new[]{b.min.x,-.54f,-.30f,-.29f,.18f,.40f,b.max.x};
                ys=new[]{1.56f,1.22f,1.22f,1.43f,1.43f,1.43f,1.43f};
                hipX=woman?-.06f:0;hipHeight=1.41f;
            }
            else if(mode==3)
            {
                // Preserve the hanging satchel and the low-ready muzzle, while removing both
                // original legs. Only geometry changes: face, hands and rifle UVs stay original.
                xs=new[]{b.min.x,-.38f,-.16f,-.15f,.43f,.49f,b.max.x};
                ys=new[]{1.48f,1.36f,1.30f,1.30f,1.30f,1.10f,1.10f};
                hipX=.12f;hipHeight=1.40f;
            }
            else if(mode==4)
            {
                xs=new[]{b.min.x,-.39f,-.18f,-.17f,.32f,.48f,b.max.x};
                ys=new[]{1.50f,1.33f,1.29f,1.30f,1.30f,1.35f,1.45f};
                hipX=.08f;hipHeight=1.40f;
            }
            else if(mode==5)
            {
                xs=new[]{b.min.x,-.65f,-.37f,-.36f,.12f,.34f,b.max.x};
                // Her satchel ends above the exposed thigh; keep the cut there instead of
                // carrying a stationary rectangle of skin along with the upper body.
                float bagFloor=woman?1.13f:1.02f;
                ys=new[]{1.54f,bagFloor,bagFloor,1.33f,1.33f,1.40f,1.48f};
                hipX=woman?-.03f:-.06f;hipHeight=1.40f;
            }
            else if(mode==6)
            {
                xs=new[]{b.min.x,-.60f,-.34f,-.33f,.10f,.28f,b.max.x};
                ys=new[]{1.54f,1.22f,1.22f,1.44f,1.44f,1.46f,1.48f};
                hipX=woman?-.23f:-.25f;hipHeight=1.40f;
            }
            float blend=mode==0?0:mode>=3&&mode<=5?.20f:.13f;
            var v=new Vector3[xs.Length*3];var uv=new Vector2[v.Length];var colors=new Color[v.Length];var alpha=new float[v.Length];var tris=new int[(xs.Length-1)*12];
            for(int i=0;i<xs.Length;i++)
            {
                // Blend only the cloth seam. Muzzles and hanging holsters stay fully opaque.
                float edgeBlend=(mode==3&&i>=5)||((mode==1||mode==2||mode==5||mode==6)&&(i==1||i==2))?0:blend;
                int n=i*3;v[n]=new Vector3(xs[i],ys[i]);v[n+1]=new Vector3(xs[i],ys[i]+edgeBlend);v[n+2]=new Vector3(xs[i],b.max.y);
                for(int j=0;j<3;j++){uv[n+j]=UV(sprite,v[n+j]);colors[n+j]=Color.white;alpha[n+j]=j==0&&edgeBlend>0?0:1;}
                if(i<xs.Length-1)for(int j=0;j<2;j++){int t=i*12+j*6,k=n+j;tris[t]=k;tris[t+1]=k+1;tris[t+2]=k+3;tris[t+3]=k+3;tris[t+4]=k+1;tris[t+5]=k+4;}
            }
            var mesh=new Mesh{name="Preserved upper body"};mesh.MarkDynamic();mesh.vertices=v;mesh.uv=uv;mesh.triangles=tris;mesh.colors=colors;mesh.RecalculateBounds();
            var props=new MaterialPropertyBlock();props.SetTexture("_MainTex",sprite.texture);props.SetColor("_RendererColor",Color.white);
            return new Body{mesh=mesh,sprite=sprite,colors=colors,alpha=alpha,properties=props,hipX=hipX,hipHeight=hipHeight};
        }
        static Vector2 UV(Sprite s,Vector2 local)=>new Vector2((s.rect.x+s.pivot.x+local.x*s.pixelsPerUnit)/s.texture.width,(s.rect.y+s.pivot.y+local.y*s.pixelsPerUnit)/s.texture.height);
        void BuildLeg(Part part,Sprite sprite,bool woman,bool inner)
        {
            const int cols=12,rows=48;int count=(cols+1)*(rows+1);
            part.rest=new Vector3[count];part.vertices=new Vector3[count];part.colors=new Color[count];var uv=new Vector2[count];var triangles=new int[cols*rows*6];
            var b=sprite.bounds;
            // Authored points measured on the two untouched generated limb parts. Texture coordinates
            // stay fixed while this mesh bends; no runtime texture edits or per-frame allocations.
            float sourceMin=woman?981:313,hipX=woman?1102:436,kneeX=woman?1130:476,ankleX=woman?1097:436;
            float soleY=961,hipY=105,kneeY=450,ankleY=835,width=.92f;
            if(Civilian){sourceMin=woman?971:314;hipX=woman?1080:425;kneeX=woman?1087:434;ankleX=woman?1056:396;soleY=956;hipY=80;kneeY=454;ankleY=817;width=1.06f;}
            if(inner)
            {
                // Independently measured source landmarks; the generated sheets are close to
                // the outer-leg reference, but are not assumed to be pixel registered.
                if(Civilian){sourceMin=woman?972:315;hipX=woman?1080:425;kneeX=woman?1087:434;ankleX=woman?1056:396;soleY=955;}
                else {sourceMin=woman?981:314;hipX=woman?1102:436;kneeX=woman?1130:476;ankleX=woman?1097:436;soleY=962;}
            }
            for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
            {
                int i=y*(cols+1)+x;Vector2 local=new Vector2(Mathf.Lerp(b.min.x,b.max.x,x/(float)cols),Mathf.Lerp(b.min.y,b.max.y,y/(float)rows));
                float sourceY=soleY-local.y*sprite.pixelsPerUnit;
                float center=sourceY<kneeY?Mathf.Lerp(hipX,kneeX,Mathf.InverseLerp(hipY,kneeY,sourceY)):Mathf.Lerp(kneeX,ankleX,Mathf.InverseLerp(kneeY,ankleY,sourceY));
                float mappedY=sourceY<kneeY?Mathf.LerpUnclamped(restHip.y,restKnee.y,(sourceY-hipY)/(kneeY-hipY)):sourceY<ankleY?Mathf.LerpUnclamped(restKnee.y,restAnkle.y,(sourceY-kneeY)/(ankleY-kneeY)):restAnkle.y-(sourceY-ankleY)*restAnkle.y/(soleY-ankleY);
                float hipWidth=Civilian?Mathf.Lerp(1,1.22f,Mathf.InverseLerp(restKnee.y+.14f,restHip.y,mappedY)):1;
                part.rest[i]=new Vector3((local.x-b.min.x+(sourceMin-center)/sprite.pixelsPerUnit)*width*hipWidth*(Civilian?1:Mathf.Lerp(1,1.14f,Mathf.InverseLerp(.26f,.62f,mappedY))),mappedY);
                uv[i]=UV(sprite,local);part.colors[i]=Color.white;
                if(x<cols&&y<rows){int t=(y*cols+x)*6;triangles[t]=i;triangles[t+1]=i+cols+1;triangles[t+2]=i+1;triangles[t+3]=i+1;triangles[t+4]=i+cols+1;triangles[t+5]=i+cols+2;}
            }
            part.mesh.MarkDynamic();part.mesh.vertices=part.rest;part.mesh.uv=uv;part.mesh.triangles=triangles;part.mesh.colors=part.colors;part.mesh.RecalculateBounds();
        }
        static Vector2 Rotate(Vector2 p,float angle){float c=Mathf.Cos(angle),s=Mathf.Sin(angle);return new Vector2(c*p.x-s*p.y,s*p.x+c*p.y);}
        void Bend(Part part,Pose pose,Color tint,float pelvisAngle)
        {
            float a=Mathf.Atan2(pose.knee.y-pose.hip.y,pose.knee.x-pose.hip.x)+Mathf.PI/2;
            float b=Mathf.Atan2(pose.ankle.y-pose.knee.y,pose.ankle.x-pose.knee.x)+Mathf.PI/2;
            for(int i=0;i<part.rest.Length;i++)
            {
                Vector2 p=part.rest[i];Vector2 upper=pose.hip+Rotate(p-restHip,a),lower=pose.knee+Rotate(p-restKnee,b),foot=FootPoint(pose,p-restAnkle);
                // The waistband follows the pelvis, not the rotating thigh. Blend into the
                // upper leg below the belt so the two hip caps cannot fan apart at full stride.
                Vector2 pelvis=pose.hip+Rotate(p-restHip,pelvisAngle*Mathf.Deg2Rad);
                float pelvisWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(restHip.y-.18f,restHip.y+.035f,p.y));
                upper=Vector2.Lerp(upper,pelvis,pelvisWeight);
                float kneeBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(restKnee.y+.09f,restKnee.y-.09f,p.y));
                float ankleBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(restAnkle.y+.09f,restAnkle.y-.05f,p.y));
                part.vertices[i]=Vector2.Lerp(Vector2.Lerp(upper,lower,kneeBlend),foot,ankleBlend);part.colors[i]=tint;
            }
            part.mesh.vertices=part.vertices;part.mesh.colors=part.colors;part.mesh.RecalculateBounds();
        }
        public void Show(float distance,bool running,bool helmet,int facing,Color tint,int order,int mode=0,bool aimed=false,bool backward=false)
            =>ShowPose(Mathf.Repeat(distance/CycleLength(running),1),running?1:0,running,helmet,facing,tint,order,mode,aimed,backward);
        void ShowPose(float phase,float runWeight,bool running,bool helmet,int facing,Color tint,int order,int mode,bool aimed,bool backward)
        {
            if(!initialized)return;gameObject.SetActive(true);Running=running;Phase=phase;RunWeight=runWeight;
            Near=Solve(Phase,runWeight);Far=Solve(Phase+.5f,runWeight);
            root.localPosition=new Vector3(0,-3+actor.lane,0);root.localScale=new Vector3(facing,1,1);
            BodyMode=Civilian?(mode==2?2:0):Mathf.Clamp(mode,0,2);Aimed=aimed;Backpedaling=backward;
            int bodyIndex=Civilian?(BodyMode==2?2:0)+(aimed?1:0):(BodyMode==0&&aimed?6:BodyMode*2)+(helmet?0:1);
            var selected=bodies[bodyIndex];float hipX=selected.hipX;
            UpperSprite=selected.sprite;
            near.renderer.transform.localPosition=new Vector3(hipX,0,0);far.renderer.transform.localPosition=new Vector3(hipX-.04f,0,0);
            BodyLeanDegrees=BodyLean(Phase,runWeight,aimed,backward);
            Bend(near,Near,tint,BodyLeanDegrees);Bend(far,Far,new Color(tint.r*.83f,tint.g*.83f,tint.b*.83f,tint.a),BodyLeanDegrees);
            group.sortingOrder=order;far.renderer.sortingOrder=0;near.renderer.sortingOrder=1;body.renderer.sortingOrder=2;
            if(activeBody!=bodyIndex){body.filter.sharedMesh=selected.mesh;body.renderer.SetPropertyBlock(selected.properties);activeBody=bodyIndex;}
            for(int i=0;i<selected.colors.Length;i++)selected.colors[i]=new Color(tint.r,tint.g,tint.b,tint.a*selected.alpha[i]);
            selected.mesh.colors=selected.colors;
            var pivot=new Vector2(hipX,selected.hipHeight);
            body.renderer.transform.localRotation=Quaternion.Euler(0,0,BodyLeanDegrees);
            body.renderer.transform.localPosition=new Vector2(hipX,Near.hip.y)-Rotate(pivot,BodyLeanDegrees*Mathf.Deg2Rad);
        }
        public void Advance(float distance,bool running,bool helmet,int facing,Color tint,int order,int mode=0,bool aimed=false,bool backward=false,float dt=0)
        {
            // Keep phase continuous when crossing the walk/run speed threshold. A half-step must
            // not become a different contact simply because its cycle length changed.
            Backpedaling=backward;
            float weight=gaitBlend.Step(running,dt);
            if(!float.IsNaN(lastDistance))travelledPhase+=Mathf.Max(0,distance-lastDistance)/CycleLength(weight)*(backward?-1:1);
            lastDistance=distance;travelledPhase=Mathf.Repeat(travelledPhase,1);
            ShowPose(travelledPhase,weight,running,helmet,facing,tint,order,mode,aimed,backward);
        }
        // While another action owns the pose, absorb travelled distance without advancing the
        // hidden gait. Re-entering locomotion therefore cannot jump across a burst of old travel.
        public void Suspend(float distance){lastDistance=distance;gaitBlend.Reset();gameObject.SetActive(false);}
        void OnDestroy(){if(near!=null)Destroy(near.mesh);if(far!=null)Destroy(far.mesh);if(body!=null)Destroy(body.mesh);if(bodies!=null)foreach(var m in bodies)Destroy(m.mesh);}
    }
}
