using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFHUD
    {
        Texture2D cover;
        void PartyMenu()
        {
            if(cover==null){var art=Resources.Load<SFArt>("SF/plate_title");if(art!=null)cover=art.Frame(0).texture;}
            if(cover!=null)GUI.DrawTexture(new Rect(0,0,width,900),cover,ScaleMode.ScaleAndCrop);
            if(game.TouchControls){TouchMenu();return;}
            GUI.DrawTexture(new Rect(0,0,Mathf.Min(width,1030),900),gradient);
            Box(42,48,44,3,game.Gold);Text("TOGETHER, THROUGH EVERYTHING",42,69,580,26,14,game.Gold,true);
            Text("STEFANIE\n& FERNANDO",38,117,600,150,58,white,true);
            Text("CHOOSE YOUR LEAD",42,280,570,24,12,muted,true);
            if(Button("FERNANDO",new Rect(42,314,258,43),false,game.Selected==0))game.Selected=0;
            if(Button("STEFANIE",new Rect(312,314,258,43),false,game.Selected==1))game.Selected=1;
            for(int i=0;i<2;i++)
            {
                var loadout=game.Loadouts[i];float x=42+i*270;
                Text(i==0?"FERNANDO'S EQUIPMENT":"STEFANIE'S EQUIPMENT",x,379,258,23,12,game.Teal,true);
                if(Button(loadout.outfit==0?"CIVILIAN":i==0?"DESERT MARPAT":"AIRBORNE OCP",new Rect(x,409,258,40)))loadout.outfit=1-loadout.outfit;
                if(Button(loadout.outfit==0?"BARE HEAD":loadout.helmet?"HELMET ON":"HELMET OFF",new Rect(x,456,258,38))&&loadout.outfit==1)loadout.helmet=!loadout.helmet;
                string[] gear={"LIGHT KIT","ARMOR + HEALTH","AMMO RESERVE"};
                if(Button(gear[loadout.gear],new Rect(x,501,258,38)))loadout.gear=(loadout.gear+1)%gear.Length;
                if(Button(loadout.sidearm==0?"SERVICE PISTOL":"HEAVY PISTOL",new Rect(x,546,258,38)))loadout.sidearm=1-loadout.sidearm;
                if(Button((loadout.startRifle?"START: ":"2: ")+(i==0?"SCAR-H":"MR762A1"),new Rect(x,587,258,27)))loadout.startRifle=!loadout.startRifle;
            }
            if(Button("<",new Rect(42,617,48,44)))game.SelectRoute(game.RouteIndex-1);
            Text(game.RouteName,99,624,360,31,22,white,true,TextAnchor.MiddleCenter);
            if(Button(">",new Rect(522,617,48,44)))game.SelectRoute(game.RouteIndex+1);
            if(Button(game.Heroic?"HEROIC  /  POWERFUL & FORGIVING":"STANDARD  /  STRONGER OPPOSITION",new Rect(42,675,528,41)))game.Heroic=!game.Heroic;
            if(Button("START MISSION   >",new Rect(42,735,528,62),true))game.StartGame();
            if(Button(game.GamepadActive?"CONTROLLER GUIDE":game.TouchControls?"TOUCH CONTROLS ON":"KEYBOARD / MOUSE  /  TOUCH OFF",new Rect(42,803,528,35)))
            {if(game.GamepadActive){PadHelpOpen=true;game.Pad.ResetHeld();}else game.EnableTouch(game.TouchControls?"0":"1");}
            if(game.GamepadActive)Text("STICK / D-PAD  NAVIGATE    A  SELECT    START  PLAY    VIEW  CONTROLS",590,812,width-620,34,14,game.Teal,true);
            Text("PLAYTEST  /  0.8.47",42,866,325,25,12,muted);
            if(Button("MUSIC & CREDITS",new Rect(350,851,220,40)))OpenAudioCredits();
            if(Button("FOREST DRIVE",new Rect(width-420,735,370,62)))game.StartDrive();
            if(Button("MOTORCYCLE RIDE",new Rect(width-420,655,370,62)))game.StartDrive(true);
            if(Button("CLASS PLATFORMER",new Rect(width-420,575,370,62)))game.ChooseCourse(true);
            Text("SIX LOCATIONS   /   "+CountMissions()+" CLEARED",width-420,57,370,26,14,white,true,TextAnchor.MiddleRight);
            Text("For Manu's birthday. With love.",width-440,862,390,25,16,white,false,TextAnchor.MiddleRight);
        }
        int CountMissions(){int n=0;for(int i=0;i<6;i++)if((game.CompletedMissions&(1<<i))!=0)n++;return n;}
        void PartyOverlay()
        {
            if(game.TouchControls||game.GamepadActive){TouchOverlay();return;}
            Box(0,0,width,900,new Color(.01f,.025f,.04f,.91f));float x=width/2-320;
            bool paused=game.State==SFState.Paused,won=game.State==SFState.Won;
            Text(paused?"TAKE A BREATH":won?"TOGETHER. STILL.":"REGROUP.",x,154,640,78,48,white,true,TextAnchor.MiddleCenter);
            Text(paused?game.RouteName:won?game.RouteName+"  /  MISSION CLEAR":"Your partner is counting on you.",x,247,640,38,19,game.Teal,false,TextAnchor.MiddleCenter);
            if(paused)
            {
                Text("SOUND",x+20,319,140,25,14,muted);game.Audio.SetMasterVolume(GUI.HorizontalSlider(new Rect(x+170,327,445,18),game.Audio.Volume,0,1));
                Text("MUSIC",x+20,362,140,25,14,muted);game.Audio.SetMusicVolume(GUI.HorizontalSlider(new Rect(x+170,370,445,18),game.Audio.MusicVolume,0,.5f));
                game.ShakeEnabled=GUI.Toggle(new Rect(x+20,411,590,30),game.ShakeEnabled,"  Camera impact motion",Style(18,white));
                bool swap=GUI.Toggle(new Rect(x+20,452,590,30),game.SwapThumbs,"  Swap mouse thumb buttons",Style(18,white));if(swap!=game.SwapThumbs)game.SetThumbSwap(swap);
                Text("DISPLAY   /   "+Screen.width+" x "+Screen.height,x+20,504,590,26,14,muted);
                if(Button("1080p",new Rect(x+20,540,185,42)))game.SetDisplay(1080);
                if(Button("1440p",new Rect(x+223,540,185,42)))game.SetDisplay(1440);
                if(Button("4K UHD",new Rect(x+427,540,188,42)))game.SetDisplay(2160);
                if(Button("MUSIC: "+game.Audio.MusicChoice,new Rect(x+20,588,595,32)))game.Audio.CycleMusic();
                if(Button("CONTINUE",new Rect(x+20,633,595,43),true))game.SetState(SFState.Playing);
                Text("Wheel zoom   /   HOME reset camera   /   F11 fullscreen",x,685,640,25,14,muted,false,TextAnchor.MiddleCenter);
            }
            else
            {
                Text("HOSTILES DEFEATED  "+game.KOs+"     /     BEST CHAIN  "+game.BestChain,x,326,640,35,19,white,true,TextAnchor.MiddleCenter);
                Text("TIME  "+Mathf.FloorToInt(game.Elapsed/60)+":"+((int)game.Elapsed%60).ToString("00")+"     /     MISSIONS CLEARED  "+CountMissions()+" OF 6",x,374,640,35,17,muted,false,TextAnchor.MiddleCenter);
                if(won&&Button("FOREST DRIVE",new Rect(x+20,549,595,60)))game.StartDrive();
                if(won){if(Button("TRAVEL TO NEXT LOCATION   >",new Rect(x+20,465,595,60),true))game.StartJourney();}
                else if(Button("RETRY CHECKPOINT",new Rect(x+20,465,595,60),true))game.RetryCheckpoint();
            }
            if(Button("MUSIC & CREDITS",new Rect(x+20,797,595,38)))OpenAudioCredits();
            if(Button("MISSION SELECT",new Rect(x+20,739,595,49)))game.ReturnToLoadout(game.RouteIndex);
            if(!Application.isEditor&&Application.platform!=RuntimePlatform.WebGLPlayer&&Button("QUIT",new Rect(width-145,820,105,42)))Application.Quit();
        }
    }
}
