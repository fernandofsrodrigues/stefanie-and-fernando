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
    public sealed class SFDepthCheck:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();SFGame g;string qa;bool civilianPistolInput;
        void Check(bool pass,string label){checks.Add((pass?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void VerifyInputWalk(SFActor actor,SFCommand command,int direction,bool run,string device)
        {
            actor.Teleport(new Vector2(7,0));actor.lane=0;actor.FieldUniform=!civilianPistolInput;actor.Helmet=true;
            actor.Armed=civilianPistolInput;actor.Rifle=actor.Crouching=actor.Aiming=false;actor.action=SFAction.Idle;
            actor.Tick(.06f,command);actor.Render(0);
            string expected=(run?"field_":"field_walk_")+(direction>0?"north":"south");
            if(civilianPistolInput)expected=direction<0?"civil_pistol_south":actor.identity=="fernando"?"civil_pistol_north":"civil_pistol_rig";
            Check(command.move.y*direction>.15f&&command.run==run&&actor.lane*direction>0&&actor.CurrentSheet==expected,
                actor.identity+(civilianPistolInput?" civilian pistol ":" field unarmed ")+device+" "+(direction>0?"north":"south")+(run?" run":" walk")+" actual input moves lane and selects reviewed travel art or matching fallback");
        }
        void VerifyStop(SFActor actor,SFCommand command,string device)
        {
            actor.Tick(.06f,command);actor.Render(0);
            Check(!actor.DepthMotion.Moving&&!actor.CurrentSheet.StartsWith("field_walk_")&&actor.CurrentSheet!="civil_pistol_south"&&actor.CurrentSheet!="civil_pistol_north",actor.identity+" "+device+" release restores standing/side art");
        }
        void InputWalkPaths(SFActor actor)
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();mouse.MakeCurrent();
            foreach(int direction in new[]{1,-1})foreach(bool run in new[]{false,true})
            {
                var key=direction>0?Key.W:Key.S;
                InputSystem.QueueStateEvent(keyboard,run?new KeyboardState(key,Key.LeftShift):new KeyboardState(key));InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();
                VerifyInputWalk(actor,SFInput.Read(false,false),direction,run,"keyboard");
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();VerifyStop(actor,SFInput.Read(false,false),"keyboard");
            var pad=InputSystem.AddDevice<Gamepad>();var reader=new SFGamepadInput();
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up});InputSystem.Update();pad.MakeCurrent();reader.Poll();
            InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();pad.MakeCurrent();reader.Poll();
            foreach(int direction in new[]{1,-1})foreach(bool run in new[]{false,true})
            {
                var state=new GamepadState{leftStick=Vector2.up*direction};if(run)state=state.WithButton(GamepadButton.LeftStick);
                InputSystem.QueueStateEvent(pad,state);InputSystem.Update();pad.MakeCurrent();reader.Poll();
                VerifyInputWalk(actor,reader.Read(false,false,false,.06f),direction,run,"controller");
            }
            InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();pad.MakeCurrent();reader.Poll();VerifyStop(actor,reader.Read(false,false,false,.06f),"controller");
            InputSystem.RemoveDevice(pad);
            var screen=InputSystem.AddDevice<Touchscreen>();var touch=new SFTouchInput();int contact=30;
            foreach(int direction in new[]{1,-1})foreach(bool run in new[]{false,true})
            {
                Vector2 at=SFTouchInput.StickCenter+Vector2.down*direction*(run?88:45);
                Vector2 pixel=new Vector2(at.x,900-at.y)*Screen.height/900f;
                InputSystem.QueueStateEvent(screen,new TouchState{touchId=++contact,phase=UnityEngine.InputSystem.TouchPhase.Began,position=pixel});InputSystem.Update();screen.MakeCurrent();
                VerifyInputWalk(actor,touch.Read(actor,Screen.width*900f/Screen.height),direction,run,"touch stick");
                InputSystem.QueueStateEvent(screen,new TouchState{touchId=contact,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=pixel});InputSystem.Update();touch.Read(actor,Screen.width*900f/Screen.height);
            }
            InputSystem.Update();VerifyStop(actor,touch.Read(actor,Screen.width*900f/Screen.height),"touch stick");
            InputSystem.RemoveDevice(screen);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/Depth-v0846"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.4f);g.StartGame();
            foreach(var a in g.Actors){a.Teleport(new Vector2(a.hero?7:125,0));a.cooldown=999;a.lane=0;}
            yield return new WaitForSeconds(.3f);g.SetState(SFState.Paused);
            var d=new SFDepthMotion();d.Step(Vector2.up,.08f,.1f,false,true);
            Check(d.Moving&&d.Direction==1,"north uses positive actual lane travel");
            float phase=d.Phase;d.Step(Vector2.up,0,.1f,false,true);
            Check(!d.Moving&&d.Phase==phase,"blocked depth input does not animate stationary feet");
            d.Step(new Vector2(.95f,1),.04f,.1f,false,true);Check(d.Direction==1,"near diagonal retains depth orientation without jitter");
            d.Step(new Vector2(1,.7f),.04f,.1f,false,true);Check(!d.Moving&&d.Direction==0,"horizontal-dominant movement returns to side art");
            d.Step(Vector2.down,-.11f,.1f,true,true);Check(d.Direction==-1&&d.Running&&d.Frame(true)>=4&&d.Frame(true)<8,"south sprint uses its running row");
            d.Step(Vector2.up,.04f,0,false,true);Check(!d.Moving,"zero-time update cannot advance a directional pose");
            d.Step(Vector2.up,.04f,.1f,false,false);Check(!d.Moving,"disabled movement cannot select depth art");
            var a1=new SFDepthMotion();var a2=new SFDepthMotion();for(int i=0;i<10;i++)a1.Step(Vector2.up,.01f,.01f,false,true);a2.Step(Vector2.up,.1f,.1f,false,true);
            Check(Mathf.Abs(a1.Phase-a2.Phase)<.00001f,"depth phase depends on distance, not frame rate");
            phase=a2.Phase;a2.Step(Vector2.up,0,.1f,true,true);Check(a2.Phase==phase,"speed change never jumps phase while stationary");
            // This opt-in QA executable runs hidden. Match the other virtual-input suites;
            // normal gameplay retains Unity's foreground input policy.
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            // Native device events may make the physical keyboard current during Update.
            // Select our virtual device after processing events so the fixture cannot read the user's keyboard.
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));InputSystem.Update();keyboard.MakeCurrent();var cmd=SFInput.Read(false,false);
            Check(cmd.move.y==1&&cmd.run,"W plus Shift sends north running through the shared input map");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S));InputSystem.Update();keyboard.MakeCurrent();cmd=SFInput.Read(false,false);
            Check(cmd.move.y==-1&&!cmd.run,"S sends south walking through the shared input map");InputSystem.RemoveDevice(keyboard);
            foreach(var actor in new[]{g.Fernando,g.Stefanie})
            {
                g.Selected=actor==g.Fernando?0:1;g.Partner.Teleport(new Vector2(110,0));
                foreach(string direction in new[]{"north","south"})
                {
                    var composite=Resources.Load<SFArt>("SF/"+actor.identity+"_field_walk_"+direction);
                    var pairs=new[]{"contact_a","pass_a","contact_b","pass_b"}.Select(p=>Resources.Load<SFArt>("SF/"+actor.identity+"_field_"+direction+"_walk_"+p)).ToArray();
                    Check(composite!=null&&composite.frames.Length==8&&Enumerable.Range(0,8).All(i=>pairs[i%4]!=null&&pairs[i%4].frames.Length==2&&composite.frames[i]==pairs[i%4].frames[i/4]),actor.identity+" "+direction+" composite preserves calibrated contact/pass sprite references for both headgear choices");
                }
                var pistolComposite=Resources.Load<SFArt>("SF/"+actor.identity+"_civil_pistol_south");
                var pistolPairs=new[]{"contact_a","pass_a","contact_b","pass_b"}.Select(p=>Resources.Load<SFArt>("SF/"+actor.identity+"_civil_pistol_south_"+p)).ToArray();
                Check(pistolComposite!=null&&pistolComposite.frames.Length==8&&Enumerable.Range(0,8).All(i=>pistolPairs[i%4]!=null&&pistolPairs[i%4].frames.Length==2&&pistolComposite.frames[i]==pistolPairs[i%4].frames[i/4]),actor.identity+" civilian pistol composite preserves all four walk and four run source phases");
                if(actor.identity=="fernando")
                {
                    var rear=Resources.Load<SFArt>("SF/fernando_civil_pistol_north");
                    var rearPairs=new[]{"contact_a","pass_a","contact_b","pass_b"}.Select(p=>Resources.Load<SFArt>("SF/fernando_civil_pistol_north_"+p)).ToArray();
                    Check(rear!=null&&rear.frames.Length==8&&Enumerable.Range(0,8).All(i=>rearPairs[i%4]!=null&&rearPairs[i%4].frames.Length==2&&rear.frames[i]==rearPairs[i%4].frames[i/4]),"Fernando rear pistol composite preserves all four walk and four run source references");
                }
                foreach(bool field in new[]{true,false})foreach(int weapon in new[]{0,1,2})foreach(bool helmet in new[]{true,false})foreach(int direction in new[]{1,-1})foreach(bool run in new[]{false,true})
                {
                    bool rifle=weapon==1,pistol=weapon==2;
                    actor.Teleport(new Vector2(7,0));actor.lane=0;actor.FieldUniform=field;actor.Helmet=helmet;actor.Armed=weapon!=0;actor.Rifle=rifle;actor.action=SFAction.Idle;actor.Crouching=actor.Aiming=false;
                    cmd=new SFCommand{move=Vector2.up*direction,run=run};actor.Tick(.02f,cmd);actor.Render(0);
                    string sheet=(field?"field_":"civil_")+(rifle?"rifle_":pistol?"pistol_":"")+(direction>0?"north":"south"),label=actor.identity+" "+sheet+(helmet?" helmet-option-on":" helmet-option-off")+(run?" run":" walk");
                    var art=Resources.Load<SFArt>("SF/"+actor.identity+"_"+sheet);
                    if(!field&&pistol&&direction==1&&actor.identity=="stefanie")
                    {
                        Check(art==null&&actor.CurrentSheet=="civil_pistol_rig"&&actor.CivilianLocomotionRig!=null&&actor.CivilianLocomotionRig.gameObject.activeSelf&&!actor.visual.enabled,label+" missing rear direction retains matching civilian pistol rig");
                        continue;
                    }
                    // A rear civilian MR762 sheet has not passed generation/review. Keep the existing weapon rig.
                    if(!field&&rifle&&actor.identity=="stefanie"&&direction==1)
                    {
                        Check(art==null&&actor.CurrentSheet=="civil_rifle_rig"&&actor.CivilianLocomotionRig!=null&&actor.CivilianLocomotionRig.gameObject.activeSelf&&!actor.visual.enabled,label+" missing direction safely retains matching side rifle rig");
                        continue;
                    }
                    bool compactPistol=!field&&pistol;
                    Check(art!=null&&art.frames.Length==(compactPistol?8:16)&&art.frames.All(x=>x!=null),label+" source contains every expected imported pose");
                    bool lowWalk=field&&weapon==0&&!run;
                    string expected=lowWalk?"field_walk_"+(direction>0?"north":"south"):sheet;
                    Check(actor.CurrentSheet==expected&&actor.visual.enabled&&(actor.LocomotionRig==null||!actor.LocomotionRig.gameObject.activeSelf)&&(actor.CivilianLocomotionRig==null||!actor.CivilianLocomotionRig.gameObject.activeSelf),label+" overrides only locomotion and suspends both side rigs");
                    int row=lowWalk?(helmet?0:4):(field&&helmet||compactPistol?0:8)+(run?4:0);bool frames=true;
                    for(int i=0;i<4;i++)
                    {
                        actor.DepthMotion.Reset();actor.DepthMotion.Step(Vector2.up*direction,(i+.1f)/4*(run?1.1f:.8f)*direction,.1f,run,true);actor.Render(0);
                        int authored=actor.identity=="fernando"&&sheet=="field_pistol_south"?new[]{0,3,2,1}[i]:i;
                        frames&=actor.CurrentFrame==row+authored&&!actor.visual.flipX;
                        SFGaitCheck.CaptureActor(actor,qa,label.Replace(' ','-')+"-"+i);
                    }
                    Check(frames,label+" selects the reviewed four-phase order without horizontal mirroring");
                }
                actor.FieldUniform=true;actor.Armed=actor.Rifle=true;actor.Render(0);
                Check(actor.CurrentSheet=="field_rifle_south"&&actor.visual.enabled,actor.identity+" drawing rifle during depth travel selects matching rifle carry");
                actor.Aiming=true;actor.Render(0);Check(actor.CurrentSheet=="field_rifle_rig",actor.identity+" rifle aim immediately restores side-facing weapon rig");
                actor.Aiming=false;actor.action=SFAction.Shoot;actor.Render(0);Check(actor.CurrentSheet=="field_rifle",actor.identity+" rifle fire keeps shot and muzzle pose priority");
                actor.action=SFAction.Reload;actor.Render(0);Check(actor.CurrentSheet=="field_rifle_reload",actor.identity+" rifle reload cannot show a travelling low-ready pose");
                actor.action=SFAction.Idle;actor.Crouching=true;actor.Render(0);Check(actor.CurrentSheet=="field_rifle",actor.identity+" rifle crouch keeps weapon crouch pose");
                actor.Crouching=false;actor.Rifle=false;actor.Render(0);Check(actor.CurrentSheet=="field_pistol_south",actor.identity+" pistol selection replaces rifle depth with the matching sidearm travel pose");
                actor.Aiming=true;actor.Render(0);Check(actor.CurrentSheet=="field_pistol_rig",actor.identity+" pistol aim restores side-facing aiming rig");
                actor.Aiming=false;actor.action=SFAction.Shoot;actor.Render(0);Check(actor.CurrentSheet=="field_actions",actor.identity+" pistol fire retains shot pose priority");
                actor.action=SFAction.Reload;actor.Render(0);Check(actor.CurrentSheet=="field_actions",actor.identity+" pistol reload cannot display depth travel");
                actor.action=SFAction.Idle;actor.Crouching=true;actor.Render(0);Check(actor.CurrentSheet=="field_utility",actor.identity+" pistol crouch retains armed crouching pose");
                actor.Crouching=false;actor.action=SFAction.Hurt;actor.Render(0);Check(!actor.CurrentSheet.EndsWith("north")&&!actor.CurrentSheet.EndsWith("south"),actor.identity+" pistol hurt interrupts directional travel");
                actor.action=SFAction.Idle;actor.Render(0);Check(actor.CurrentSheet=="field_pistol_south",actor.identity+" pistol travel resumes after interrupted action");
                actor.FieldUniform=false;actor.Render(0);Check(actor.CurrentSheet=="civil_pistol_south",actor.identity+" civilian pistol selects its own front travel instead of military clothing");
                actor.Aiming=true;actor.Render(0);Check(actor.CurrentSheet=="civil_pistol_rig",actor.identity+" civilian pistol aim interrupts front travel");
                actor.Aiming=false;actor.action=SFAction.Shoot;actor.Render(0);Check(actor.CurrentSheet!="civil_pistol_south",actor.identity+" civilian pistol firing retains horizontal shot pose");
                actor.action=SFAction.Reload;actor.Render(0);Check(actor.CurrentSheet!="civil_pistol_south",actor.identity+" civilian pistol reload overrides front travel");
                actor.action=SFAction.Idle;actor.Crouching=true;actor.Render(0);Check(actor.CurrentSheet!="civil_pistol_south",actor.identity+" civilian pistol crouching overrides front travel");
                actor.Crouching=false;actor.action=SFAction.Hurt;actor.Render(0);Check(actor.CurrentSheet!="civil_pistol_south",actor.identity+" civilian pistol hurt overrides front travel");actor.action=SFAction.Idle;
                actor.FieldUniform=true;actor.DepthMotion.ClearMovement();actor.Render(0);Check(actor.CurrentSheet=="field_pistol_rig",actor.identity+" stopping pistol depth does not hold a lifted foot");
                actor.DepthMotion.Step(Vector2.up,.1f,.1f,false,true);actor.Render(0);Check(actor.CurrentSheet=="field_pistol_north",actor.identity+" pistol reverses to rear travel");
                actor.DepthMotion.Step(Vector2.down,-.1f,.1f,false,true);actor.Render(0);Check(actor.CurrentSheet=="field_pistol_south",actor.identity+" pistol reverses back to front travel");
                actor.Armed=false;actor.Render(0);Check(actor.CurrentSheet=="field_walk_south"&&actor.visual.enabled&&!actor.LocomotionRig.gameObject.activeSelf,actor.identity+" rifle holster restores low unarmed walking sheet and suspends side rig");
                actor.Armed=actor.Rifle=true;actor.FieldUniform=false;actor.Render(0);Check(actor.CurrentSheet=="civil_rifle_south"&&actor.visual.enabled,actor.identity+" outfit change selects civilian rifle depth art");
                actor.action=SFAction.Shoot;actor.Render(0);Check(actor.CurrentSheet=="civil_rifle",actor.identity+" civilian rifle fire overrides travel");
                actor.action=SFAction.Reload;actor.Render(0);Check(actor.CurrentSheet=="civil_rifle",actor.identity+" civilian rifle reload overrides travel");
                actor.action=SFAction.Idle;actor.Crouching=true;actor.Render(0);Check(actor.CurrentSheet=="civil_rifle"&&actor.CurrentFrame==3,actor.identity+" civilian rifle crouch retains its dedicated pose");
                actor.Crouching=false;actor.Rifle=false;actor.Render(0);Check(actor.CurrentSheet=="civil_pistol_south",actor.identity+" pistol switch replaces civilian rifle travel with pistol travel");
                actor.Armed=false;actor.Render(0);Check(actor.CurrentSheet=="civil_south",actor.identity+" civilian rifle holster restores unarmed travel");
                actor.Armed=actor.Rifle=true;actor.Render(0);Check(actor.CurrentSheet=="civil_rifle_south",actor.identity+" civilian rifle redraw restores the loaded travel sheet");
                actor.DepthMotion.Step(Vector2.up,.1f,.1f,false,true);actor.Render(0);Check(actor.CurrentSheet==(actor.identity=="fernando"?"civil_rifle_north":"civil_rifle_rig"),actor.identity+" direction change uses reviewed art or matching side fallback");
                actor.Aiming=true;actor.Render(0);Check(actor.CurrentSheet=="civil_rifle_rig",actor.identity+" aiming during civilian rifle travel restores the aiming rig");actor.Aiming=false;
                actor.FieldUniform=true;actor.DepthMotion.ClearMovement();actor.Render(0);Check(!actor.CurrentSheet.EndsWith("south"),actor.identity+" stopped rifle travel cannot retain a raised-foot depth pose");
                actor.DepthMotion.Step(Vector2.down,-.1f,.1f,true,true);actor.Render(0);Check(actor.CurrentSheet=="field_rifle_south",actor.identity+" resumed rifle travel restores directional art");
                actor.Armed=actor.Rifle=false;
                actor.action=SFAction.Hurt;actor.Render(0);Check(!actor.CurrentSheet.EndsWith("north")&&!actor.CurrentSheet.EndsWith("south"),actor.identity+" damage overrides depth motion");
                actor.action=SFAction.Punch;actor.Render(0);Check(actor.CurrentSheet=="field_actions",actor.identity+" combat retains horizontal attack art");
                actor.action=SFAction.Idle;actor.Armed=actor.Rifle=true;actor.Aiming=true;actor.Render(0);Check(!actor.CurrentSheet.EndsWith("north")&&!actor.CurrentSheet.EndsWith("south"),actor.identity+" drawn rifles retain matching weapon art");
                actor.Armed=false;actor.Aiming=false;actor.Crouching=true;actor.Render(0);Check(actor.CurrentSheet=="field_ground",actor.identity+" crouch overrides depth motion");
                actor.Crouching=false;actor.FieldUniform=false;actor.Render(0);Check(actor.CurrentSheet=="civil_south",actor.identity+" changing outfit immediately selects civilian depth art");
                actor.action=SFAction.Hurt;actor.Render(0);Check(!actor.CurrentSheet.StartsWith("civil_"),actor.identity+" civilian hurt overrides depth motion");
                actor.action=SFAction.Punch;actor.Render(0);Check(!actor.CurrentSheet.EndsWith("south"),actor.identity+" civilian combo retains its attack pose");
                actor.action=SFAction.Idle;actor.Armed=true;actor.Rifle=true;actor.Aiming=true;actor.Render(0);Check(actor.CurrentSheet=="civil_rifle_rig"&&actor.CivilianLocomotionRig!=null&&actor.CivilianLocomotionRig.gameObject.activeSelf,actor.identity+" moving civilian rifle aiming retains the weapon rig");
                actor.Rifle=false;actor.Render(0);Check(!actor.CurrentSheet.EndsWith("south"),actor.identity+" civilian pistol aiming overrides depth motion");
                actor.Armed=actor.Aiming=false;actor.Render(0);Check(actor.CurrentSheet=="civil_south"&&actor.visual.enabled&&!actor.CivilianLocomotionRig.gameObject.activeSelf,actor.identity+" holstering resumes depth motion and suspends the firearm rig");
                actor.Armed=actor.Aiming=false;actor.Crouching=true;actor.Render(0);Check(!actor.CurrentSheet.EndsWith("south"),actor.identity+" civilian crouch overrides depth motion");
                actor.Crouching=false;actor.DepthMotion.ClearMovement();actor.Render(0);Check(!actor.CurrentSheet.EndsWith("south"),actor.identity+" stopping cannot freeze a civilian raised foot pose");
                actor.FieldUniform=true;actor.Teleport(new Vector2(7,0));Check(!actor.DepthMotion.Moving&&actor.DepthMotion.Phase==0,actor.identity+" teleport resets stale depth animation");
                InputWalkPaths(actor);
                civilianPistolInput=true;InputWalkPaths(actor);civilianPistolInput=false;
                if(actor.identity=="fernando")
                {
                    actor.FieldUniform=false;actor.Armed=true;actor.Rifle=actor.Aiming=actor.Crouching=false;actor.action=SFAction.Idle;
                    actor.DepthMotion.Step(Vector2.up,.1f,.1f,false,true);actor.Render(0);
                    Check(actor.CurrentSheet=="civil_pistol_north","Fernando north reversal selects rear pistol travel");
                    actor.Aiming=true;actor.Render(0);Check(actor.CurrentSheet=="civil_pistol_rig","Fernando rear pistol aiming restores the side weapon rig");
                    actor.Aiming=false;actor.action=SFAction.Shoot;actor.Render(0);Check(actor.CurrentSheet!="civil_pistol_north","Fernando rear pistol firing restores the shot pose");
                    actor.action=SFAction.Reload;actor.Render(0);Check(actor.CurrentSheet!="civil_pistol_north","Fernando rear pistol reload overrides travel");
                    actor.action=SFAction.Idle;actor.Crouching=true;actor.Render(0);Check(actor.CurrentSheet!="civil_pistol_north","Fernando rear pistol crouch overrides travel");
                    actor.Crouching=false;actor.action=SFAction.Hurt;actor.Render(0);Check(actor.CurrentSheet!="civil_pistol_north","Fernando rear pistol hurt overrides travel");
                    actor.action=SFAction.Idle;actor.Armed=false;actor.Render(0);Check(actor.CurrentSheet=="civil_north","Fernando rear pistol holster restores unarmed travel");
                    actor.Armed=true;actor.Render(0);Check(actor.CurrentSheet=="civil_pistol_north","Fernando rear pistol redraw resumes the loaded rear art");
                    actor.Rifle=true;actor.Render(0);Check(actor.CurrentSheet=="civil_rifle_north","Fernando rear pistol rifle switch selects rifle travel");
                    actor.Rifle=false;actor.DepthMotion.ClearMovement();actor.Render(0);Check(actor.CurrentSheet!="civil_pistol_north","Fernando stopped rear pistol never holds a raised-foot travel pose");
                }
            }
            File.WriteAllLines(Path.Combine(qa,"depth-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
