using UnityEngine;

namespace StefanieAndFernando
{
    public static class SFStrikeAnimation
    {
        // Preparation holds until the authored hit event; contact holds briefly before recovery.
        public static int ThreePoseFrame(float clock,float contact,float duration)
        {
            if(clock<contact)return 0;
            return clock<contact+Mathf.Max(0,duration-contact)*.35f?1:2;
        }
        // Three preparation frames, contact at exactly the hit event, then four recovery poses.
        public static int UppercutFrame(float clock,float contact,float duration)=>EightPoseFrame(clock,contact,duration);
        public static int EightPoseFrame(float clock,float contact,float duration)
        {
            if(clock<contact)return Mathf.Clamp((int)(Mathf.Max(0,clock)/Mathf.Max(.001f,contact)*3),0,2);
            return 3+Mathf.Clamp((int)((clock-contact)/Mathf.Max(.001f,duration-contact)*5),0,4);
        }
    }
}
