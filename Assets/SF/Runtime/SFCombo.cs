namespace StefanieAndFernando
{
    public enum SFStrike { Jab,Straight,Hook,Uppercut,Roundhouse }
    public readonly struct SFComboMove
    {
        public readonly SFStrike kind;
        public readonly float duration,contact,damage,reach;
        public readonly bool finisher;
        public SFComboMove(SFStrike kind,float duration,float contact,float damage,float reach,bool finisher=false)
        {this.kind=kind;this.duration=duration;this.contact=contact;this.damage=damage;this.reach=reach;this.finisher=finisher;}
        public string Label=>kind==SFStrike.Hook?"LEFT HOOK":kind==SFStrike.Roundhouse?"RIGHT ROUNDHOUSE":kind.ToString().ToUpperInvariant();
    }
    public static class SFCombo
    {
        static readonly SFComboMove Jab=new SFComboMove(SFStrike.Jab,.28f,.10f,16,1.75f);
        static readonly SFComboMove Straight=new SFComboMove(SFStrike.Straight,.34f,.13f,20,1.9f);
        static readonly SFComboMove Hook=new SFComboMove(SFStrike.Hook,.40f,.17f,24,1.65f);
        static readonly SFComboMove Uppercut=new SFComboMove(SFStrike.Uppercut,.52f,.22f,34,1.85f,true);
        static readonly SFComboMove Roundhouse=new SFComboMove(SFStrike.Roundhouse,.66f,.30f,36,2.55f,true);
        public static int Length(string identity)=>identity=="stefanie"?4:5;
        public static SFComboMove Get(string identity,int step,bool kickFinish=false)
        {
            bool woman=identity=="stefanie";
            if(step==Length(identity)-1)return woman||kickFinish?Roundhouse:Uppercut;
            return step==1||step==3?Straight:step==2&&woman?Hook:Jab;
        }
    }
}
