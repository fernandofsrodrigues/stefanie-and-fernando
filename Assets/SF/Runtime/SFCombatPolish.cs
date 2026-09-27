using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        sealed class ShotTrail
        {
            public SpriteRenderer core,glow;public Vector3 from,to;public float age,duration;public bool active;
        }
        readonly List<ShotTrail> shotTrails=new List<ShotTrail>();
        public int ActiveTracers=>shotTrails.Count(t=>t.active);
        public Vector3 LastShotEndpoint {get;private set;}
        public static Color TracerColor(SFActor actor)=>actor.ally?new Color(.54f,.88f,.65f):actor.hero?new Color(1,.72f,.30f):new Color(1,.35f,.18f);
        void FirePresentation(SFActor actor,Vector3 end,bool struckCover)
        {
            var from=actor.VisualPosition+Vector3.up*actor.MuzzleHeight;
            from+=Vector3.right*actor.facing*Mathf.Min(actor.ShotRifle?.91f:.68f,Mathf.Abs(end.x-from.x)*.3f);
            LastShotEndpoint=end;
            var trail=shotTrails.FirstOrDefault(t=>!t.active);
            if(trail==null&&shotTrails.Count<32)
            {trail=new ShotTrail{core=CreateSprite("Tracer core",Pixel,Color.white,194),glow=CreateSprite("Tracer glow",Ellipse,Color.white,193)};shotTrails.Add(trail);}
            if(trail!=null)
            {
                trail.active=true;trail.from=from;trail.to=end;trail.age=0;trail.duration=Mathf.Clamp(Vector3.Distance(from,end)/150,.055f,.11f);
                trail.core.color=Color.Lerp(TracerColor(actor),Color.white,.65f);trail.glow.color=TracerColor(actor);
                trail.core.enabled=trail.glow.enabled=true;DrawTrail(trail,0);
            }
            var flash=CreateSprite("Muzzle light",Ellipse,new Color(1,.78f,.4f,.8f),195);flash.transform.position=from;
            flash.transform.localScale=new Vector3(.25f,.11f,1);effects.Add(new SFEffect{visual=flash,life=.045f,maxLife=.045f});
            if(struckCover)
            {
                for(int i=0;i<4;i++)
                {var dust=CreateSprite("Cover dust",Ellipse,new Color(.7f,.66f,.55f,.5f),192);dust.transform.position=end;dust.transform.localScale=Vector3.one*Random.Range(.055f,.13f);effects.Add(new SFEffect{visual=dust,velocity=Random.insideUnitCircle*.6f,life=.28f,maxLife=.28f});}
                Audio.PlayAt("cover",end.x,.35f);
            }
            Audio.PlayAt(actor.ShotRifle?"rifle":"pistol",actor.X,.6f);
        }
        void DrawTrail(ShotTrail t,float phase)
        {
            var delta=t.to-t.from;float distance=delta.magnitude;
            float head=Mathf.Min(distance,Mathf.Lerp(.2f,distance,phase)),tail=Mathf.Max(0,head-1.05f);
            Vector3 middle=t.from+delta.normalized*((head+tail)*.5f);
            foreach(var sr in new[]{t.core,t.glow}){sr.transform.position=middle;sr.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);}
            t.core.transform.localScale=new Vector3(head-tail,.014f,1);t.glow.transform.localScale=new Vector3(head-tail,.06f,1);
        }
        void TickTracers(float dt)
        {
            foreach(var t in shotTrails.Where(t=>t.active))
            {t.age+=dt;if(t.age>=t.duration){t.active=false;t.core.enabled=t.glow.enabled=false;}else DrawTrail(t,t.age/t.duration);}
        }
        public SFCover FindEnemyCover(SFActor actor,SFActor target)
        {
            return Covers.Where(c=>c.height>=1.15f&&Mathf.Abs(c.footprint.center.x-actor.X)<5.5f&&Mathf.Abs(c.footprint.center.x-target.X)>2.5f)
                .Where(c=>{float side=Mathf.Sign(c.footprint.center.x-target.X);var at=new Vector2(side>0?c.footprint.xMax+.6f:c.footprint.xMin-.6f,c.footprint.center.y);return c.Blocks(target.GroundPosition,1.8f,at,.85f);})
                .OrderBy(c=>Mathf.Abs(c.footprint.center.x-actor.X)).FirstOrDefault();
        }
        SFCommand RangedCoverCommand(SFActor actor,SFActor target)
        {
            var cover=FindEnemyCover(actor,target);
            if(cover==null){actor.CoverChoice=null;return Navigate(actor,new Vector2(target.X-Mathf.Sign(target.X-actor.X)*5.5f,target.lane));}
            if(actor.CoverChoice!=cover){actor.CoverChoice=cover;actor.CoverClock=0;}
            float side=Mathf.Sign(cover.footprint.center.x-target.X);
            var shelter=new Vector2(side>0?cover.footprint.xMax+.6f:cover.footprint.xMin-.6f,cover.footprint.center.y);
            bool atShelter=Vector2.Distance(actor.GroundPosition,shelter)<.4f;
            bool peek=actor.CoverClock%3.8f>1.45f;
            var goal=shelter;if(peek&&cover.height>1.7f)goal.y=cover.footprint.yMin-.72f;
            var c=Navigate(actor,goal);c.walk=true;c.crouch=!peek&&atShelter;if(!actor.Busy)actor.facing=target.X>actor.X?1:-1;return c;
        }
    }
}
