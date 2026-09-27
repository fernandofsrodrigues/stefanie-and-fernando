using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFHUD
    {
        bool showAudioCredits;
        Vector2 creditsScroll;
        public bool AudioCreditsOpen=>showAudioCredits;
        public void OpenAudioCredits()
        {
            if(game.State==SFState.Playing)game.SetState(SFState.Paused);
            game.Touch.Reset();showAudioCredits=true;
        }
        public void CloseAudioCredits(){showAudioCredits=false;game.Touch.Reset();game.Pad.ResetHeld();}
        void AudioCredits()
        {
            Box(0,0,width,900,new Color(.015f,.025f,.035f,.98f));
            float w=Mathf.Min(940,width-72),x=(width-w)/2;
            Text("THE SOUND OF TOGETHER",x,55,w,55,36,white,true);
            Text(game.Audio.NowPlaying,x,124,w,32,20,game.Teal);
            bool touch=game.TouchControls||game.GamepadActive;
            if(Button("SOUNDTRACK: "+game.Audio.MusicChoice,new Rect(x,172,w,touch?76:44)))game.Audio.CycleMusic();
            if(touch)
            {
                Text("MUSIC LEVEL",x+110,261,w-220,62,18,muted,true,TextAnchor.MiddleCenter);
                if(Button("-",new Rect(x,253,92,76)))game.Audio.SetMusicVolume(game.Audio.MusicVolume-.05f);
                if(Button("+",new Rect(x+w-92,253,92,76)))game.Audio.SetMusicVolume(game.Audio.MusicVolume+.05f);
            }
            else
            {
                Text("MUSIC LEVEL",x,240,180,28,14,muted);
                game.Audio.SetMusicVolume(GUI.HorizontalSlider(new Rect(x+190,248,w-190,18),game.Audio.MusicVolume,0,.5f));
            }
            var credits=Resources.Load<TextAsset>("AudioCredits");
            string body=credits!=null?credits.text:"See Audio-Credits-v0.7.md in the game folder.";
            float paneWidth=touch?w-138:w,paneHeight=touch?337:417;
            float contentHeight=Mathf.Max(paneHeight,Style(17,white).CalcHeight(new GUIContent(body),paneWidth-40)+24);
            if(touch)
            {
                if(Button("UP",new Rect(x+w-112,367,112,100)))creditsScroll.y=Mathf.Max(0,creditsScroll.y-240);
                if(Button("DOWN",new Rect(x+w-112,517,112,100)))creditsScroll.y=Mathf.Min(contentHeight-paneHeight,creditsScroll.y+240);
            }
            creditsScroll=GUI.BeginScrollView(new Rect(x,touch?343:292,paneWidth,paneHeight),creditsScroll,new Rect(0,0,paneWidth-28,contentHeight));
            Text(body,0,0,paneWidth-40,contentHeight,17,white);
            GUI.EndScrollView();
            if(Button("ARTIST & TRACKS",new Rect(x,touch?698:735,w*.48f,touch?76:42)))Application.OpenURL("https://chriszabriskie.com/cylinders/");
            if(Button("CC BY 4.0 LICENSE",new Rect(x+w*.52f, touch?698:735,w*.48f,touch?76:42)))Application.OpenURL("https://creativecommons.org/licenses/by/4.0/");
            if(Button("BACK",new Rect(x,802,w,touch?76:50),true))CloseAudioCredits();
        }
    }
}
