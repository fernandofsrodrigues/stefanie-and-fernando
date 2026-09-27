using UnityEngine;

namespace StefanieAndFernando
{
    // Visual phases share the existing reload clock; ammunition still transfers only at completion.
    public static class SFReloadAnimation
    {
        public static int Frame(float clock,float duration)
        {
            float progress=Mathf.Clamp01(clock/Mathf.Max(.001f,duration));
            return progress<.20f?0:progress<.48f?1:progress<.78f?2:3;
        }
    }
}
