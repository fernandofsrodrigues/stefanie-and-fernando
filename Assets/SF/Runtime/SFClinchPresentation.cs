using UnityEngine;

namespace StefanieAndFernando
{
    public static class SFClinchAnimation
    {
        // Chamber, contact and recovery use the attack clock, so hit-stop/pause cannot desync the pair.
        public static int Frame(SFAction action,float clock,float contact,float duration)
        {
            if(action!=SFAction.ClinchKnee||clock<contact-.20f)return 0;
            if(clock<contact-.05f)return 1;
            if(clock<contact+.11f)return 2;
            return clock<duration-.06f?3:0;
        }
    }

    public sealed partial class SFActor
    {
        // A paired sheet contains both identities. Never apply it to another enemy or a military outfit.
        bool PairedContact => hero&&Alive&&!Armed&&!FieldUniform&&
            GripTarget!=null&&GripTarget.identity=="latch"&&!GripTarget.boss&&GripTarget.Alive&&
            GripTarget.HeldBy==this&&
            Mathf.Abs(lane-GripTarget.lane)<.08f&&Mathf.Abs(Height-GripTarget.Height)<.18f&&
            Mathf.Abs((GripTarget.X-X)*facing-.85f)<.22f&&game.ClearAttack(this,GripTarget,false);
        public bool UsePairedClinch => PairedContact&&!GroundSequence&&
            (action==SFAction.Grapple||action==SFAction.ClinchKnee)&&GripTarget.action==SFAction.Held&&
            art.TryGetValue("clinch_latch",out var pair)&&pair!=null&&pair.frames.Length==4;
        public bool UsePairedGround => PairedContact&&GroundSequence&&action==SFAction.GroundStrike&&
            GripTarget.action==SFAction.Knocked&&
            art.TryGetValue("takedown_latch",out var pair)&&pair!=null&&pair.frames.Length==6&&
            art.TryGetValue("ground_straight_latch",out var straight)&&straight!=null&&straight.frames.Length==1;
        public bool UsePairedBody => UsePairedClinch||UsePairedGround;
        bool ShowingPairedBody => CurrentSheet=="clinch_latch"||CurrentSheet=="takedown_latch"||CurrentSheet=="ground_straight_latch";
    }
    public static class SFTakedownAnimation
    {
        // No separate animation clock: entry ends before the first hit, and rise follows the sixth.
        // Frame 4 in the sheet repeats a jab and is deliberately never displayed.
        public static int Frame(float clock)
        {
            if(clock<.14f)return 0;
            if(clock<.30f)return 1;
            if(clock>SFGroundAnimation.FirstHit+(SFGroundAnimation.Hits-1)*SFGroundAnimation.Interval+.12f)return 5;
            int strike=SFGroundAnimation.Frame(clock);
            return strike==1?3:strike==3?4:2;
        }
    }
}
