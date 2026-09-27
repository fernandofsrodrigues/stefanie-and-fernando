using System;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFAudio
    {
        internal const string MasterKey="SF.audio.master",MusicKey="SF.audio.music",ModeKey="SF.audio.mode";
        // Automated executable checks must never replace the player's preferences.
        readonly bool suppressOptions=Environment.GetCommandLineArgs().Any(a=>a.StartsWith("-sf",StringComparison.Ordinal));
        bool optionsDirty;
        float saveOptionsAt;
        static float SafeLevel(float value,float maximum,float fallback)=>float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,0,maximum);
        internal void ReloadOptions()
        {
            Volume=SafeLevel(PlayerPrefs.GetFloat(MasterKey,.65f),1,.65f);
            MusicVolume=SafeLevel(PlayerPrefs.GetFloat(MusicKey,.27f),.5f,.27f);
            MusicMode=Mathf.Clamp(PlayerPrefs.GetInt(ModeKey,0),0,4);
            optionsDirty=false;
        }
        public void SetMasterVolume(float value)
        {
            value=SafeLevel(value,1,.65f);if(Mathf.Approximately(Volume,value))return;
            Volume=value;OptionsChanged();
        }
        public void SetMusicVolume(float value)
        {
            value=SafeLevel(value,.5f,.27f);if(Mathf.Approximately(MusicVolume,value))return;
            MusicVolume=value;OptionsChanged();
        }
        void OptionsChanged(){if(suppressOptions)return;optionsDirty=true;saveOptionsAt=Time.realtimeSinceStartup+.75f;}
        internal void FlushOptions(bool force=false)
        {
            if(!force&&(!optionsDirty||suppressOptions))return;
            PlayerPrefs.SetFloat(MasterKey,Volume);PlayerPrefs.SetFloat(MusicKey,MusicVolume);PlayerPrefs.SetInt(ModeKey,MusicMode);
            PlayerPrefs.Save();optionsDirty=false;
        }
        void OnApplicationFocus(bool focused){if(!focused)FlushOptions();}
        void OnApplicationPause(bool paused){if(paused)FlushOptions();}
        void OnApplicationQuit()=>FlushOptions();
    }
}
