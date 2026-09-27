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
    public sealed class SFCloseCombatCheck:MonoBehaviour
    {
        SFGame g;SFActor enemy;string qa;readonly List<string> checks=new List<string>();
        void Check(bool ok,string label){checks.Add((ok?"PASS: ":"FAIL: ")+label);Debug.Log(checks.Last());}
        void Advance(SFActor p,float seconds,SFCommand c=default)
        {
            // Match gameplay ordering: both actors advance, including the victim's hit immunity.
            for(float t=0;t<seconds-.000001f;){float dt=Mathf.Min(.02f,seconds-t);p.Tick(dt,c);enemy.Tick(dt,default);t+=dt;}
        }
        void Ready(SFActor p,float dx=.95f)
        {
            g.Selected=p==g.Fernando?0:1;g.Partner.ReleaseGrip();g.Partner.Teleport(new Vector2(110,0));
            p.ReleaseGrip();p.action=SFAction.Idle;p.Armed=p.Rifle=p.Aiming=p.Crouching=false;p.ResetCombo();p.CounterTime=0;p.cooldown=0;p.invulnerable=0;p.health=1000;p.facing=1;
            p.Teleport(new Vector2(7,0));p.lane=-.85f;
            enemy.HeldBy=null;enemy.action=SFAction.Idle;enemy.boss=false;enemy.invulnerable=0;enemy.health=enemy.maxHealth=1000;enemy.Teleport(new Vector2(7+dx,0));enemy.lane=p.lane;
            Advance(p,.65f);p.cooldown=0;
        }
        void GroundArtChecks(SFActor p)
        {
            // Keep coverage of the independent-body fallback for opponents without paired artwork.
            var previousEnemy=enemy;previousEnemy.Teleport(new Vector2(128,0));enemy=g.Actors.First(a=>a.identity=="silk");
            var art=Resources.Load<SFArt>("SF/"+p.identity+"_ground_combo");
            Check(art!=null&&art.frames.Length==4&&art.frames.All(s=>s!=null)&&art.frames.Distinct().Count()==4,p.identity+" four dedicated civilian ground-combo sprites load");
            for(int variant=0;variant<3;variant++)
            {
                Ready(p);p.FieldUniform=variant>0;p.Helmet=variant==1;p.TryGroundSequence();
                Check(p.GroundSequence,p.identity+" variant "+variant+" starts authored ground sequence");
                for(int beat=0;beat<6;beat++)
                {
                    float clock=SFGroundAnimation.FirstHit+beat*SFGroundAnimation.Interval;
                    Advance(p,clock-p.actionClock+.001f);
                    string sheet=variant==0?"ground_combo":"field_ground";
                    Check(p.CurrentSheet==sheet&&p.GroundHits==beat+1,p.identity+" variant "+variant+" contact "+beat+" matches damage beat and outfit (clock="+p.actionClock.ToString("F4")+", hits="+p.GroundHits+")");
                    if(variant==0)Check(p.CurrentFrame==(beat%2==0?1:3),p.identity+" civilian beat "+beat+" alternates jab and straight artwork");
                    else Check(p.CurrentFrame/3==(p.Helmet?0:1),p.identity+" military beat "+beat+" preserves headgear");
                }
                Advance(p,.5f);Check(!p.GroundSequence&&p.CurrentSheet!="ground_combo",p.identity+" variant "+variant+" ends without stale ground art");
            }
            Ready(p);p.FieldUniform=false;p.TryGroundSequence();
            foreach(float clock in new[]{.1f,.5f,.62f,.74f})
            {p.actionClock=clock;p.Render(0);enemy.Tick(.02f,default);SFGaitCheck.CaptureActor(p,qa,p.identity+"-ground-frame-"+p.CurrentFrame);}
            p.facing=-1;p.Render(0);Check(p.visual.flipX,p.identity+" civilian ground art follows left facing");
            p.ReleaseGrip(true);p.action=SFAction.Hurt;p.Render(0);Check(p.CurrentSheet=="reaction",p.identity+" hurt interrupts ground artwork");
            Ready(p);p.Armed=p.Rifle=true;p.Render(0);Check(p.CurrentSheet=="civil_rifle",p.identity+" firearm selection retains rifle artwork");p.Armed=p.Rifle=false;
            enemy.Teleport(new Vector2(128,0));enemy=previousEnemy;
        }
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/CloseCombat-v0846"));Directory.CreateDirectory(qa);
            yield return new WaitForSeconds(.5f);g.StartGame();foreach(var a in g.Actors){a.Teleport(new Vector2(a.hero?7:128,0));a.cooldown=999;a.lane=-.85f;}
            yield return new WaitForSeconds(.3f);g.SetState(SFState.Paused);enemy=g.Actors.First(a=>!a.Friendly&&!a.boss);
            foreach(var p in new[]{g.Fernando,g.Stefanie})
            {
                Ready(p);Check(!g.TryApproachClinch(p,new SFCommand{move=Vector2.right}),p.identity+" paused approach cannot capture");
                g.SetState(SFState.Playing);Ready(p);
                Check(!g.TryApproachClinch(p,default),p.identity+" standing close does not auto-grab");
                Check(!g.TryApproachClinch(p,new SFCommand{move=Vector2.left}),p.identity+" retreat does not auto-grab");
                var denied=new[]{new SFCommand{move=Vector2.right,punch=true},new SFCommand{move=Vector2.right,kick=true},new SFCommand{move=Vector2.right,guard=true},new SFCommand{move=Vector2.right,jump=true},new SFCommand{move=Vector2.right,dash=true},new SFCommand{move=Vector2.right,crouch=true},new SFCommand{move=Vector2.right,toggleArmed=true}};
                foreach(var c in denied)Check(!g.TryApproachClinch(p,c),p.identity+" explicit action retains approach priority "+System.Array.IndexOf(denied,c));
                p.Armed=true;Check(!g.TryApproachClinch(p,new SFCommand{move=Vector2.right}),p.identity+" firearm never auto-holsters into a clinch");p.Armed=false;
                enemy.lane=p.lane+1;Check(!g.TryApproachClinch(p,new SFCommand{move=Vector2.right}),p.identity+" different depth lane cannot auto-grab");enemy.lane=p.lane;
                enemy.boss=true;Check(!g.TryApproachClinch(p,new SFCommand{move=Vector2.right}),p.identity+" healthy boss resists approach");enemy.boss=false;
                enemy.Teleport(new Vector2(9,0));Check(!g.TryApproachClinch(p,new SFCommand{move=Vector2.right}),p.identity+" punch distance does not become long-distance grab");enemy.Teleport(new Vector2(7.95f,0));
                var wall=new SFCover{footprint=new Rect(7.4f,p.lane-.3f,.1f,.6f),height=3,label="QA clinch wall"};g.Covers.Add(wall);
                Check(!g.TryApproachClinch(p,new SFCommand{move=Vector2.right}),p.identity+" solid cover blocks automatic clinch");g.Covers.Remove(wall);
                enemy.HeldBy=g.Partner;Check(!g.TryApproachClinch(p,new SFCommand{move=Vector2.right}),p.identity+" partner-owned opponent cannot be stolen");enemy.HeldBy=null;
                p.Tick(.02f,new SFCommand{move=Vector2.right});Check(p.GripTarget==enemy&&enemy.HeldBy==p,p.identity+" actual approach input automatically pairs clinch");
                float hp=enemy.health;
                for(int i=0;i<3;i++){enemy.invulnerable=0;g.ResolveAttack(p,SFAction.ClinchKnee);Check(enemy.health<hp,p.identity+" clinch knee "+(i+1)+" lands");hp=enemy.health;}
                Check(p.GripTarget==null&&enemy.HeldBy==null&&enemy.action==SFAction.Knocked,p.identity+" third knee drops and releases opponent");
                Ready(p);Check(g.TryGrapple(p),p.identity+" throw setup acquires target");p.facing=-1;
                var other=g.Actors.First(a=>!a.Friendly&&a!=enemy&&!a.boss);other.Teleport(new Vector2(6.8f,0));other.lane=p.lane;other.health=1000;other.invulnerable=0;
                Check(g.ThrowEnemy(p)&&enemy.ThrowSpeed<0,p.identity+" directional throw can put enemies on chosen side");
                float otherHp=other.health;for(int i=0;i<10;i++)g.TickThrown(enemy,.02f);
                Check(other.health<otherHp&&other.action==SFAction.Knocked,p.identity+" thrown body knocks down another hostile");other.Teleport(new Vector2(128,0));
                Ready(p);p.Tick(.02f,new SFCommand{grapple=true});Check(p.GroundSequence&&p.GripTarget==enemy&&enemy.action==SFAction.Knocked,p.identity+" G command starts paired ground sequence");
                g.SetState(SFState.Paused);float clock=p.actionClock;yield return new WaitForSeconds(.08f);Check(p.actionClock==clock,p.identity+" pause freezes ground sequence");
                hp=enemy.health;float hit=g.OutgoingDamage(p,7,SFAction.GroundStrike);Advance(p,1.95f);
                Check(p.GroundHits==6&&Mathf.Abs(hp-enemy.health-6*hit)<.01f&&p.GroundSequence,p.identity+" six distinct timed ground impacts each deal damage exactly once");
                Check(enemy.action==SFAction.Knocked&&enemy.CurrentSheet=="reaction"&&enemy.CurrentFrame==3,p.identity+" owned target stays visibly down throughout sequence");
                SFGaitCheck.CaptureActor(p,qa,p.identity+"-ground-provisional");
                Advance(p,.4f);Check(!p.GroundSequence&&p.GripTarget==null&&enemy.HeldBy==null&&!p.Busy,p.identity+" completion returns to free movement");
                g.SetState(SFState.Playing);Ready(p);p.TryGroundSequence();Advance(p,.4f,new SFCommand{move=Vector2.left});Check(!p.GroundSequence&&p.GroundHits==0&&enemy.HeldBy==null,p.identity+" movement escapes before first strike");
                Ready(p);p.TryGroundSequence();Advance(p,.56f);p.invulnerable=0;p.Damage(1,other,true);Check(!p.GroundSequence&&p.GripTarget==null&&enemy.HeldBy==null,p.identity+" incoming damage interrupts and releases both actors");
                Ready(p);p.TryGroundSequence();enemy.health=1;Advance(p,.6f);Check(!enemy.Alive&&!p.GroundSequence&&p.GripTarget==null&&!p.Busy,p.identity+" defeated opponent ends sequence without stale attack");
                Ready(p);p.TryGroundSequence();p.Teleport(new Vector2(10,0));Check(!p.GroundSequence&&enemy.HeldBy==null,p.identity+" checkpoint teleport cannot retain ground ownership");
                Ready(p);p.Armed=true;Check(!p.TryGroundSequence(),p.identity+" takedown never consumes armed fire input");p.Armed=false;
                Check(g.TryGrapple(p),p.identity+" release setup");p.Tick(.02f,new SFCommand{guard=true});Check(p.GripTarget==null&&p.GripReentryDelay>0,p.identity+" guard disengages and delays immediate regrab");
                GroundArtChecks(p);
                g.SetState(SFState.Paused);
            }
            Check(g.Audio.BankSize("bodyhit")==5&&g.Audio.BankSize("bodyheavy")==5,"ten new licensed body impact derivatives load");
            var f=g.Fernando;Ready(f);f.action=SFAction.Punch;enemy.Damage(1,f);Check(g.Audio.LastPlayed=="bodyhit","ordinary punch selects body impact bank");
            string clip=g.Audio.LastClip;enemy.invulnerable=0;enemy.Damage(1,f);Check(g.Audio.LastClip!=clip,"consecutive ordinary impacts do not repeat the same clip");
            f.action=SFAction.Kick;enemy.invulnerable=0;enemy.Damage(1,f);Check(g.Audio.LastPlayed=="bodyheavy","kick selects heavier body impact bank");
            f.action=SFAction.Shoot;enemy.invulnerable=0;enemy.Damage(1,f);Check(g.Audio.LastPlayed=="hit","firearm impact retains existing bank");
            Ready(f);enemy.Teleport(new Vector2(20,0));clip=g.Audio.LastClip;f.action=SFAction.Punch;g.ResolveAttack(f,SFAction.Punch);Check(g.Audio.LastClip==clip,"miss has no false contact impact");
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;var k=InputSystem.AddDevice<Keyboard>();var m=InputSystem.AddDevice<Mouse>();k.MakeCurrent();m.MakeCurrent();
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.G));InputSystem.Update();Check(SFInput.Read(false,false).grapple,"G routes to contextual takedown command");
            InputSystem.QueueStateEvent(k,new KeyboardState());InputSystem.QueueStateEvent(m,new MouseState().WithButton(MouseButton.Back));InputSystem.Update();Check(SFInput.Read(false,false).grapple,"Mouse4 routes to contextual takedown command");
            InputSystem.RemoveDevice(k);InputSystem.RemoveDevice(m);
            File.WriteAllLines(Path.Combine(qa,"close-combat-checks.txt"),checks);Application.Quit(checks.Any(x=>x.StartsWith("FAIL:"))?1:0);
        }
    }
}
#endif
