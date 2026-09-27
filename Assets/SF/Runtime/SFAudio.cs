using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // Licensed effects and compositions. Attribution is available in-game.
    public sealed partial class SFAudio : MonoBehaviour
    {
        public static readonly string[] TrackNames={"Cylinder One","Cylinder Seven","Cylinder Nine","Pressure"};
        static readonly string[] TrackFiles={"music_cylinder_01","music_cylinder_07","music_cylinder_09","pressure"};
        readonly Dictionary<string,AudioClip[]> clips=new Dictionary<string,AudioClip[]>();
        readonly Dictionary<string,int> last=new Dictionary<string,int>();
        readonly AudioSource[] voices=new AudioSource[16],music=new AudioSource[2];
        readonly AudioClip[] soundtrack=new AudioClip[4];
        AudioSource resultVoice;
        AudioClip victoryCue,defeatCue;
        internal AudioSource ResultVoice=>resultVoice;
        internal int ResultPlays {get;private set;}
        SFGame game; int cursor,deck,current=-1,queued=-1; bool started,paused,blending;
        float crossfade,duck;
        public float Volume=.65f,MusicVolume=.27f;
        public int MusicMode; // Auto, then each track. No combat-triggered song restarts.
        public int LoadedClips=>clips.Values.Sum(a=>a.Length);
        public int MusicTracks=>soundtrack.Count(c=>c!=null);
        public bool HasMusic=>soundtrack.Any(c=>c!=null);
        public string NowPlaying=>current<0?"":TrackNames[current]+(current==3?" / yd":" / Chris Zabriskie");
        public string MusicChoice=>MusicMode==0?"AUTO / LOCATION":TrackNames[MusicMode-1].ToUpperInvariant();
        public bool IsCrossfading=>blending;
        public string LastPlayed {get;private set;}
        public string LastClip {get;private set;}
        public int BankSize(string key)=>clips.TryGetValue(key,out var bank)?bank.Length:0;
        void Awake()
        {
            game=GetComponent<SFGame>();ReloadOptions();var all=Resources.LoadAll<AudioClip>("Audio");
            foreach(string key in new[]{"pistol","rifle","shotgun","step","grass","hit","heavy","cloth","cover","bodyhit","bodyheavy"})
                clips[key]=all.Where(c=>c.name.StartsWith(key)).OrderBy(c=>c.name).ToArray();
            for(int i=0;i<voices.Length;i++)voices[i]=Source();
            resultVoice=Source();
            victoryCue=Resources.Load<AudioClip>("Audio/result_win");
            defeatCue=Resources.Load<AudioClip>("Audio/result_fail");
            for(int i=0;i<2;i++){music[i]=Source();music[i].volume=0;music[i].loop=false;}
            for(int i=0;i<soundtrack.Length;i++)soundtrack[i]=Resources.Load<AudioClip>("Audio/"+TrackFiles[i]);
            InitVehicleAudio();
        }
        AudioSource Source(){var s=gameObject.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;return s;}
        public void StartAmbience(){started=true;}
        // Results must remain audible after world effects pause. This source never
        // enters the combat voice pool and is stopped before retry/menu transitions.
        public void StateChanged(SFState previous,SFState next,bool playResultCue=true)
        {
            if(previous==next)return;
            resultVoice.Stop();resultVoice.clip=null;
            if(!playResultCue||(next!=SFState.Won&&next!=SFState.Lost))return;
            foreach(var s in voices)s.Stop();
            resultVoice.clip=next==SFState.Won?victoryCue:defeatCue;
            resultVoice.volume=Volume*.75f;resultVoice.mute=Volume<=0;
            if(resultVoice.clip!=null){resultVoice.Play();ResultPlays++;duck=1.2f;}
        }
        public void CycleMusic(){MusicMode=(MusicMode+1)%5;OptionsChanged();StartAmbience();}
        public static int TrackForLocation(int route)=>new[]{1,2,0,2,1,2}[Mathf.Clamp(route,0,5)];
        public void Play(string key,float volume=1)=>PlayAt(key,game.Player!=null?game.Player.X:10,volume);
        public void PlayDamage(SFActor target,SFActor attacker,bool blocked)
        {
            bool melee=attacker!=null&&attacker.action!=SFAction.Shoot;
            bool heavy=attacker!=null&&(attacker.action==SFAction.Kick||attacker.action==SFAction.Sweep||attacker.action==SFAction.ClinchKnee||attacker.ComboAttack&&attacker.ComboMove.finisher);
            PlayAt(melee?(blocked?"bodyheavy":heavy?"bodyheavy":"bodyhit"):blocked?"guard":"hit",target.X,blocked?.32f:heavy?.64f:.56f);
        }
        public void PlayAt(string key,float x,float volume=1)
        {
            if(key=="shot")key="pistol";
            else if(key=="guard")key="heavy";
            else if(key=="collect"||key=="assist"||key=="dash"||key=="jump")key="cloth";
            if(!clips.TryGetValue(key,out var bank)||bank.Length==0)return;
            int old=last.TryGetValue(key,out var v)?v:-1;
            int index=bank.Length==1?0:(old+1+Random.Range(0,bank.Length-1))%bank.Length;
            last[key]=index;LastPlayed=key;LastClip=bank[index].name;
            float camera=game.Camera.transform.position.x,distance=Mathf.Abs(x-camera);
            if(distance>23)return;
            var source=voices.FirstOrDefault(s=>!s.isPlaying)??voices[cursor++%voices.Length];
            source.Stop();source.clip=bank[index];source.panStereo=Mathf.Clamp((x-camera)/13,-.8f,.8f);
            source.pitch=key=="pistol"||key=="rifle"||key=="shotgun"?1:Random.Range(.97f,1.03f);
            source.volume=Volume*Mathf.Clamp01(volume)*Mathf.Lerp(1,.18f,Mathf.Clamp01(distance/23));source.Play();
            if(key=="pistol"||key=="rifle"||key=="shotgun")duck=.25f;
        }
        void Update()
        {
            if(optionsDirty&&Time.realtimeSinceStartup>=saveOptionsAt)FlushOptions();
            bool shouldPause=game.State!=SFState.Playing;
            if(shouldPause!=paused)
            {paused=shouldPause;foreach(var s in voices)if(paused)s.Pause();else s.UnPause();}
            foreach(var s in voices)s.mute=Volume<=0;
            resultVoice.volume=Volume*.75f;resultVoice.mute=Volume<=0;
            RefreshVehicleAudio(game.State!=SFState.Playing||game.LayoutBlocked);
            if(!started&&game.State==SFState.Menu)StartAmbience();
            if(!started||!HasMusic)return;
            float dt=Time.unscaledDeltaTime;duck=Mathf.Max(0,duck-dt);
            float gain=Volume*MusicVolume*(duck>0?.55f:1)*(game.State==SFState.Paused?.6f:1);
            int wanted=MusicMode>0?MusicMode-1:game.State==SFState.Menu?0:TrackForLocation(game.RouteIndex);
            if(soundtrack[wanted]==null)wanted=System.Array.FindIndex(soundtrack,c=>c!=null);
            if(current<0){current=wanted;music[deck].clip=soundtrack[current];music[deck].Play();}
            if(!blending&&(wanted!=current||music[deck].time>=music[deck].clip.length-3f||!music[deck].isPlaying))
            {
                queued=wanted;music[1-deck].clip=soundtrack[queued];music[1-deck].time=0;
                music[1-deck].volume=0;music[1-deck].Play();blending=true;crossfade=0;
            }
            if(blending)
            {
                crossfade+=dt/3f;float t=Mathf.Clamp01(crossfade);
                music[deck].volume=gain*Mathf.Cos(t*Mathf.PI*.5f);music[1-deck].volume=gain*Mathf.Sin(t*Mathf.PI*.5f);
                if(t>=1){music[deck].Stop();deck=1-deck;current=queued;blending=false;}
            }
            else {music[deck].volume=Mathf.MoveTowards(music[deck].volume,gain,dt*.2f);music[1-deck].volume=0;}
        }
    }
}
