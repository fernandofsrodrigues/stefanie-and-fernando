using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFActor
    {
        public readonly SFMagazine SidearmAmmo=new SFMagazine(),RifleAmmo=new SFMagazine();
        public SFMagazine Magazine=>Rifle?RifleAmmo:SidearmAmmo;
        public bool Reloading=>action==SFAction.Reload;
        public int TotalAmmo=>SidearmAmmo.Total+RifleAmmo.Total;
        public int NextComboStep {get;private set;}
        public int ActiveComboStep {get;private set;}
        public SFComboMove ComboMove {get;private set;}
        public bool ComboAttack {get;private set;}
        public float ComboWindow {get;private set;}
        float punchBuffer,kickBuffer;bool punchHeld;
        public string CombatLabel=>GroundSequence?GroundLabel:Reloading?"RELOADING "+Mathf.CeilToInt(Mathf.Max(0,attackDuration-actionClock)*10)/10f+"s":
            ComboAttack&&Busy?(ActiveComboStep+1)+" / "+SFCombo.Length(identity)+"  "+ComboMove.Label:
            ComboWindow>0&&NextComboStep>0?"NEXT: "+SFCombo.Get(identity,NextComboStep).Label:"";
        public void ResetWeapons()
        {
            var kit=game.Loadout(this);int cap=kit.sidearm==1?8:17;
            SidearmAmmo.Reset(cap,cap*(kit.gear==2?4:3));RifleAmmo.Reset(20,kit.gear==2?80:60);
            ResetCombo();
        }
        public void ResetCombo(){NextComboStep=0;ComboWindow=0;ComboAttack=false;punchBuffer=kickBuffer=0;}
        void TickCombatInput(float dt,SFCommand c)
        {
            ComboWindow=Mathf.Max(0,ComboWindow-dt);
            if(ComboWindow<=0)NextComboStep=0;
            punchBuffer=Mathf.Max(0,punchBuffer-dt);kickBuffer=Mathf.Max(0,kickBuffer-dt);
            if(!Armed&&!Restrained&&Alive)
            {
                if(c.punch&&!punchHeld)punchBuffer=.22f;
                if(c.kick)kickBuffer=.22f;
            }
            punchHeld=c.punch;
        }
        bool BeginCombo(bool kick)
        {
            int step=ComboWindow>0?NextComboStep:0;
            if(kick&&step!=SFCombo.Length(identity)-1)return false;
            var move=SFCombo.Get(identity,step,kick);
            BeginAttack(move.kind==SFStrike.Roundhouse?SFAction.Kick:SFAction.Punch);
            ComboAttack=true;ComboMove=move;ActiveComboStep=step;
            attackDuration=move.duration;attackAt=move.contact;cooldown=move.duration+.06f;
            punchBuffer=kickBuffer=0;return true;
        }
        public void ConfirmComboHit()
        {
            if(!ComboAttack)return;
            NextComboStep=ComboMove.finisher?0:ActiveComboStep+1;
            ComboWindow=ComboMove.finisher?0:Mathf.Max(0,attackDuration-actionClock)+.72f;
        }
        public bool TryReload()
        {
            if(!hero||!Alive||!Armed||Busy||Restrained||action==SFAction.Hurt||GripTarget!=null||!Magazine.CanReload)return false;
            ResetCombo();triggerBuffer=0;action=SFAction.Reload;actionClock=0;
            attackDuration=Rifle?(PrecisionRifle?2.4f:2.15f):1.35f;cooldown=attackDuration;
            motor.SetCommand(0,false);return true;
        }
        void ChangeWeapon(SFCommand c)
        {
            if(!c.toggleArmed&&!c.cycleWeapon&&c.weapon==0)return;
            if(Reloading){action=SFAction.Idle;actionClock=0;cooldown=0;}
            ResetCombo();triggerBuffer=0;
            if(c.toggleArmed)Armed=!Armed;
            if(c.cycleWeapon){Rifle=!Rifle;Armed=true;}
            if(c.weapon!=0){Rifle=c.weapon==2;Armed=true;}
        }
    }
}
