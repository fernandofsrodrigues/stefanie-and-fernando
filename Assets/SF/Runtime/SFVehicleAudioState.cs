using UnityEngine;

namespace StefanieAndFernando
{
    [System.Flags]
    public enum SFVehicleCue { None=0, DoorOpen=1, DoorClose=2, Ignition=4 }

    // Road-clock events and a bounded mix, independent of the audio device or frame rate.
    public sealed class SFVehicleAudioState
    {
        SFDrivePhase phase=SFDrivePhase.Ready;
        float clock,speed;int impacts;
        public float EngineGain {get;private set;}
        public float TireGain {get;private set;}
        public float Pitch {get;private set;}=.88f;
        public bool EngineWanted {get;private set;}
        public SFVehicleCue Step(SFDrivePhase next,float nextClock,float nextSpeed,int nextImpacts,float dt,bool paused)
        {
            if(paused||dt<=0)return SFVehicleCue.None;
            dt=Mathf.Min(dt,.25f);SFVehicleCue cues=SFVehicleCue.None;
            bool changed=phase!=next;
            if(changed)
            {
                if(next==SFDrivePhase.Boarding||next==SFDrivePhase.Dismounting)cues|=SFVehicleCue.DoorOpen;
                if(next==SFDrivePhase.Driving)cues|=SFVehicleCue.Ignition;
            }
            float closeAt=next==SFDrivePhase.Boarding?1.15f:1.55f;
            if((next==SFDrivePhase.Boarding||next==SFDrivePhase.Dismounting)&&nextClock>=closeAt&&(changed||clock<closeAt))
                cues|=SFVehicleCue.DoorClose;
            EngineWanted=next==SFDrivePhase.Driving||next==SFDrivePhase.Stopping;
            float ratio=Mathf.Clamp01(nextSpeed/SFDriveModel.MaxSpeed);
            float engine=EngineWanted?Mathf.Lerp(.13f,.23f,ratio):0;
            // Avoid tire chirps during coasting, lane changes, or collision speed loss.
            bool braking=!changed&&next==SFDrivePhase.Driving&&nextImpacts==impacts&&nextSpeed>2&&(speed-nextSpeed)/dt>5;
            EngineGain=Mathf.MoveTowards(EngineGain,engine,dt*.7f);
            TireGain=Mathf.MoveTowards(TireGain,braking?.10f*ratio:0,dt*.7f);
            Pitch=Mathf.MoveTowards(Pitch,Mathf.Lerp(.88f,1.18f,ratio),dt*.45f);
            phase=next;clock=nextClock;speed=nextSpeed;impacts=nextImpacts;
            return cues;
        }
    }
}
