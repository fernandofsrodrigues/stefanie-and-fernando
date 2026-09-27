using UnityEngine;

namespace StefanieAndFernando
{
    // Depth-facing locomotion is visual only; combat keeps its horizontal target facing.
    public sealed class SFDepthMotion
    {
        public int Direction {get;private set;}
        public bool Moving {get;private set;}
        public bool Running {get;private set;}
        public float Phase {get;private set;}
        public void ClearMovement(){Moving=false;}
        public void Reset(){Direction=0;Moving=Running=false;Phase=0;}
        public void Step(Vector2 input,float actualDepthTravel,float dt,bool run,bool allowed)
        {
            Moving=false;
            if(!allowed||dt<=0||input.sqrMagnitude<.01f){Direction=0;return;}
            float x=Mathf.Abs(input.x),y=Mathf.Abs(input.y);
            // Hysteresis prevents analogue diagonal jitter from rapidly swapping cameras.
            bool depth=y>(Direction==0?x*1.2f:x*.8f)&&y>.1f;
            if(!depth){Direction=0;return;}
            if(Mathf.Abs(actualDepthTravel)<.00001f)return; // no cycling against lane/cover bounds
            Direction=actualDepthTravel>0?1:-1;Moving=true;Running=run;
            Phase=Mathf.Repeat(Phase+Mathf.Abs(actualDepthTravel)/(run?1.1f:.8f),1);
        }
        public int Frame(bool helmet)=> (helmet?0:8)+(Running?4:0)+Mathf.Min(3,(int)(Phase*4));
    }
}
