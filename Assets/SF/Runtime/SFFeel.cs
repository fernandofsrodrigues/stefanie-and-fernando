using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    // Impact feel for human play on the class course, after the classic arcade brawlers: a short hit-stop on the lead hero's
    // melee exchanges, a knockout slow-motion beat and flash when a boss falls, and a title card the first time a course boss speaks.
    // Automated runs set SmokeMode, so QA timing and captures are unchanged unless a check opts in with FeelForced.
    // The party campaign (CourseMode false) never uses any of it.
    // The "Camera impact motion" option doubles as the reduced-motion switch for hit-stop, slow motion, the flash and the card slide.
    public sealed partial class SFGame
    {
        public const float BossCardLength=2.6f;
        float hitStop,knockoutSlow;
        // True only while this pack holds Time.timeScale away from 1, so the clock is never written otherwise.
        bool feelHoldsTime;
        readonly HashSet<string> bossCardsShown=new HashSet<string>();
        internal bool FeelForced;
        internal float HitStopRemaining=>hitStop;
        public float KnockoutFlash {get;private set;}
        public float BossCardTime {get;private set;}
        public string BossCardName {get;private set;}="";
        public string BossCardRole {get;private set;}="";
        internal bool FeelActive=>(FeelForced||!SmokeMode)&&CourseMode&&ShakeEnabled&&State==SFState.Playing&&!DriveActive;
        // Rendering-only enemy variety (SFActor.Variety below). Off in every automated run unless a check opts in.
        internal bool PresentationVariety=>FeelForced||!SmokeMode;

        // Every accepted, unblocked hit reports here. A boss knockout always gets its beat, whatever finished it (a pistol shot too).
        // Otherwise gunfire and hazards never freeze the frame, and only exchanges involving the lead hero do,
        // so a partner fight elsewhere on screen never stutters the player's own timing.
        internal void LandedHit(SFActor target,SFActor from,float amount)
        {
            if(!FeelActive||from==null)return;
            if(target.boss&&!target.Alive){hitStop=Mathf.Max(hitStop,.2f);knockoutSlow=1.1f;KnockoutFlash=1;HoldTime(.05f);return;}
            if(from.action==SFAction.Shoot)return;
            // Defeated() runs first and hands the lead to a living partner, so a lead knocked out this frame is now Partner with a fresh .6 swap cooldown.
            bool downedLead=target.hero&&!target.Alive&&target==Partner&&swapCooldown>=.6f;
            if(target!=Player&&from!=Player&&!downedLead)return;
            bool heavy=amount>=18||from.action==SFAction.Sweep||(from.ComboAttack&&from.ComboMove.finisher);
            hitStop=Mathf.Max(hitStop,!target.Alive?.12f:heavy?.08f:.05f);
            // Set now so the very next frame is already frozen.
            HoldTime(.05f);
        }

        // The first line from a course boss (final or mid-boss) raises its title card once per run.
        void AnnounceBoss(string speaker)
        {
            if(!CourseMode||(SmokeMode&&!FeelForced)||bossCardsShown.Contains(speaker))return;
            var chapter=Chapter;
            foreach(var beat in chapter.beats)
            {
                if(beat.speaker!=speaker||beat.actor<0||beat.actor>=chapter.cast.Length||!chapter.cast[beat.actor].boss)continue;
                bossCardsShown.Add(speaker);BossCardName=speaker;BossCardTime=BossCardLength;
                BossCardRole=(chapter.cast[beat.actor].final?"BOSS":"MID-BOSS")+"  /  "+chapter.city;return;
            }
        }

        // Runs first in Update on unscaled time. Outside live play (pause, menu, result cards, the drive, option OFF,
        // automated runs) the clock is restored to normal speed and any pending freeze is dropped.
        void TickFeel()
        {
            float real=Mathf.Min(Time.unscaledDeltaTime,.1f);
            KnockoutFlash=Mathf.MoveTowards(KnockoutFlash,0,real*2.5f);
            if(State==SFState.Playing)BossCardTime=Mathf.Max(0,BossCardTime-real);
            if(!FeelActive){hitStop=knockoutSlow=0;ReleaseTime();return;}
            if(hitStop>0)hitStop=Mathf.Max(0,hitStop-real);
            else if(knockoutSlow>0)knockoutSlow=Mathf.Max(0,knockoutSlow-real);
            if(hitStop>0)HoldTime(.05f);
            else if(knockoutSlow>0)HoldTime(Mathf.Lerp(1,.3f,Mathf.Clamp01(knockoutSlow/.5f)));
            else ReleaseTime();
        }

        void HoldTime(float scale){Time.timeScale=scale;feelHoldsTime=true;}
        void ReleaseTime(){if(!feelHoldsTime)return;Time.timeScale=1;feelHoldsTime=false;}

        // A scene restart (or the object going away mid-freeze) always leaves the clock at normal speed.
        void OnDisable(){hitStop=knockoutSlow=0;ReleaseTime();}
    }

    // Presentation-only variety against "an army of identical twins": the n-th instance of an enemy person in creation
    // order (the first is 0 and is left unchanged) takes a subtle tint and an idle loop started .37 s * n later.
    // Persons, not identities (SFEnemyProfiles.Person): a smuggler_shotgun is the smuggler with a shotgun, so the 2nd and later
    // appearances of that one person always tint, whichever identity each carries. Every other id is its own person.
    // Heroes, allies and bosses never vary. It is off in the menu and in automated runs, so QA colours and frames are unchanged.
    public sealed partial class SFActor
    {
        static readonly Color[] VarietyTints={new Color(1,.93f,.86f),new Color(.88f,.94f,1),new Color(.95f,1,.9f),new Color(.93f,.9f,.98f)};
        int twinRank=-1;
        // Counted once and cached: the actors of the same person created before this one (Configure runs before Actors.Add).
        internal int TwinRank
        {
            get
            {
                if(twinRank>=0)return twinRank;
                twinRank=0;string person=SFEnemyProfiles.Person(identity);
                foreach(var a in game.Actors){if(a==this)break;if(SFEnemyProfiles.Person(a.identity)==person)twinRank++;}
                return twinRank;
            }
        }
        internal int Variety=>!hero&&!ally&&!boss&&game.CourseMode&&game.PresentationVariety&&game.State!=SFState.Menu?TwinRank:0;
        internal static Color VarietyTint(int rank)=>rank>0?VarietyTints[(rank-1)%VarietyTints.Length]:Color.white;
    }
}
