using UnityEngine;

namespace StefanieAndFernando
{
    // State belongs to an actor's visible rig. Hidden attacks/airborne poses must not
    // leave an old sprint blend waiting when grounded locomotion becomes visible again.
    public sealed class SFGaitBlend
    {
        public const float AccelerateSeconds=.22f, DecelerateSeconds=.26f;
        float progress;
        bool initialized;
        public float Weight=>Mathf.SmoothStep(0,1,progress);
        public void Reset(){initialized=false;}
        public float Step(bool running,float dt)
        {
            if(!initialized){progress=running?1:0;initialized=true;}
            else if(dt>0)progress=Mathf.MoveTowards(progress,running?1:0,dt/(running?AccelerateSeconds:DecelerateSeconds));
            return Weight;
        }
    }
}
