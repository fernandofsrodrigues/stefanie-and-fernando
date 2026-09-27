using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFAudio
    {
        readonly AudioSource[] vehicleShots=new AudioSource[4];
        readonly float[] vehicleShotGains=new float[4];
        readonly AudioClip[] vehicleClips=new AudioClip[5];
        AudioSource vehicleEngine,vehicleTires;
        SFVehicleAudioState vehicleMix=new SFVehicleAudioState();
        bool vehicleActive,vehiclePaused,engineStarted,tiresStarted;int vehicleCursor;
        internal int VehicleCueCount {get;private set;}
        internal SFVehicleAudioState VehicleMix=>vehicleMix;
        internal bool VehiclePaused=>vehiclePaused;
        internal bool VehicleActive=>vehicleActive;
        internal int VehicleClipCount {get{int n=0;foreach(var c in vehicleClips)if(c!=null)n++;return n;}}
        internal AudioSource[] VehicleSources=>new[]{vehicleEngine,vehicleTires,vehicleShots[0],vehicleShots[1],vehicleShots[2],vehicleShots[3]};
        void InitVehicleAudio()
        {
            string[] names={"vehicle_engine","vehicle_tires","vehicle_door_open","vehicle_door_close","vehicle_ignition"};
            for(int i=0;i<names.Length;i++)vehicleClips[i]=Resources.Load<AudioClip>("Audio/"+names[i]);
            vehicleEngine=Source();vehicleTires=Source();vehicleEngine.loop=vehicleTires.loop=true;
            vehicleEngine.clip=vehicleClips[0];vehicleTires.clip=vehicleClips[1];
            for(int i=0;i<vehicleShots.Length;i++)vehicleShots[i]=Source();
        }
        bool motorcycleAudio;
        public void BeginVehicleAudio(bool motorcycle=false)
        {
            StopVehicleAudio();motorcycleAudio=motorcycle;vehicleEngine.clip=motorcycle?Resources.Load<AudioClip>("Audio/motorcycle_idle"):vehicleClips[0];vehicleMix=new SFVehicleAudioState();vehicleActive=true;VehicleCueCount=0;
        }
        public void StopVehicleAudio()
        {
            if(vehicleEngine==null)return;
            vehicleEngine.Stop();vehicleTires.Stop();vehicleEngine.volume=vehicleTires.volume=0;
            foreach(var s in vehicleShots){s.Stop();s.clip=null;s.volume=0;}
            vehicleActive=vehiclePaused=engineStarted=tiresStarted=false;vehicleCursor=0;
        }
        public void TickVehicleAudio(SFDriveModel road,float dt,bool suspended)
        {
            if(!vehicleActive||road==null)return;
            RefreshVehicleAudio(suspended);
            var cues=vehicleMix.Step(road.Phase,road.Clock,road.Speed,road.Impacts,dt,suspended);
            if(suspended||dt<=0)return;
            if(!motorcycleAudio&&(cues&SFVehicleCue.DoorOpen)!=0)VehicleOneShot(2,.30f);
            if(!motorcycleAudio&&(cues&SFVehicleCue.DoorClose)!=0)VehicleOneShot(3,.38f);
            if(!motorcycleAudio&&(cues&SFVehicleCue.Ignition)!=0)VehicleOneShot(4,.27f);
            SetVehicleLoop(vehicleEngine,vehicleMix.EngineWanted||vehicleMix.EngineGain>.001f,ref engineStarted);
            SetVehicleLoop(vehicleTires,vehicleMix.TireGain>.001f,ref tiresStarted);
            RefreshVehicleAudio(false);
        }
        void SetVehicleLoop(AudioSource source,bool wanted,ref bool running)
        {
            if(wanted&&!running&&source.clip!=null){source.Play();running=true;}
            else if(!wanted&&running){source.Stop();running=false;}
        }
        void VehicleOneShot(int clip,float gain)
        {
            if(vehicleClips[clip]==null)return;
            int i=vehicleCursor++%vehicleShots.Length;var s=vehicleShots[i];s.Stop();s.clip=vehicleClips[clip];
            vehicleShotGains[i]=gain;s.volume=Mathf.Clamp01(Volume)*gain;s.mute=Volume<=0;s.Play();VehicleCueCount++;
        }
        void RefreshVehicleAudio(bool suspended)
        {
            if(!vehicleActive)return;
            if(suspended!=vehiclePaused)
            {
                vehiclePaused=suspended;
                if(suspended){vehicleEngine.Pause();vehicleTires.Pause();foreach(var s in vehicleShots)s.Pause();}
                else{vehicleEngine.UnPause();vehicleTires.UnPause();foreach(var s in vehicleShots)s.UnPause();}
            }
            float master=Mathf.Clamp01(Volume);
            vehicleEngine.volume=master*vehicleMix.EngineGain;vehicleEngine.pitch=vehicleMix.Pitch;
            vehicleTires.volume=master*vehicleMix.TireGain;
            vehicleEngine.mute=vehicleTires.mute=master<=0;
            for(int i=0;i<vehicleShots.Length;i++){vehicleShots[i].volume=master*vehicleShotGains[i];vehicleShots[i].mute=master<=0;}
        }
    }
}
