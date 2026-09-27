using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StefanieAndFernando
{
    public sealed partial class SFDriveCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();SFGame g;
        bool Web=>Application.platform==RuntimePlatform.WebGLPlayer;
        void Check(bool ok,string label){checks.Add((ok?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void Tick(SFDriveModel model,int steps,SFCommand c){for(int i=0;i<steps;i++)model.Tick(.05f,c,false);}
        IEnumerator View(string name)
        {
            // Hold the exact sampled frame while the browser/native image is inspected.
            // Smoke input alone still advances SFGame.Update and would expire a brief muzzle flash.
            bool enabled=g.enabled;g.enabled=false;Debug.Log("DRIVE QA VIEW: "+name);
            if(!Web)g.Capture("v0846-road-"+name);yield return new WaitForSeconds(Web?1.5f:.05f);g.enabled=enabled;
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return null;
            CheckVehicleMix();
            CheckDriveReloadTiming();yield return CheckDriveWeaponVisuals();
            CheckPursuitMotion();yield return CheckPursuitVisuals();
            CheckGunnerRecoilClock();yield return CheckGunnerRecoilVisuals();
            var throttle=new SFCommand{punch=true};var m=new SFDriveModel();
            Check(m.Phase==SFDrivePhase.Ready&&m.Distance==0&&m.Integrity==100,"road starts parked with full condition");
            Tick(m,40,throttle);Check(m.Distance==0,"throttle cannot skip boarding");
            Check(m.Board()&&!m.Board(),"boarding starts once only");
            Tick(m,20,throttle);Check(m.Phase==SFDrivePhase.Boarding&&m.Distance==0,"boarding holds vehicle parked");
            Tick(m,20,default);Check(m.Phase==SFDrivePhase.Driving,"boarding reaches driving");
            Tick(m,65,throttle);Check(m.Speed==SFDriveModel.MaxSpeed&&m.Distance>0&&m.WheelAngle<0,"acceleration moves vehicle and wheels to capped speed");
            float d=m.Distance,clock=m.Clock;m.Tick(.25f,throttle,true);Check(m.Distance==d&&m.Clock==clock,"pause freezes distance and phase clock");
            Tick(m,20,new SFCommand{crouch=true,shoot=true});Check(m.Speed==0,"brake takes priority over throttle");
            Tick(m,100,new SFCommand{move=Vector2.up});Check(m.Lane==1,"north steering clamps inside road");
            Tick(m,100,new SFCommand{move=Vector2.down});Check(m.Lane==-1,"south steering clamps inside road");
            var safe=new SFDriveModel(false);safe.Board();Tick(safe,40,default);Tick(safe,1100,throttle);
            Check(safe.Phase==SFDrivePhase.Complete&&safe.Speed==0&&safe.Distance==480,"unobstructed route auto brakes and completes dismount");
            Check(safe.Integrity==100&&safe.Impacts==0,"depth-separated barriers do not damage the pickup");
            Check(safe.Checkpoint==240&&safe.SceneryBlend==1,"halfway checkpoint and arrival scenery reached");
            d=safe.Distance;Tick(safe,50,throttle);Check(safe.Distance==d&&!safe.Board()&&!safe.Retry(),"completion cannot restart or overshoot route");
            Check(SFDriveModel.BarrierDistances.Length==2,"only two rare roadwork obstacles across the route");
            var hit=new SFDriveModel(false);hit.Board();Tick(hit,40,default);
            hit.Enemies.Add(new SFConvoyEnemy(0,0,0));hit.Enemies.Add(new SFConvoyEnemy(1,0,1));hit.Enemies.Add(new SFConvoyEnemy(2,0,-1));
            foreach(var mag in hit.Magazines)mag.Clear();
            Tick(hit,700,default);
            Check(hit.Phase==SFDrivePhase.Recovery&&hit.Integrity==0,"hostile fire can stop an unprotected stationary vehicle");
            Check(hit.HostileShots>0,"announced hostile fire delivers actual damage");
            Check(hit.Retry()&&hit.Distance==0&&hit.Integrity==100&&hit.Magazine.Loaded>0,"recovery restores a playable checkpoint and finite ammunition");
            var ai=new SFDriveModel();ai.SetRole(SFDriveRole.Gunner);ai.Board();Tick(ai,1150,default);
            Check(ai.Phase==SFDrivePhase.Complete&&ai.Impacts==0,"AI driver traverses three lanes and avoids roadwork without player driving");
            Check(ai.Integrity>0&&ai.HostileShots>0,"AI driver responds to announced fire while preserving a survivable escape");
            Check(ai.Fired==0&&ai.Escaped==6,"AI driver does not secretly fire the player gun");
            var gun=new SFDriveModel(false);gun.SetRole(SFDriveRole.Gunner);gun.Board();Tick(gun,40,default);
            gun.Enemies.Add(new SFConvoyEnemy(0,0,0));Tick(gun,80,default);gun.SelectTarget(0);
            int total=gun.Magazine.Total;
            Tick(gun,5,new SFCommand{shoot=true});
            Check(gun.Fired>=2&&gun.Magazine.Total==total-gun.Fired,"held SCAR fire consumes exactly one round per shot");
            int fired=gun.Fired;gun.SetWeapon(0);Tick(gun,5,new SFCommand{shoot=true});
            Check(gun.Fired==fired,"weapon switch requires trigger release and does not leak held shots");
            Tick(gun,2,default);
            // Bring the target within pistol range without changing its health or ownership.
            gun.Enemies[0].X=0;gun.Enemies[0].Health=100;
            Tick(gun,1,new SFCommand{shoot=true});fired=gun.Fired;Tick(gun,8,new SFCommand{shoot=true});
            Check(gun.Fired==fired,"pistol requires a new press for each shot");
            Tick(gun,1,default);gun.Enemies[0].X=0;Tick(gun,1,new SFCommand{shoot=true});
            Check(gun.Fired==fired+1,"release and press delivers another pistol shot");
            total=gun.Magazine.Total;Check(gun.Reload(),"partial magazine can be reloaded");
            float reload=gun.ReloadTime;gun.Tick(.2f,default,true);Check(gun.ReloadTime==reload,"pause freezes reload completion");
            fired=gun.Fired;Tick(gun,5,new SFCommand{shoot=true});Check(gun.Fired==fired,"reload blocks firing");
            gun.SetWeapon(1);Check(!gun.Reloading&&gun.Magazines[0].Total==total,"weapon swap cancels reload without duplicating ammunition");
            gun.SetWeapon(0);gun.Reload();Tick(gun,35,default);
            Check(gun.Magazine.Loaded==17&&gun.Magazine.Total==total,"completed reload conserves magazine plus reserve");
            fired=gun.Fired;gun.SetRole(SFDriveRole.Driver);gun.SetRole(SFDriveRole.Gunner);Tick(gun,4,new SFCommand{shoot=true});
            Check(gun.Fired==fired,"role transfer cannot inherit a held trigger");
            var pistolRun=new SFDriveModel();pistolRun.SetRole(SFDriveRole.Gunner);pistolRun.SetWeapon(0);pistolRun.Board();
            for(int frame=0;frame<2100;frame++)pistolRun.Tick(.05f,new SFCommand{shoot=frame%9==0,reload=pistolRun.Magazine.Loaded==0},false);
            Check(pistolRun.Phase==SFDrivePhase.Complete&&pistolRun.Defeated>=4,"pistol gunner role has reachable targets and a complete playable route");
            var buddy=new SFDriveModel();buddy.Board();Tick(buddy,1100,throttle);
            Check(buddy.Phase==SFDrivePhase.Complete&&buddy.Defeated>=4&&buddy.Fired>0,"AI gunner protects driver and finishes multiple pursuit encounters");
            Check(buddy.Magazines[1].Total<120,"AI gunner consumes the same finite rifle ammunition");
            var small=new SFDriveModel();var large=new SFDriveModel();small.Board();large.Board();Tick(small,40,default);Tick(large,40,default);
            for(int i=0;i<200;i++)small.Tick(.01f,throttle,false);for(int i=0;i<8;i++)large.Tick(.25f,throttle,false);
            Check(Mathf.Abs(small.Distance-large.Distance)<.1f&&Mathf.Abs(small.Speed-large.Speed)<.01f,"substeps keep slow-frame acceleration consistent");
            foreach(string id in new[]{"vehicle_pickup_drive","vehicle_pickup_board","plate_serra_convoy","plate_serra_overlook_hd","vehicle_pickup_guns","vehicle_pursuit"})
            {var a=Resources.Load<SFArt>("SF/"+id);Check(a!=null&&a.frames.Length==(id.EndsWith("board")||id.EndsWith("guns")||id.EndsWith("pursuit")?2:1)&&a.frames.All(f=>f!=null),id+" imports complete");}
            var hd=Resources.Load<SFArt>("SF/plate_serra_overlook_hd").Frame(0);
            Check(hd.texture.width==4096&&hd.texture.height==1365,"reviewed 4x coastal source imports at a bounded 4096-pixel runtime size");
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var key=InputSystem.AddDevice<Keyboard>();key.MakeCurrent();var mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(key,new KeyboardState(Key.D,Key.W));InputSystem.Update();var c=SFInput.Read(false,false);
            Check(c.move==Vector2.one,"D and W supply independent horizontal and north lane input");
            InputSystem.QueueStateEvent(key,new KeyboardState(Key.A,Key.S));InputSystem.Update();c=SFInput.Read(true,false);Check(c.move==new Vector2(-1,-1),"A and S supply backward positioning and south lane input while armed");
            InputSystem.QueueStateEvent(key,new KeyboardState(Key.E));InputSystem.Update();Check(SFInput.Read(false,false).interact,"E maps to board and exit");
            InputSystem.QueueStateEvent(key,new KeyboardState());InputSystem.Update();
            var pad=InputSystem.AddDevice<Gamepad>();pad.MakeCurrent();var map=new SFGamepadInput();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));InputSystem.Update();map.Poll();InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();map.Poll();
            InputSystem.QueueStateEvent(pad,new GamepadState{rightTrigger=1,leftStick=Vector2.up});InputSystem.Update();map.Poll();c=map.Read(false,false,false,.05f);Check(c.punch&&c.move.y>.99f,"controller RT and stick steer road with unarmed loadout");
            c=map.Read(true,true,false,.05f);Check(c.shoot,"controller throttle also works with rifle loadout");
            InputSystem.QueueStateEvent(pad,new GamepadState{leftTrigger=1});InputSystem.Update();map.Poll();Check(map.Read(true,true,false,.05f).crouch,"controller LT brakes");
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));InputSystem.Update();map.Poll();Check(map.Read(false,false,false,.05f).jump,"controller A boards and exits");
            InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(key);InputSystem.RemoveDevice(mouse);
            var screen=InputSystem.AddDevice<Touchscreen>();screen.MakeCurrent();float width=Screen.width*900f/Screen.height;int contact=100;
            foreach(bool armed in new[]{false,true})foreach(int control in new[]{0,2,3,6,8,13,15})
            {
                g.Player.Armed=armed;Vector2 at=SFTouchInput.Control(control,width).center;at=new Vector2(at.x,900-at.y)*Screen.height/900f;
                InputSystem.QueueStateEvent(screen,new TouchState{touchId=++contact,phase=UnityEngine.InputSystem.TouchPhase.Began,position=at});InputSystem.Update();c=g.Touch.Read(g.Player,width,true);
                Check(control==0?c.shoot:control==2?c.crouch:control==6?c.cycleWeapon:control==8?c.swap:control==13?c.interact:control==15?c.reload:!c.jump&&!c.interact&&c.weapon==0,"road touch "+control+" respects visible controls with armed="+armed);
                InputSystem.QueueStateEvent(screen,new TouchState{touchId=contact,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=at});InputSystem.Update();g.Touch.Read(g.Player,width,true);
            }
            InputSystem.RemoveDevice(screen);g.Player.Armed=false;
            var pos=g.Player.transform.position;var camera=g.Camera.transform.position;float health=g.Player.health,elapsed=g.Elapsed;int ammo=g.Player.TotalAmmo,completed=g.CompletedMissions;
            Check(g.StartDrive()&&g.DriveActive&&g.State==SFState.Playing,"title can enter optional road");
            int sourceCount=g.GetComponents<AudioSource>().Length;
            var vehicleSources=g.Audio.VehicleSources;
            Check(g.Audio.VehicleClipCount==5&&vehicleSources.Length==6&&vehicleSources.All(s=>s!=null),"five vehicle clips load into six dedicated sources");
            Check(g.Audio.VehicleActive&&g.Audio.VehicleCueCount==0&&!vehicleSources.Any(s=>s.isPlaying),"ready pickup is silent before boarding");
            Check(vehicleSources.Take(2).All(s=>s.clip!=null&&s.loop&&s.clip.frequency==48000&&s.clip.channels==1&&s.clip.length>2),"engine and tire loops import as bounded mono 48 kHz clips");
            Check(!g.StartDrive()&&!g.World.gameObject.activeSelf&&g.Actors.All(a=>!a.motor.Body.simulated),"road prevents duplicate entry and freezes all foot actors");
            var roadKey=InputSystem.AddDevice<Keyboard>();roadKey.MakeCurrent();var roadMouse=InputSystem.AddDevice<Mouse>();roadMouse.MakeCurrent();
            InputSystem.QueueStateEvent(roadMouse,new MouseState{position=new Vector2(800,450)});InputSystem.QueueStateEvent(roadKey,new KeyboardState(Key.Tab));InputSystem.Update();
            c=g.ReadDriveCommand();Check(g.Drive.Role==SFDriveRole.Gunner&&!c.shoot,"actual keyboard TAB transfers role and clears firing");
            InputSystem.QueueStateEvent(roadMouse,new MouseState{position=Vector2.zero});InputSystem.QueueStateEvent(roadKey,new KeyboardState(Key.F));InputSystem.Update();
            Check(g.ReadDriveCommand().shoot,"keyboard F fires independently of a parked mouse over HUD");
            InputSystem.QueueStateEvent(roadKey,new KeyboardState());InputSystem.QueueStateEvent(roadMouse,new MouseState{position=Vector2.zero}.WithButton(MouseButton.Left));InputSystem.Update();
            Check(!g.ReadDriveCommand().shoot,"mouse click on HUD cannot fire vehicle weapon");
            InputSystem.QueueStateEvent(roadMouse,new MouseState{position=new Vector2(800,450)}.WithButton(MouseButton.Left));InputSystem.Update();
            Check(g.ReadDriveCommand().shoot,"mouse primary fires inside the road play area");
            InputSystem.QueueStateEvent(roadMouse,new MouseState());InputSystem.QueueStateEvent(roadKey,new KeyboardState(Key.R,Key.Digit1));InputSystem.Update();
            c=g.ReadDriveCommand();Check(c.reload&&c.weapon==1,"actual road keyboard keeps reload and pistol selection separate");
            InputSystem.QueueStateEvent(roadMouse,new MouseState{position=new Vector2(800,450)}.WithButton(MouseButton.Left));InputSystem.QueueStateEvent(roadKey,new KeyboardState());InputSystem.Update();
            g.SwitchDriveRole();g.SwitchDriveRole();Check(!g.ReadDriveCommand().shoot,"actual role switch suppresses an already-held mouse trigger");
            InputSystem.QueueStateEvent(roadMouse,new MouseState{position=new Vector2(800,450)});InputSystem.Update();g.ReadDriveCommand();
            InputSystem.QueueStateEvent(roadMouse,new MouseState{position=new Vector2(800,450)}.WithButton(MouseButton.Left));InputSystem.Update();Check(g.ReadDriveCommand().shoot,"fresh mouse press works after role-switch neutral release");
            InputSystem.RemoveDevice(roadMouse);InputSystem.RemoveDevice(roadKey);g.SwitchDriveRole();
            yield return View("ready");
            g.TickDrive(.05f,new SFCommand{interact=true});Check(g.Drive.Phase==SFDrivePhase.Boarding,"context action starts boarding in game");
            Check(g.Audio.VehicleCueCount==1,"boarding emits one opening cue");
            for(int i=0;i<10;i++)g.TickDrive(.05f,default);yield return View("boarding");
            for(int i=0;i<30;i++)g.TickDrive(.05f,default);Check(g.Drive.Phase==SFDrivePhase.Driving,"integrated boarding hands control to driver");
            Check(g.Audio.VehicleCueCount==3&&g.Audio.VehicleMix.EngineWanted,"boarding closes door then starts engine exactly once");
            for(int i=0;i<80;i++)g.TickDrive(.05f,throttle);yield return View("driving");
            var engineClip=vehicleSources[0].clip;for(int i=0;i<24;i++)g.Audio.PlayAt("bodyhit",0,.1f);
            Check(vehicleSources[0].clip==engineClip&&vehicleSources[0].loop,"foot impact voice pressure cannot steal engine loop");
            float volume=g.Audio.Volume;g.Audio.Volume=0;g.TickDrive(.05f,throttle);
            Check(vehicleSources.All(s=>s.mute&&s.volume==0),"master mute silences every vehicle source immediately");
            g.Audio.Volume=.65f;g.TickDrive(.05f,throttle);
            Check(vehicleSources[0].volume>0&&!vehicleSources[0].mute,"restoring master volume restores bounded engine gain");
            g.Audio.Volume=volume;
            g.PauseForInterruption();d=g.Drive.Distance;clock=g.Drive.Clock;g.TickDrive(.2f,throttle);Check(g.State==SFState.Paused&&g.Drive.Distance==d&&g.Drive.Clock==clock,"focus interruption pauses road and clears held controls");
            int cues=g.Audio.VehicleCueCount;float gain=g.Audio.VehicleMix.EngineGain;
            Check(g.Audio.VehiclePaused&&!vehicleSources.Any(s=>s.isPlaying),"focus interruption pauses dedicated loops and one shots");
            g.TickDrive(.2f,throttle);Check(g.Audio.VehicleCueCount==cues&&g.Audio.VehicleMix.EngineGain==gain,"paused ticks cannot advance vehicle audio events or fades");
            g.SetState(SFState.Playing);g.EnableTouch("1");g.RefreshTouchLayout(900,1600);Check(g.State==SFState.Paused&&g.LayoutBlocked,"portrait rotation pauses active drive");
            g.RefreshTouchLayout(1600,900);Check(g.State==SFState.Paused&&!g.LayoutBlocked,"landscape return requires explicit resume");g.EnableTouch("0");g.SetState(SFState.Playing);
            g.TickDrive(.05f,throttle);Check(!g.Audio.VehiclePaused&&g.Audio.VehicleCueCount==cues,"explicit resume continues without replaying ignition or doors");
            while(g.Drive.Distance<240)g.TickDrive(.05f,throttle);yield return View("transition");
            g.Drive.SetRole(SFDriveRole.Gunner);for(int i=0;i<80;i++)g.TickDrive(.05f,new SFCommand{shoot=true});yield return View("gunner");
            Check(g.Audio.VehicleCueCount==cues,"driver to gunner transfer preserves audio lifecycle");
            for(int limit=0;limit<1500&&g.Drive.Phase==SFDrivePhase.Driving;limit++)g.TickDrive(.05f,new SFCommand{shoot=true});
            while(g.Drive.Phase==SFDrivePhase.Stopping)g.TickDrive(.05f,default);
            Check(g.Drive.Phase==SFDrivePhase.Dismounting&&g.Drive.Speed==0,"arrival stops before dismounting");
            for(int i=0;i<22;i++)g.TickDrive(.05f,default);yield return View("dismounting");
            for(int i=0;i<40;i++)g.TickDrive(.05f,default);Check(g.Drive.Phase==SFDrivePhase.Complete,"integrated route reaches complete");yield return View("arrival");
            var straggler=g.Drive.Enemies[5];straggler.Active=true;straggler.Resolved=true;straggler.X=0;g.RenderDrive();
            Check(g.PursuitVisibleCount==0&&!g.PursuitMuzzleVisible(5)&&g.PursuitSmokeVisible(5)==0,"safe overlook cannot retain a late pursuit vehicle or its effects");
            Check(g.Audio.VehicleCueCount==5&&g.Audio.VehicleMix.EngineGain==0&&g.Audio.VehicleMix.TireGain==0,"arrival opens and closes door once and fades vehicle loops to silence");
            Check(g.Player.health==health&&g.Player.TotalAmmo==ammo&&g.Player.transform.position==pos&&g.Elapsed==elapsed&&g.CompletedMissions==completed,"road preserves foot health ammo position time and mission progress");
            g.TickDrive(.05f,new SFCommand{interact=true});Check(!g.DriveActive&&g.State==SFState.Menu&&g.World.gameObject.activeSelf&&g.Camera.transform.position==camera,"exit restores title world and camera");
            Check(!g.Audio.VehicleActive&&vehicleSources.All(s=>!s.isPlaying&&s.volume==0),"leaving road stops all vehicle playback");
            g.SetState(SFState.Won);int resultPlays=g.Audio.ResultPlays;Check(g.StartDrive(),"completed mission offers road interlude");g.EndDrive();Check(g.State==SFState.Won,"leaving road returns to completed mission");
            Check(g.Audio.ResultPlays==resultPlays&&!g.Audio.ResultVoice.isPlaying&&g.Audio.ResultVoice.clip==null,"returning from road does not replay the completed foot mission cue");
            g.SetState(SFState.Playing);g.SetState(SFState.Won);
            Check(g.Audio.ResultPlays==resultPlays+1,"a new foot victory still plays its result cue after road return");
            g.ReturnToLoadout(0);Check(g.StartDrive(),"road can be reentered after cleanup");g.ReturnToLoadout(1);yield return null;
            Check(!g.DriveActive&&g.State==SFState.Menu&&g.RouteIndex==1&&GameObject.Find("Serra pickup interlude")==null,"mission select cleans up vehicle renderers and meshes");
            Check(g.GetComponents<AudioSource>().Length==sourceCount,"reentry and mission changes reuse the fixed audio source pool");
            string credit=Resources.Load<TextAsset>("AudioCredits").text;
            Check(credit.Contains("audible-edge")&&credit.Contains("looneybits")&&credit.Contains("licenses/by/3.0/")&&credit.Contains("car-engine-loop-96khz-4s"),"vehicle author source and attribution license are available in game");
            yield return CheckMotorcycles();
            yield return CheckRoadUpgrade();yield return CheckJLTV();
            g.StartGame();Check(!g.StartDrive()&&!g.StartDrive(true),"active combat cannot enter road and lose action ownership");g.SetState(SFState.Paused);
            string qa=Web?"/sf-qa":Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(qa);File.WriteAllLines(Path.Combine(qa,"drive-v0846-checks.txt"),checks);
            Debug.Log("DRIVE QA COMPLETE: "+checks.Count+" checks; failures="+checks.Count(s=>s.StartsWith("FAIL"))+"; renderedWeb="+Web);
            if(Web){g.ReturnToLoadout(0);g.StartDrive();g.SetState(SFState.Paused);}else Application.Quit(checks.Any(s=>s.StartsWith("FAIL"))?2:0);
        }
    }
}
