using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFDriveCheck
    {
        void CheckVehicleMix()
        {
            var s=new SFVehicleAudioState();
            Check(s.Step(SFDrivePhase.Ready,1,0,0,.1f,false)==SFVehicleCue.None&&s.EngineGain==0,"parked vehicle mix is silent");
            Check(s.Step(SFDrivePhase.Boarding,.02f,0,0,.02f,false)==SFVehicleCue.DoorOpen,"door opens at boarding transition");
            Check(s.Step(SFDrivePhase.Boarding,1.1f,0,0,.25f,false)==SFVehicleCue.None,"door close waits for boarding pose timing");
            Check(s.Step(SFDrivePhase.Boarding,1.3f,0,0,.2f,false)==SFVehicleCue.DoorClose,"slow frame crossing door threshold emits one close");
            Check(s.Step(SFDrivePhase.Boarding,1.5f,0,0,.2f,false)==SFVehicleCue.None,"closed door does not retrigger each tick");
            Check(s.Step(SFDrivePhase.Driving,0,0,0,.05f,false)==SFVehicleCue.Ignition&&s.EngineWanted,"driving starts ignition");
            for(int i=0;i<50;i++)s.Step(SFDrivePhase.Driving,1+i*.05f,11,0,.05f,false);
            Check(Mathf.Abs(s.EngineGain-.23f)<.001f&&Mathf.Abs(s.Pitch-1.18f)<.001f,"top speed engine pitch and gain remain capped");
            float gain=s.EngineGain,pitch=s.Pitch;
            Check(s.Step(SFDrivePhase.Dismounting,1.7f,0,0,.25f,true)==SFVehicleCue.None&&s.EngineGain==gain&&s.Pitch==pitch,"pause freezes event cursor pitch and gain");
            Check(s.Step(SFDrivePhase.Driving,4,11,0,.05f,false)==SFVehicleCue.None,"resume retains driving lifecycle");
            s.Step(SFDrivePhase.Driving,4.05f,10.9f,0,.05f,false);
            Check(s.TireGain==0,"ordinary coasting does not squeal tires");
            s.Step(SFDrivePhase.Driving,4.1f,10.15f,0,.05f,false);
            Check(s.TireGain>0&&s.TireGain<=.10f,"strong braking fades a bounded tire cue");
            for(int i=0;i<8;i++)s.Step(SFDrivePhase.Driving,4.2f+i*.05f,9,0,.05f,false);
            Check(s.TireGain==0,"tire sound releases after braking");
            s.Step(SFDrivePhase.Driving,5,2.5f,1,.05f,false);
            Check(s.TireGain==0,"collision speed loss does not masquerade as braking");
            s.Step(SFDrivePhase.Driving,5.05f,1.75f,1,.05f,false);
            Check(s.TireGain==0,"low speed braking does not sustain skid loop");
            Check(s.Step(SFDrivePhase.Stopping,0,4,1,.05f,false)==SFVehicleCue.None&&s.EngineWanted,"arrival braking retains engine without new ignition");
            Check(s.Step(SFDrivePhase.Dismounting,0,0,1,.05f,false)==SFVehicleCue.DoorOpen&&!s.EngineWanted,"dismount opens door and releases engine");
            Check(s.Step(SFDrivePhase.Dismounting,1.7f,0,1,.25f,false)==SFVehicleCue.DoorClose,"dismount threshold emits close on slow frame");
            for(int i=0;i<20;i++)s.Step(SFDrivePhase.Complete,i*.05f,0,1,.05f,false);
            Check(s.EngineGain==0&&s.TireGain==0,"completed arrival has no residual loop gain");
            s.Step(SFDrivePhase.Recovery,0,0,2,.05f,false);
            Check(!s.EngineWanted&&s.EngineGain==0,"disabled vehicle is silent");
            Check(s.Step(SFDrivePhase.Driving,0,0,2,.05f,false)==SFVehicleCue.Ignition,"checkpoint retry starts a fresh ignition");
            Check(s.Step(SFDrivePhase.Driving,.1f,0,2,.1f,false)==SFVehicleCue.None,"retry cannot repeat ignition");
            Check(s.Step(SFDrivePhase.Boarding,0,0,2,0,false)==SFVehicleCue.None,"zero delta cannot advance audio lifecycle");
        }
    }
}
