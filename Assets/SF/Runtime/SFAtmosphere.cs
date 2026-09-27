using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // Human-play atmosphere, one mood per course chapter: after-rain drips and mist in Curitiba, golden-hour motes in Rio,
    // wind-blown sand in Al Anbar. Visual only (no colliders, nothing reads it); off in automated runs so QA captures stay
    // deterministic, hidden on the menu and during the drive, and frozen with everything else on pause and hit-stop.
    public sealed partial class SFGame
    {
        SpriteRenderer[] motes;Vector2[] moteVelocity;float[] moteSeed;int moteChapter=-1;
        internal bool AtmosphereActive=>(FeelForced||!SmokeMode)&&CourseMode&&!DriveActive;
        internal int AtmosphereCount=>motes==null?0:motes.Length;
        internal bool AtmosphereShown=>motes!=null&&motes.All(m=>m.enabled);
        internal bool AtmosphereHidden=>motes==null||motes.All(m=>!m.enabled);
        internal Vector3[] AtmospherePositions=>motes==null?new Vector3[0]:motes.Select(m=>m.transform.position).ToArray();

        void TickAtmosphere(float dt)
        {
            if(motes!=null&&(!AtmosphereActive||State==SFState.Menu))foreach(var m in motes)m.enabled=false;
            if(!AtmosphereActive||State!=SFState.Playing)return;
            if(motes==null||moteChapter!=CourseChapter)BuildAtmosphere(CourseChapter);
            float halfW=Camera.orthographicSize*Camera.aspect+1,halfH=Camera.orthographicSize+1;var view=Camera.transform.position;
            for(int i=0;i<motes.Length;i++)
            {
                var m=motes[i];m.enabled=true;
                var p=m.transform.position+(Vector3)(moteVelocity[i]*dt);
                // A slow sway keeps neighbouring motes from moving as one sheet.
                p.x+=Mathf.Sin(Elapsed*(.5f+moteSeed[i])+moteSeed[i]*20)*dt*.3f;
                // Wrap around the view so the field stays full while the camera pans; motes still move with the world, not the screen.
                if(p.x<view.x-halfW)p.x+=2*halfW;else if(p.x>view.x+halfW)p.x-=2*halfW;
                if(p.y<view.y-halfH)p.y+=2*halfH;else if(p.y>view.y+halfH)p.y-=2*halfH;
                m.transform.position=p;
            }
        }

        void BuildAtmosphere(int chapter)
        {
            if(motes!=null)foreach(var m in motes)if(m!=null)Destroy(m.gameObject);
            moteChapter=chapter;int count=chapter==2?64:chapter==1?44:34;
            motes=new SpriteRenderer[count];moteVelocity=new Vector2[count];moteSeed=new float[count];
            float halfW=Camera.orthographicSize*Camera.aspect+1,halfH=Camera.orthographicSize+1;var view=Camera.transform.position;
            for(int i=0;i<count;i++)
            {
                float a=AtmosphereHash(i),b=AtmosphereHash(i+101),c=AtmosphereHash(i+211);moteSeed[i]=a;
                // One in six drifts in front of the actors; the rest stay behind them.
                bool front=i%6==0;
                SpriteRenderer sr;
                if(chapter==2)
                {
                    bool haze=i%8==1;
                    sr=CreateSprite("Atmosphere sand",haze?Ellipse:Pixel,haze?new Color(.86f,.7f,.5f,.05f):new Color(.95f,.82f,.6f,.14f+.16f*b),front&&!haze?180:50);
                    sr.transform.localScale=haze?new Vector3(4+2*b,1.1f,1):new Vector3(.3f+.5f*c,.018f,1);
                    moteVelocity[i]=haze?new Vector2(-.6f-.4f*b,0):new Vector2(-3.5f-3*b,-.25f-.35f*c);
                }
                else if(chapter==1)
                {
                    sr=CreateSprite("Atmosphere mote",Ellipse,new Color(1,.84f,.5f,.2f+.25f*b),front?180:50);
                    sr.transform.localScale=Vector3.one*(.05f+.07f*c);
                    moteVelocity[i]=new Vector2(.08f-.16f*b,.05f+.15f*c);
                }
                else
                {
                    bool mist=i%5==2;
                    sr=CreateSprite(mist?"Atmosphere mist":"Atmosphere drip",mist?Ellipse:Pixel,mist?new Color(.7f,.82f,.9f,.045f):new Color(.78f,.88f,.96f,.14f+.12f*b),front&&!mist?180:50);
                    sr.transform.localScale=mist?new Vector3(3.5f+2*b,1.2f,1):new Vector3(.016f,.18f+.14f*c,1);
                    moteVelocity[i]=mist?new Vector2(.15f+.2f*b,0):new Vector2(-.25f,-5-2*b);
                }
                sr.transform.SetParent(World,false);sr.transform.position=new Vector3(view.x-halfW+2*halfW*a,view.y-halfH+2*halfH*c,0);
                motes[i]=sr;
            }
        }

        // Deterministic 0..1 spread so every run lays out the same field.
        static float AtmosphereHash(int i)=>Mathf.Repeat(Mathf.Sin(i*12.9898f+.5f)*43758.5453f,1);
    }
}
