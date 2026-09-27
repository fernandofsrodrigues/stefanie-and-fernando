#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StefanieAndFernando
{
    // Actual game Update, keyboard events, window changes, physics and preference storage.
    public sealed class SFUsabilityCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();
        Keyboard keyboard;
        void Check(bool pass,string label){checks.Add((pass?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        IEnumerator Start()
        {
            var g=GetComponent<SFGame>();var hud=GetComponent<SFHUD>();yield return new WaitForSeconds(.4f);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();g.SmokeMode=false;
            hud.OpenAudioCredits();yield return Press(Key.Enter);
            Check(g.State==SFState.Menu&&hud.AudioCreditsOpen,"Enter behind title credits cannot start a mission");
            yield return Press(Key.F7);Check(!g.TouchControls,"credits consume gameplay shortcuts");
            yield return Press(Key.Escape);Check(g.State==SFState.Menu&&!hud.AudioCreditsOpen,"Escape closes title credits without starting play");
            yield return Press(Key.Enter);Check(g.State==SFState.Playing,"Enter still starts mission after credits close");
            hud.OpenAudioCredits();Check(g.State==SFState.Paused,"opening credits during play pauses the mission");
            float elapsed=g.Elapsed,x=g.Player.X;yield return new WaitForSeconds(.3f);
            Check(Mathf.Approximately(elapsed,g.Elapsed)&&Mathf.Abs(x-g.Player.X)<.01f,"credits freeze mission time and character physics");
            yield return Press(Key.Enter);Check(g.State==SFState.Paused&&hud.AudioCreditsOpen,"Enter cannot resume behind pause credits");
            yield return Press(Key.Escape);Check(g.State==SFState.Paused&&!hud.AudioCreditsOpen,"first Escape closes credits and keeps mission paused");
            yield return Press(Key.Escape);Check(g.State==SFState.Playing,"second deliberate Escape resumes mission");

            g.SmokeMode=true;g.EnableTouch("1");g.SmokeCommand=new SFCommand{move=Vector2.right};
            Screen.SetResolution(600,960,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);
            Check(g.LayoutBlocked&&g.State==SFState.Paused,"actual portrait window automatically pauses touch gameplay");
            elapsed=g.Elapsed;x=g.Player.X;yield return new WaitForSeconds(.25f);
            Check(Mathf.Approximately(elapsed,g.Elapsed)&&Mathf.Abs(x-g.Player.X)<.01f,"portrait warning freezes mission clock and movement");
            g.SetState(SFState.Playing);Check(g.State==SFState.Paused,"resume is blocked while portrait warning is visible");
            g.SetState(SFState.Menu);g.StartGame();Check(g.State==SFState.Menu,"portrait cannot silently start a mission from the title");
            g.SetState(SFState.Paused);Screen.SetResolution(1600,900,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);
            Check(!g.LayoutBlocked&&g.State==SFState.Paused,"returning to landscape requires explicit resume");
            g.SmokeCommand=default;g.SetState(SFState.Playing);Check(g.State==SFState.Playing,"landscape Continue resumes normally");
            g.SetState(SFState.Paused);g.EnableTouch("0");

            string[] keys={SFAudio.MasterKey,SFAudio.MusicKey,SFAudio.ModeKey};
            bool[] existed=keys.Select(PlayerPrefs.HasKey).ToArray();
            float master=PlayerPrefs.GetFloat(keys[0]),music=PlayerPrefs.GetFloat(keys[1]);int mode=PlayerPrefs.GetInt(keys[2]);
            int missions=PlayerPrefs.GetInt("SF.CompletedMissions");
            try
            {
                g.Audio.SetMasterVolume(.43f);g.Audio.SetMusicVolume(.18f);g.Audio.MusicMode=2;g.Audio.CycleMusic();g.Audio.FlushOptions(true);
                g.Audio.Volume=0;g.Audio.MusicVolume=0;g.Audio.MusicMode=0;g.Audio.ReloadOptions();
                Check(Mathf.Approximately(g.Audio.Volume,.43f)&&Mathf.Approximately(g.Audio.MusicVolume,.18f)&&g.Audio.MusicMode==3,"master, music and track choice round-trip through preferences");
                g.Audio.SetMasterVolume(0);g.Audio.SetMusicVolume(0);g.Audio.FlushOptions(true);g.Audio.ReloadOptions();
                Check(g.Audio.Volume==0&&g.Audio.MusicVolume==0,"saved silence remains silent on reload");
                PlayerPrefs.SetFloat(keys[0],float.NaN);PlayerPrefs.SetFloat(keys[1],9);PlayerPrefs.SetInt(keys[2],99);g.Audio.ReloadOptions();
                Check(g.Audio.Volume==.65f&&g.Audio.MusicVolume==.5f&&g.Audio.MusicMode==4&&g.Audio.MusicChoice=="PRESSURE","invalid saved options recover without breaking soundtrack selection");
                Check(PlayerPrefs.GetInt("SF.CompletedMissions")==missions,"audio preferences leave mission progress untouched");
            }
            finally
            {
                PlayerPrefs.SetFloat(keys[0],master);PlayerPrefs.SetFloat(keys[1],music);PlayerPrefs.SetInt(keys[2],mode);
                for(int i=0;i<keys.Length;i++)if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);
                PlayerPrefs.Save();g.Audio.ReloadOptions();InputSystem.RemoveDevice(keyboard);
            }
            Check(keys.Select(PlayerPrefs.HasKey).SequenceEqual(existed)&&(!existed[0]||Mathf.Approximately(PlayerPrefs.GetFloat(keys[0]),master))&&(!existed[1]||Mathf.Approximately(PlayerPrefs.GetFloat(keys[1]),music))&&(!existed[2]||PlayerPrefs.GetInt(keys[2])==mode),"test restores the player's original audio preferences");
            string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(qa);File.WriteAllLines(Path.Combine(qa,"usability-v071-checks.txt"),checks);
            Application.Quit(checks.Any(c=>c.StartsWith("FAIL"))?1:0);
        }
    }
}
#endif
