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
    public sealed class SFRifleCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();SFGame g;
        void Check(bool pass,string label){checks.Add((pass?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return new WaitForSeconds(.5f);
            Check(g.Audio.MusicTracks==4&&g.Audio.LoadedClips==34&&g.Audio.BankSize("bodyhit")==5&&g.Audio.BankSize("bodyheavy")==5,"four licensed compositions, 24 retained effects and ten body-impact derivatives load");
            Check(Resources.Load<TextAsset>("AudioCredits").text.Contains("Creative Commons Attribution 4.0"),"attribution included in runtime resources");
            foreach(var id in new[]{"fernando","stefanie"})foreach(var kind in new[]{"field_rifle","field_rifle_walk"})
            {var a=Resources.Load<SFArt>("SF/"+id+"_"+kind);Check(a!=null&&a.frames.Length==8&&a.frames.All(f=>f!=null),id+" "+kind+" complete atlas");}
            foreach(var l in g.Loadouts){l.outfit=1;l.startRifle=true;}
            g.StartGame();yield return new WaitForSeconds(.2f);
            foreach(var a in g.Actors.Where(a=>!a.hero)){a.Teleport(new Vector2(130,0));a.cooldown=999;}
            foreach(var a in new[]{g.Fernando,g.Stefanie}){a.Teleport(new Vector2(a==g.Fernando?8:6,0));a.lane=-.85f;a.invulnerable=100;}
            Check(g.Fernando.Armed&&g.Fernando.Rifle&&g.Stefanie.Rifle,"both loadouts can start with signature rifle");
            g.Selected=0;int ammo=g.Ammo;g.SmokeCommand=new SFCommand{shoot=true};yield return new WaitForSeconds(1.05f);g.SmokeCommand=default;
            Check(ammo-g.Ammo>=3,"holding SCAR trigger delivers sustained fire");yield return new WaitForSeconds(.5f);
            g.Selected=1;g.Stefanie.Armed=g.Stefanie.Rifle=true;ammo=g.Ammo;
            g.SmokeCommand=new SFCommand{shoot=true};yield return new WaitForSeconds(1.05f);
            Check(ammo-g.Ammo==1,"holding MR762 trigger delivers exactly one shot");
            g.SmokeCommand=default;yield return new WaitForSeconds(.12f);g.SmokeCommand=new SFCommand{shoot=true};yield return new WaitForSeconds(.2f);g.SmokeCommand=default;
            Check(ammo-g.Ammo==2,"release and new press deliver the next MR762 shot");yield return new WaitForSeconds(.5f);
            g.Player.RifleAmmo.Clear();g.SmokeCommand=new SFCommand{shoot=true};yield return new WaitForSeconds(.2f);g.SmokeCommand=default;
            Check(g.Player.RifleAmmo.Total==0&&g.Player.action!=SFAction.Shoot,"empty rifle cannot fire or make ammo negative");g.Player.ResetWeapons();
            foreach(var a in new[]{g.Fernando,g.Stefanie})
            {
                g.Selected=a==g.Fernando?0:1;a.Armed=a.Rifle=true;a.action=SFAction.Idle;a.cooldown=0;
                g.SmokeCommand=new SFCommand{move=Vector2.right,run=true};var frames=new HashSet<int>();
                for(int i=0;i<12;i++){yield return new WaitForSeconds(.07f);if(a.CurrentSheet=="field_rifle_rig")frames.Add(a.CurrentFrame);}
                Check(frames.Count>=3,a.identity+" travelled distance advances military locomotion frames");g.SmokeCommand=default;yield return new WaitForSeconds(.2f);
                a.Armed=a.Rifle=true;a.Helmet=true;a.Aiming=true;a.Crouching=false;a.Render(0);
                Check(a.CurrentSheet=="field_rifle"&&a.CurrentFrame==1,a.identity+" aims signature rifle in military uniform");
                a.Helmet=false;a.Crouching=true;a.Render(0);Check(a.CurrentFrame==7,a.identity+" bare-head kneeling rifle pose");
                a.FieldUniform=false;a.Render(0);Check(a.CurrentSheet=="civil_rifle"&&a.CurrentFrame==3,a.identity+" civilian crouch retains signature rifle");a.FieldUniform=true;
                a.Helmet=true;a.Crouching=false;a.Render(0);
            }
            g.SmokeCommand=new SFCommand{aim=true};g.Capture("v07-rifles");yield return new WaitForSeconds(.1f);g.SmokeCommand=default;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var screen=InputSystem.AddDevice<Touchscreen>();screen.MakeCurrent();g.Touch.Reset();
            Vector2 Pixel(Vector2 p)=>new Vector2(p.x,900-p.y)*Screen.height/900f;
            float width=Screen.width*900f/Screen.height;
            InputSystem.QueueStateEvent(screen,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=Pixel(SFTouchInput.StickCenter)});InputSystem.Update();g.Touch.Read(g.Player,width);
            InputSystem.QueueStateEvent(screen,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=Pixel(SFTouchInput.StickCenter+Vector2.right*89)});
            InputSystem.QueueStateEvent(screen,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Began,position=Pixel(SFTouchInput.Control(0,width).center)});InputSystem.Update();
            var input=g.Touch.Read(g.Player,width);Debug.Log("Touch diagnostics "+input.move+" run="+input.run+" shoot="+input.shoot+" armed="+g.Player.Armed+" "+string.Join(" | ",screen.touches.Where(t=>t.press.isPressed).Select(t=>t.touchId.ReadValue()+":"+t.position.ReadValue()+":"+t.press.wasPressedThisFrame)));Check(input.move.x>.9f&&input.run&&input.shoot,"two independent contacts can run and fire together");
            InputSystem.QueueStateEvent(screen,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=Pixel(SFTouchInput.StickCenter)});InputSystem.Update();input=g.Touch.Read(g.Player,width);
            Check(input.move==Vector2.zero&&input.shoot,"releasing joystick keeps the other finger firing");
            InputSystem.QueueStateEvent(screen,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Canceled});InputSystem.Update();input=g.Touch.Read(g.Player,width);
            Check(!input.shoot&&input.move==Vector2.zero,"canceled contact releases action without stuck input");InputSystem.RemoveDevice(screen);
            g.Audio.MusicMode=3;yield return new WaitForSeconds(6.5f);Check(g.Audio.NowPlaying.StartsWith("Cylinder Nine")&&!g.Audio.IsCrossfading,"music selector completes crossfade to chosen track");
            var music=g.GetComponents<AudioSource>().First(s=>s.clip!=null&&s.clip.name=="music_cylinder_09"&&s.isPlaying);
            g.SetState(SFState.Paused);float t=music.time;yield return new WaitForSeconds(.25f);Check(music.isPlaying&&music.time>t,"pause keeps quiet music continuous");
            g.Audio.MusicVolume=0;yield return new WaitForSeconds(1);Check(g.GetComponents<AudioSource>().Where(s=>s.clip!=null&&(s.clip.name.StartsWith("music_")||s.clip.name=="pressure")).All(s=>s.volume<.001f),"music control mutes both crossfade decks");
            string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));File.WriteAllLines(Path.Combine(qa,"rifle-v07-checks.txt"),checks);
            Application.Quit(checks.Any(c=>c.StartsWith("FAIL"))?1:0);
        }
    }
}
#endif
