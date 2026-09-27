using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFHUD
    {
        void TouchMenu()
        {
            Box(0,0,width,900,new Color(.01f,.025f,.04f,.77f));
            Text("STEFANIE & FERNANDO",40,50,width-80,62,42,white,true);
            Text("TOGETHER, THROUGH EVERYTHING",42,122,width-84,25,16,game.Gold,true);
            float left=40,w=480,right=width-520;
            Text("CHOOSE YOUR LEAD",left,191,w,30,18,game.Teal,true);
            if(Button("FERNANDO",new Rect(left,231,234,76),false,game.Selected==0))game.Selected=0;
            if(Button("STEFANIE",new Rect(left+246,231,234,76),false,game.Selected==1))game.Selected=1;
            if(Button("<",new Rect(left,331,76,76)))game.SelectRoute(game.RouteIndex-1);
            Text(game.RouteName,left+84,331,w-168,76,22,white,true,TextAnchor.MiddleCenter);
            if(Button(">",new Rect(left+w-76,331,76,76)))game.SelectRoute(game.RouteIndex+1);
            if(Button(game.Heroic?"HEROIC / FORGIVING":"STANDARD / MORE RESISTANCE",new Rect(left,431,w,76)))game.Heroic=!game.Heroic;
            if(Button("START MISSION  >",new Rect(left,531,w,88),true))game.StartGame();
            if(Button("MUSIC & CREDITS",new Rect(left,643,234,76)))OpenAudioCredits();
            if(Button("FOREST DRIVE",new Rect(left+246,643,234,76)))game.StartDrive();
            if(Button("MOTORCYCLE RIDE",new Rect(left,743,234,76)))game.StartDrive(true);
            if(Button("KEYBOARD / MOUSE",new Rect(left+246,743,234,76)))game.EnableTouch("0");
            for(int i=0;i<2;i++)
            {
                var kit=game.Loadouts[i];float x=right+i*246;
                Text(i==0?"FERNANDO'S KIT":"STEFANIE'S KIT",x,191,234,30,18,game.Teal,true,TextAnchor.MiddleCenter);
                if(Button(kit.outfit==0?"CIVILIAN":i==0?"DESERT MARPAT":"AIRBORNE OCP",new Rect(x,231,234,76)))kit.outfit=1-kit.outfit;
                if(Button(kit.outfit==0?"BARE HEAD":kit.helmet?"HELMET ON":"HELMET OFF",new Rect(x,331,234,76))&&kit.outfit==1)kit.helmet=!kit.helmet;
                string[] gear={"LIGHT KIT","ARMOR + HEALTH","AMMO RESERVE"};
                if(Button(gear[kit.gear],new Rect(x,431,234,76)))kit.gear=(kit.gear+1)%gear.Length;
                if(Button(kit.sidearm==0?"SERVICE PISTOL":"HEAVY PISTOL",new Rect(x,531,234,76)))kit.sidearm=1-kit.sidearm;
                if(Button((kit.startRifle?"START: ":"CARRY: ")+(i==0?"SCAR-H":"MR762A1"),new Rect(x,631,234,76)))kit.startRifle=!kit.startRifle;
            }
            Text("Stick: move / push farther to run\nWEAPON cycles unarmed, pistol and rifle.\nRELOAD refills your magazine from reserve.\nMR762A1: tap FIRE for each shot.",right,736,480,100,18,white);
            Text("PLAYTEST  /  0.8.47",40,854,480,30,15,muted);
            if(Button("CLASS PLATFORMER",new Rect(right,112,480,64)))game.ChooseCourse(true);
            Text("SIX LOCATIONS  /  "+CountMissions()+" CLEARED",right,854,480,30,15,game.Gold,true,TextAnchor.MiddleRight);
        }
        void TouchOverlay()
        {
            Box(0,0,width,900,new Color(.01f,.025f,.04f,.94f));
            bool paused=game.State==SFState.Paused,won=game.State==SFState.Won;
            Text(paused?"TAKE A BREATH":won?"TOGETHER. STILL.":"REGROUP.",40,75,width-80,70,46,white,true,TextAnchor.MiddleCenter);
            Text(game.RouteName,40,161,width-80,36,22,game.Teal,true,TextAnchor.MiddleCenter);
            float x=width/2-488;
            if(paused)
            {
                Text("SOUND",x,231,440,32,18,muted);
                if(Button("-",new Rect(x,273,100,76)))game.Audio.SetMasterVolume(game.Audio.Volume-.1f);
                Text(Mathf.RoundToInt(game.Audio.Volume*100)+"%",x+112,273,220,76,26,white,true,TextAnchor.MiddleCenter);
                if(Button("+",new Rect(x+344,273,100,76)))game.Audio.SetMasterVolume(game.Audio.Volume+.1f);
                Text("MUSIC",x,389,440,32,18,muted);
                if(Button("-",new Rect(x,431,100,76)))game.Audio.SetMusicVolume(game.Audio.MusicVolume-.05f);
                Text(Mathf.RoundToInt(game.Audio.MusicVolume*200)+"%",x+112,431,220,76,26,white,true,TextAnchor.MiddleCenter);
                if(Button("+",new Rect(x+344,431,100,76)))game.Audio.SetMusicVolume(game.Audio.MusicVolume+.05f);
                if(Button("MUSIC: "+game.Audio.MusicChoice,new Rect(x,549,444,76)))game.Audio.CycleMusic();
                if(Button("CAMERA MOTION: "+(game.ShakeEnabled?"ON":"OFF"),new Rect(x,667,444,76)))game.ShakeEnabled=!game.ShakeEnabled;
            }
            else
            {
                Text("HOSTILES DEFEATED  "+game.KOs+"\nBEST CHAIN  "+game.BestChain+"\nTIME  "+Mathf.FloorToInt(game.Elapsed/60)+":"+((int)game.Elapsed%60).ToString("00"),x,295,444,200,24,white,true);
            }
            if(won&&Button("FOREST DRIVE",new Rect(x,549,444,76)))game.StartDrive();
            float r=x+532;
            if(Button(paused?"CONTINUE":won?"TRAVEL TO NEXT LOCATION >":"RETRY CHECKPOINT",new Rect(r,273,444,94),true))
            {if(paused)game.SetState(SFState.Playing);else if(won)game.StartJourney();else game.RetryCheckpoint();}
            if(Button("MISSION SELECT",new Rect(r,431,444,76)))game.ReturnToLoadout(game.RouteIndex);
            if(Button("MUSIC & CREDITS",new Rect(r,549,444,76)))OpenAudioCredits();
            if(Application.platform==RuntimePlatform.WebGLPlayer)
            {if(Button("FULL SCREEN",new Rect(r,667,444,76)))Screen.fullScreen=!Screen.fullScreen;}
            else if(!Application.isEditor&&Button("QUIT GAME",new Rect(r,667,444,76)))Application.Quit();
            Text(game.GamepadActive?"STICK / D-PAD: navigate  |  A: select  |  B / START: resume  |  VIEW: controls":"INTERACT: supplies / revive  |  BACKUP: local support  |  SWITCH: change lead",40,813,width-80,44,17,muted,false,TextAnchor.MiddleCenter);
        }
    }
}
