using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    public enum SFAction { Idle, Punch, Kick, Shoot, Hurt, Guard, Down, Grapple, Held, Sweep, GroundStrike, Knocked, ClinchKnee, Reload }
    public sealed partial class SFActor : MonoBehaviour
    {
        public SFGame game;
        public string identity;
        public bool hero, boss, ranged, ally;
        public bool Friendly=>hero||ally;
        public float health=100, maxHealth=100, lane, actionClock, invulnerable, cooldown, recovery;
        public int facing=1, combo;
        public SFAction action;
        public PlayerMovement motor;
        public SpriteRenderer visual, shadow;
        public float moveAmount;
        public float attackDuration, attackAt;
        public bool Armed, Rifle, Aiming, Crouching;
        public bool FieldUniform,Helmet=true,AerialAttack;
        public float GuardTime,CounterTime,ThrowClock,ThrowSpeed;
        public SFActor ThrowOwner;
        public readonly HashSet<SFActor> ThrowHits=new HashSet<SFActor>();
        public bool ShotRifle, ShotAimed;
        public bool PrecisionRifle=>identity=="stefanie";
        public string RifleName=>PrecisionRifle?"HK MR762A1":"FN SCAR-H Mk 17";
        public float RifleDamage=>PrecisionRifle?38:26;
        public float RifleReach=>PrecisionRifle?14:12;
        public float MuzzleHeight=>hero&&Rifle?(Crouching?1.42f:2.22f):(Crouching?.82f:1.8f);
        bool triggerHeld; float triggerBuffer;
        public SFActor GripTarget, HeldBy;
        [System.NonSerialized] public SFCover CoverChoice;
        internal int CourseStepIndex=-1;
        // Course-chapter identity layer. LayoutId picks behaviour and frame tables, ArtId the sheets actually loaded (the id, or its layout as one whole set).
        // Existing ids have no profile (P==null): LayoutId==ArtId==identity, so their paths stay unchanged. HoldTier keeps a chapter guard on its deck.
        public string LayoutId,ArtId;internal bool HoldTier;SFEnemyProfile P;
        public float CoverClock,GripTime; public int ClinchHits;
        public CapsuleCollider2D Capsule;
        public bool Busy => action==SFAction.Reload||action==SFAction.Punch||action==SFAction.Kick||action==SFAction.Shoot||action==SFAction.Sweep||action==SFAction.GroundStrike||action==SFAction.ClinchKnee;
        public bool Restrained => action==SFAction.Held||action==SFAction.Knocked;
        bool attackDelivered;
        float animClock, dashClock, footstepClock, strideDistance, lastX, lastLane, crouchBlend, spawnTime, stopClock=-1;
        bool wasRunning;
        public readonly SFDepthMotion DepthMotion=new SFDepthMotion();
        public string CurrentSheet { get; private set; }
        public int CurrentFrame { get; private set; }
        Dictionary<string,SFArt> art=new Dictionary<string,SFArt>();
        bool fieldArtDeferred;
        public float X => transform.position.x;
        public float Height => transform.position.y;
        public Vector2 GroundPosition => new Vector2(X,lane);
        public bool Alive => health>0;
        public Vector3 VisualPosition => new Vector3(X,Height-3+lane,0);

        public void Configure(SFGame owner,string id,bool friendly,Vector2 spawn,bool isBoss=false,float spawnHeight=0)
        {
            game=owner;spawnTime=game.Elapsed; identity=id; hero=friendly;boss=isBoss;ally=id.StartsWith("ally_");P=hero?null:SFEnemyProfiles.Get(id);LayoutId=P!=null?P.layout:id;ArtId=id;ranged=LayoutId=="keel"||LayoutId=="ratchet"||ally;Armed=ranged;Rifle=LayoutId=="ratchet"||ally;lastX=spawn.x;lastLane=spawn.y;
            maxHealth=health=P!=null?P.hp:hero?100:(boss?240:(ranged?52:64));
            transform.position=new Vector3(spawn.x,.15f+spawnHeight,0);transform.localScale=Vector3.one;lane=spawn.y;
            gameObject.layer=7;
            foreach(var old in GetComponentsInChildren<SpriteRenderer>())old.enabled=false;
            foreach(var old in GetComponents<Collider2D>())Destroy(old);
            var body=GetComponent<Rigidbody2D>();if(body==null)body=gameObject.AddComponent<Rigidbody2D>();
            body.bodyType=RigidbodyType2D.Dynamic;body.gravityScale=2.7f;body.constraints=RigidbodyConstraints2D.FreezeRotation;
            body.interpolation=RigidbodyInterpolation2D.Interpolate;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            body.linearVelocity=Vector2.zero;
            Capsule=gameObject.AddComponent<CapsuleCollider2D>();Capsule.size=new Vector2(.52f,2.25f);Capsule.offset=new Vector2(0,1.14f);
            var feet=new GameObject("SF feet").transform;feet.SetParent(transform,false);feet.localPosition=new Vector3(0,.07f,0);
            motor=GetComponent<PlayerMovement>();if(motor==null)motor=gameObject.AddComponent<PlayerMovement>();
            motor.Configure(body,feet,1<<6,P!=null?P.speed:hero?5.2f:(boss?2.5f:3.1f),10.5f);
            motor.HorizontalConstraint=next=>{float x=game.ClampCoverX(this,next);return game.CourseMode?Mathf.Clamp(x,.7f,132):x;}; // Course: the level bound stops the body (and so the walk cycle) instead of snapping a still-moving actor back each frame.
            visual=new GameObject("Artwork").AddComponent<SpriteRenderer>();visual.transform.SetParent(transform,false);visual.sharedMaterial=game.Unlit;
            shadow=game.CreateSprite("Contact shadow",game.Ellipse,new Color(.02f,.03f,.04f,.35f),50);
            shadow.transform.SetParent(game.World,false);
            if(hero)foreach(var kind in new[]{"move","guard","punch","kick","pistol","walk","run","idle","crouch","jump","strike","hook","field_hook_helmet","field_hook_bare","uppercut","field_uppercut_helmet","field_uppercut_bare","roundhouse","field_roundhouse_helmet","field_roundhouse_bare","recoil","rifle","reaction","pistol_walk","pistol_run","stop","aerial","cover","field_aerial","field_aerial_bare","field_north","field_south","field_walk_north","field_walk_south","field_rifle_north","field_rifle_south","field_pistol_north","field_pistol_south","civil_north","civil_south","civil_pistol_south","civil_pistol_north","civil_rifle_north","civil_rifle_south","field_move","field_reaction","field_actions","field_jab","field_straight","field_utility","field_ground","field_rifle","field_rifle_reload","field_rifle_walk","civil_rifle"}){
                bool defer=kind.StartsWith("field_")&&game.CourseMode&&SFResourceCheck.DeferFieldArt;
                art[kind]=defer?null:Resources.Load<SFArt>("SF/"+identity+"_"+kind);
                fieldArtDeferred|=defer;
            }
            // A profiled id without a complete 8-frame base sheet draws its layout's art as one whole set (never mixed with its own sheets).
            else {ArtId=id;var own=Resources.Load<SFArt>("SF/"+id);if(P!=null&&!SFEnemyProfiles.Usable(own,8)){ArtId=P.layout;own=Resources.Load<SFArt>("SF/"+ArtId);Debug.LogWarning("SF art fallback "+id+" -> "+ArtId);}art["enemy"]=own;foreach(var kind in new[]{"walk","idle","strike","reaction","reload"}){var s=Resources.Load<SFArt>("SF/"+ArtId+"_"+kind);art[kind]=P==null||SFEnemyProfiles.Usable(s,1)?s:null;}ResetEnemyWeapon();}
            // Profile scale, like the profile tint, applies only while an id draws its layout's fallback art: an id's own sheets are authored
            // at their intended size (the boss sheets at about 3.15 units), so scaling them again would oversize them. Heroes and existing ids keep 1.
            if(P!=null&&ArtId!=identity)visual.transform.localScale=Vector3.one*P.scale;
            if(hero){var civilianSidearm=Resources.Load<SFArt>("SF/"+identity+"_sidearm");if(civilianSidearm!=null)art["pistol"]=civilianSidearm;}
            if(hero){foreach(string kind in new[]{"ground_combo","clinch_latch","takedown_latch","ground_straight_latch"})art[kind]=Resources.Load<SFArt>("SF/"+identity+"_"+kind);ResetWeapons();}
            Render(0);
        }

        public void Tick(float dt,SFCommand command)
        {
            DepthMotion.ClearMovement();
            TickCombatInput(dt,command);
            triggerBuffer=Mathf.Max(0,triggerBuffer-dt);
            if(command.shoot&&!triggerHeld)triggerBuffer=.18f;
            triggerHeld=command.shoot;
            if(!Armed||!Rifle||Restrained||!Alive)triggerBuffer=0;
            game.TickThrown(this,dt);CounterTime=Mathf.Max(0,CounterTime-dt);
            GripReentryDelay=Mathf.Max(0,GripReentryDelay-dt);
            cooldown=Mathf.Max(0,cooldown-dt);invulnerable=Mathf.Max(0,invulnerable-dt);actionClock+=dt;animClock+=dt;
            TickAerialLanding(dt);
            dashClock=Mathf.Max(0,dashClock-dt);CoverClock+=dt;GripTime-=dt;
            float travelled=Vector2.Distance(new Vector2(X,lane),new Vector2(lastX,lastLane));
            if(travelled<1&&motor.IsGrounded)strideDistance+=travelled;lastX=X;lastLane=lane;
            if(!Alive)
            {
                ReleaseGrip();Crouching=Aiming=false;
                motor.SetCommand(0,false);
                if(hero&&!game.CourseMode){recovery-=dt;if(recovery<=0){health=45;action=SFAction.Idle;invulnerable=2;game.Say(identity=="fernando"?"FERNANDO":"STEFANIE","I'm back. Stay with me.");}}
                Render(dt);return;
            }
            if(action==SFAction.Knocked&&actionClock>2.1f){action=SFAction.Idle;invulnerable=.3f;}
            if(Restrained){motor.SetCommand(0,false);moveAmount=0;Aiming=Crouching=false;Render(dt);return;}
            if(GripTarget!=null&&(GripTime<=0||!GripTarget.Alive||Mathf.Abs(X-GripTarget.X)>2)){ReleaseGrip();}
            if(TickGroundSequence(command))return;
            if(GripTarget!=null&&!Busy&&(command.jump||command.dash||command.guard||command.toggleArmed||command.cycleWeapon||command.weapon!=0))ReleaseGrip();
            if(hero&&Reloading&&(command.toggleArmed||command.cycleWeapon||command.weapon!=0)){action=SFAction.Idle;actionClock=0;cooldown=0;}
            if(Reloading&&actionClock>=attackDuration){(HasEnemyFirearm?EnemyAmmo:Magazine).Reload();action=SFAction.Idle;actionClock=0;cooldown=.10f;triggerBuffer=0;}
            bool attacking=Busy;
            if(attacking)
            {
                if(FlyingKick)TickAerialContact(dt);
                else if(!Reloading&&!attackDelivered&&actionClock>=attackAt)
                {attackDelivered=true;if(action!=SFAction.Shoot||SpendShot())game.ResolveAttack(this,action);}
                if(!Reloading&&actionClock>=attackDuration&&(!FlyingKick||(motor.IsGrounded&&AerialLandingRemaining<=0))){action=GripTarget!=null?SFAction.Grapple:SFAction.Idle;actionClock=0;attacking=false;}
            }
            if(action==SFAction.Hurt&&actionClock>.28f){action=SFAction.Idle;actionClock=0;}
            if(hero&&!attacking&&action!=SFAction.Hurt&&GripTarget==null)
            {
                ChangeWeapon(command);
                if(command.reload&&TryReload())attacking=true;
            }
            Aiming=hero&&Armed&&command.aim&&!Restrained&&GripTarget==null;
            Crouching=command.crouch&&motor.IsGrounded&&(!attacking||action==SFAction.Shoot)&&GripTarget==null;
            crouchBlend=Mathf.MoveTowards(crouchBlend,Crouching?1:0,dt*6);
            Capsule.size=new Vector2(.52f,Crouching?1.22f:2.25f);Capsule.offset=new Vector2(0,Crouching?.625f:1.14f);
            if(command.dash&&hero&&cooldown<=0&&!attacking){dashClock=.2f;invulnerable=.28f;cooldown=.75f;game.Audio.Play("dash",.35f);}
            bool canMove=((!attacking&&action!=SFAction.Hurt)||dashClock>0)&&GripTarget==null&&AerialLandingRemaining<=0;
            if(command.guard&&!attacking){if(action!=SFAction.Guard)GuardTime=0;GuardTime+=dt;action=SFAction.Guard;}else if(action==SFAction.Guard){action=SFAction.Idle;GuardTime=0;}
            float movement=canMove?command.move.x:0;
            if(action==SFAction.Guard)movement*=.3f;
            float gait=hero?(command.run?1:command.walk?.48f:.72f):(command.walk?.48f:1);
            if(Crouching)movement*=.42f;else if(Aiming)movement*=.45f;else movement*=gait;
            if(dashClock>0)movement=facing*1.8f;
            // A committed flying kick retains forward travel; it cannot steer or change depth mid-strike.
            if(FlyingKick&&!motor.IsGrounded&&AerialLandingRemaining<=0)movement=aerialDrive*(actionClock<SFAerialKick.ActiveEnd?1:actionClock<SFAerialKick.Duration?.35f:0);
            if(Mathf.Abs(command.move.x)>.1f&&!attacking)facing=command.move.x>0?1:-1;
            if(Aiming&&command.aimFacing!=0&&!attacking)facing=command.aimFacing;
            float laneBefore=lane;
            // Course platforms are drawn at lane 0: standing on one ignores W/S and settles onto its lip. Mid-air and street depth control stay.
            bool onPlatform=game.CourseMode&&motor.IsGrounded&&Height>.3f&&motor.Body.linearVelocity.y<=.1f; // Not rising: the ground overlap also fires while jumping up through a one-way platform.
            if(canMove&&!onPlatform)lane=game.ClampCoverLane(this,lane+command.move.y*dt*1.65f*(Crouching||Aiming?.45f:gait));
            // Measure depth travel over the same interval that produced it. Dividing
            // last frame's travel by this frame's dt made steady depth movement flicker idle.
            float depthSpeed=dt>0?Mathf.Abs(lane-laneBefore)/dt:0;
            DepthMotion.Step(onPlatform?new Vector2(command.move.x,0):command.move,lane-laneBefore,dt,command.run,canMove&&motor.IsGrounded&&dashClock<=0);
            moveAmount=Mathf.Max(Mathf.Abs(motor.Body.linearVelocity.x),depthSpeed)/(hero?5.2f:3.1f);
            if(onPlatform)lane=Mathf.MoveTowards(lane,0,dt*3f); // After moveAmount so settling never cycles the legs; runs mid-attack too.
            motor.SetCommand(movement,command.jump&&canMove);
            if(command.jump&&motor.IsGrounded&&canMove)game.Audio.Play("jump",.22f);
            if(punchBuffer<=0&&kickBuffer<=0&&AerialLandingRemaining<=0&&game.TryApproachClinch(this,command)){movement=0;moveAmount=0;DepthMotion.ClearMovement();}
            if(!attacking&&action!=SFAction.Hurt&&cooldown<=0&&AerialLandingRemaining<=0)
            {
                if(command.ground&&hero&&motor.IsGrounded)
                {
                    bool groundedTarget=GripTarget!=null||game.Actors.Exists(a=>!a.Friendly&&a.Alive&&a.action==SFAction.Knocked&&SFMath.WithinStrike(GroundPosition,Height,facing,a.GroundPosition,a.Height,1.85f));
                    ReleaseGrip(true);BeginAttack(groundedTarget?SFAction.GroundStrike:SFAction.Sweep);
                }
                else if(command.grapple&&hero){if(this==game.Player)TryGroundSequence();else if(GripTarget!=null)ReleaseGrip();else game.TryGrapple(this);}
                else if(GripTarget!=null&&command.kick&&hero)game.ThrowEnemy(this);
                else if(GripTarget!=null&&command.punch&&hero)BeginAttack(SFAction.ClinchKnee);
                else if(GripTarget==null)
                {
                    if(command.kick||kickBuffer>0){if(!hero||Armed||!motor.IsGrounded||!BeginCombo(true))BeginAttack(SFAction.Kick);kickBuffer=0;}
                    else if((command.punch||punchBuffer>0)&&!(command.guard&&game.CourseMode&&this==game.Partner)){if(hero&&!Armed)BeginCombo(false);else BeginAttack(SFAction.Punch);punchBuffer=0;}
                    else if(hero&&Armed&&(Rifle&&PrecisionRifle?triggerBuffer>0:command.shoot))
                    {
                        triggerBuffer=0;
                        if(Magazine.Loaded>0)BeginAttack(SFAction.Shoot);
                        else {cooldown=.35f;if(this==game.Player)game.Notify(Magazine.Reserve>0?"EMPTY MAGAZINE  /  RELOAD":"NO AMMUNITION");}
                    }
                }
            }
            if(motor.IsGrounded&&travelled<1)footstepClock+=travelled;
            if(moveAmount>.15f&&motor.IsGrounded&&footstepClock>.85f){footstepClock=0;game.Audio.PlayAt(!game.CourseMode&&(game.RouteIndex==3||game.RouteIndex==5)?"grass":"step",X,hero?.18f:.11f);}
            if(stopClock>=0)stopClock+=dt;
            bool neutral=hero&&!Armed&&motor.IsGrounded&&action==SFAction.Idle&&!Crouching&&dashClock<=0&&!command.jump;
            if(neutral&&wasRunning&&command.move.sqrMagnitude<.01f)stopClock=0;
            if(!neutral||command.move.sqrMagnitude>.01f)stopClock=-1;
            wasRunning=neutral&&moveAmount>.8f&&command.move.sqrMagnitude>.01f;
            if(transform.position.x<.7f||transform.position.x>132)motor.Body.position=new Vector2(Mathf.Clamp(X,.7f,132),Height);
            Render(dt);
        }

        public void BeginAttack(SFAction next)
        {
            if(Restrained||!Alive)return;
            if(HasEnemyFirearm&&(Reloading||(next==SFAction.Shoot&&EnemyAmmo.Loaded<=0)))return;
            if(hero&&next==SFAction.Kick&&!motor.IsGrounded&&AerialUsed)return;
            ComboAttack=false;
            action=next;actionClock=0;attackDelivered=false;AerialAttack=next==SFAction.Kick&&!motor.IsGrounded&&Height>.15f;
            attackDuration=next==SFAction.Kick?.7f:next==SFAction.Shoot?.42f:.42f;
            attackAt=next==SFAction.Kick?.32f:next==SFAction.Shoot?.12f:.16f;
            if(next==SFAction.Shoot){ShotRifle=Rifle;ShotAimed=Aiming;if(hero&&Rifle){attackDuration=PrecisionRifle?.38f:.20f;attackAt=PrecisionRifle?.09f:.06f;}}
            if(next==SFAction.Shoot&&hero&&!Rifle&&game.Loadout(this).sidearm==1){attackDuration=.55f;attackAt=.11f;}
            if(next==SFAction.ClinchKnee){attackDuration=.68f;attackAt=.32f;}
            if(next==SFAction.GroundStrike||next==SFAction.Sweep){attackDuration=.8f;attackAt=.34f;}
            if(!hero){attackDuration=boss?1.25f:ranged?1.6f:.85f;attackAt=boss?.7f:ranged?1.1f:.48f;}
            if(hero&&AerialAttack)StartAerialKick();
            cooldown=attackDuration+(hero?.08f:boss?.6f:.5f);
            if(next==SFAction.Punch)combo=(combo+1)%3;
        }

        public bool Damage(float amount,SFActor from,bool unblockable=false,bool preserveGrip=false)
        {
            if(!Alive||invulnerable>0||(from!=null&&from.Friendly==Friendly))return false;
            bool groundContact=action==SFAction.Knocked||(HeldBy!=null&&HeldBy.GroundSequence);
            bool blocked=!unblockable&&action==SFAction.Guard&&from!=null&&(from.X-X)*facing>0;
            if(blocked&&hero&&GuardTime<=.18f&&from.action!=SFAction.Shoot)
            {
                CounterTime=1;from.ReleaseGrip();from.action=SFAction.Hurt;from.actionClock=0;from.cooldown=.65f;
                game.Notify("PARRY  /  COUNTER NOW");game.Audio.PlayAt("guard",X,.4f);invulnerable=.15f;return false;
            }
            amount=game.IncomingDamage(this,amount);
            health=Mathf.Max(0,health-amount*(blocked?.15f:1));invulnerable=hero?.6f:.12f;
            if(!blocked){ResetCombo();triggerBuffer=0;if(Reloading)cooldown=.28f;ReleaseGrip();if(HeldBy!=null&&(!preserveGrip||health<=0))HeldBy.ReleaseGrip();if((action!=SFAction.Knocked&&!(boss&&game.CourseMode&&Busy))||health<=0){action=health<=0?SFAction.Down:preserveGrip&&HeldBy!=null?SFAction.Held:SFAction.Hurt;actionClock=0;}}
            if(health<=0){recovery=hero?8:0;game.Defeated(this);}
            if(!blocked)game.LandedHit(this,from,amount);
            game.Impact(VisualPosition+Vector3.up*(groundContact?.65f:1.4f),blocked?game.Teal:game.Gold,blocked?"GUARD":"",hero?.12f:.055f);
            game.Audio.PlayDamage(this,from,blocked);
            return true;
        }

        public void Revive(){ReleaseGrip();if(HeldBy!=null)HeldBy.ReleaseGrip();health=Mathf.Max(health,55);recovery=0;action=SFAction.Idle;actionClock=0;invulnerable=2;}

        public void ReleaseGrip(bool takedown=false)
        {
            if(GroundSequence){action=SFAction.Idle;actionClock=0;cooldown=.18f;}
            GroundSequence=false;
            if(GripTarget!=null)GripReentryDelay=.6f;
            var target=GripTarget;GripTarget=null;
            if(target!=null&&target.HeldBy==this){target.HeldBy=null;if(target.Alive){target.action=takedown?SFAction.Knocked:SFAction.Idle;target.actionClock=0;target.cooldown=.65f;}}
            if(action==SFAction.Grapple){action=SFAction.Idle;actionClock=0;}
            // A composite owns the victim's visible body only while the actual grip exists.
            // Restore both immediately for throws, damage, defeat and timeout, regardless of tick order.
            if(target!=null&&target.visual!=null)target.Render(0);
            if(ShowingPairedBody&&visual!=null)Render(0);
        }

        public void KnockDown(){if(!Alive)return;ResetCombo();ReleaseGrip();if(HeldBy!=null)HeldBy.ReleaseGrip();action=SFAction.Knocked;actionClock=0;motor.SetCommand(0,false);}

        public void Teleport(Vector2 position)
        {
            if(GroundSequence)ReleaseGrip(true);
            ResetCombo();DepthMotion.Reset();ResetAerialKick();
            CourseStepIndex=-1;
            ThrowClock=ThrowSpeed=0;ThrowOwner=null;ThrowHits.Clear();
            // Keep the interpolated Transform and physics body together across menu/checkpoint transitions.
            transform.position=new Vector3(position.x,position.y,0);
            motor.Body.position=position;
            motor.Body.linearVelocity=Vector2.zero;lastX=position.x;lastLane=lane;stopClock=-1;wasRunning=false;
            motor.SetCommand(0,false);
            Render(0);
        }

        // The Web course starts in civilian clothing. Keep military textures out of memory
        // until requested; the original SFArt objects and frame pixels are loaded unchanged.
        void EnsureFieldArt()
        {
            if(!fieldArtDeferred||!FieldUniform)return;
            foreach(string kind in new List<string>(art.Keys))
                if(kind.StartsWith("field_"))art[kind]=Resources.Load<SFArt>("SF/"+identity+"_"+kind);
            fieldArtDeferred=false;
        }
        public void Render(float dt)
        {
            EnsureFieldArt();
            bool previousPair=ShowingPairedBody;
            string sheet="move";int index=0;
            if(hero)
            {
                if(!Alive||action==SFAction.Knocked){sheet="reaction";index=action==SFAction.Knocked&&actionClock>1.65f?4:3;}
                else if(action==SFAction.Grapple||action==SFAction.Held){sheet="strike";index=2;}
                else if(action==SFAction.ClinchKnee){sheet="roundhouse";index=actionClock<attackAt?1:2;}
                else if(action==SFAction.GroundStrike){sheet="crouch";index=GroundSequence?((int)(Mathf.Max(0,actionClock-.38f)/.12f)%2==0?3:4):actionClock<attackAt?3:4;}
                else if(action==SFAction.Sweep){sheet="roundhouse";index=actionClock<attackAt?1:5;}
                else if(action==SFAction.Punch){sheet="strike";index=ComboAttack?(actionClock<attackAt?1:actionClock<attackAt+.12f?(ComboMove.kind==SFStrike.Straight?3:4):6):Mathf.Clamp((int)(actionClock/attackDuration*8),0,7);}
                else if(action==SFAction.Kick){sheet=AerialAttack?"aerial":"roundhouse";index=AerialAttack?SFAerialKick.Frame(actionClock,AerialLandingRemaining):Mathf.Clamp((int)(actionClock/attackDuration*8),0,7);}
                else if(action==SFAction.Shoot){sheet=ShotRifle?"rifle":"recoil";index=ShotRifle?(Crouching?5:actionClock<attackAt?2:3):Mathf.Clamp((int)(actionClock/attackDuration*8),0,7);
                    if(Crouching&&!ShotRifle&&art["cover"]!=null){sheet="cover";index=5;}}
                else if(action==SFAction.Guard){sheet="guard";index=4;}
                else if(action==SFAction.Hurt){sheet="reaction";index=2;}
                else if(Armed)
                {
                    sheet=Rifle?"rifle":"pistol";index=Crouching?5:Aiming?2:1;
                    if(!Rifle&&(Crouching||crouchBlend>.02f)&&art["cover"]!=null)
                    {sheet="cover";index=Aiming||action==SFAction.Shoot?5:Crouching?Mathf.Min(4,(int)(crouchBlend*5)):Mathf.Clamp(8-(int)(crouchBlend*2),6,8);}
                    // These new sheets still repeat support-leg phases. Preview is explicitly opt-in.
                    // Aim, crouch, airborne and rifle states retain their matching weapon poses.
                    if(game.MotionStudyPreview&&!Rifle&&!Crouching&&!Aiming&&motor.IsGrounded&&moveAmount>.15f)
                    {sheet=moveAmount>.8f?"pistol_run":"pistol_walk";index=(int)(strideDistance/(moveAmount>.8f?3.8f:2.6f)*8)%8;}
                }
                else if(Crouching||crouchBlend>.02f){sheet="crouch";index=Crouching?Mathf.Min(3,(int)(crouchBlend*4)):Mathf.Clamp(7-(int)(crouchBlend*3),5,7);}
                else if(!motor.IsGrounded&&Height>.2f){sheet="jump";index=motor.Body.linearVelocity.y>1?3:4;}
                else if(game.MotionStudyPreview&&stopClock>=0&&stopClock<.42f){sheet="stop";index=Mathf.Clamp((int)(stopClock/.42f*6),0,5);}
                else if(moveAmount>.15f){sheet=moveAmount>.8f?"run":"walk";index=(int)(strideDistance/(sheet=="run"?3.8f:2.6f)*8)%8;}
                else {sheet="idle";index=0;} // Hold a stable silhouette until a body-consistent breathing atlas is available.
            }
            else
            {
                sheet="enemy";
                if(!Alive)index=boss?7:ranged?7:7;
                else if(action==SFAction.Hurt)index=boss?5:ranged?6:7;
                else if(action==SFAction.Punch||action==SFAction.Shoot)index=boss?(actionClock<attackAt?1:2):ranged?(actionClock<attackAt?4:5):(actionClock<attackAt?4:5+combo%2);
                else if(moveAmount>.1f)index=boss?4:ranged?1+(int)(animClock*6)%2:(int)(animClock*8)%4;
                else index=boss?0:ranged?0:4;
                if((!Alive||action==SFAction.Knocked)&&art["reaction"]!=null){sheet="reaction";index=3;}
                else if((action==SFAction.Hurt||action==SFAction.Held)&&art["reaction"]!=null){sheet="reaction";index=1;}
                else if(action==SFAction.Punch&&art["strike"]!=null){sheet="strike";index=Mathf.Clamp((int)(actionClock/attackDuration*8),0,7);}
                else if(action==SFAction.Idle&&moveAmount>.1f&&art["walk"]!=null){sheet="walk";index=(int)(strideDistance/2.4f*art["walk"].frames.Length)%art["walk"].frames.Length;}
                else if(action==SFAction.Idle&&moveAmount<.1f&&art["idle"]!=null){sheet="idle";index=(int)((Variety>0?animClock+.37f*Variety:animClock)*5)%8;}
                // Vesper's base atlas already supplies kneeling and braced poses.
                // Restrained reactions override stale movement without altering attacks.
                if(LayoutId=="vesper"&&Alive&&art["reaction"]==null)
                {
                    if(action==SFAction.Knocked||action==SFAction.Held){sheet="enemy";index=6;}
                    else if(action==SFAction.Guard){sheet="enemy";index=4;}
                }
            }
            if(!hero&&(LayoutId=="silk"||LayoutId=="ratchet"||LayoutId=="foreman"||LayoutId=="cantilever"))
            {
                sheet="enemy";index=0;
                if(LayoutId=="ratchet")index=!Alive?7:action==SFAction.Knocked||Crouching?3:action==SFAction.Hurt?6:action==SFAction.Shoot?4:moveAmount>.15f?1:0;
                else if(LayoutId=="silk")index=!Alive||action==SFAction.Knocked?1:action==SFAction.Kick?(actionClock<attackAt?2:actionClock<attackAt+.15f?4:6):action==SFAction.Hurt?1:0;
                else if(LayoutId=="foreman")index=!Alive||action==SFAction.Knocked?6:action==SFAction.Hurt?5:Busy?(actionClock<attackAt?1:actionClock<attackAt+.2f?2:3):0;
                else index=!Alive||action==SFAction.Knocked?6:action==SFAction.Hurt?4:Busy?(actionClock<attackAt?0:action==SFAction.Kick?2:1):0;
                if(art["reaction"]!=null)
                {
                    if(!Alive||action==SFAction.Knocked){sheet="reaction";index=Alive&&actionClock>1.65f?4:3;}
                    else if(action==SFAction.Hurt||action==SFAction.Held){sheet="reaction";index=1;}
                }
            }
            if(!hero&&LayoutId=="keel"&&Crouching)index=3;
            if(HasEnemyFirearm&&Alive&&Reloading&&art.TryGetValue("reload",out var enemyReload)&&enemyReload!=null)
            {sheet="reload";index=SFReloadAnimation.Frame(actionClock,attackDuration);}
            if(ally)
            {sheet="enemy";index=action==SFAction.Shoot?5:!Alive?4:0;
                if(Alive&&moveAmount>.1f&&!Busy&&art["walk"]!=null){sheet="walk";index=(int)(strideDistance/2.4f*art["walk"].frames.Length)%art["walk"].frames.Length;}}
            if(hero&&!FieldUniform&&Armed&&Rifle&&art.TryGetValue("civil_rifle",out var civilianRifle)&&civilianRifle!=null&&(action==SFAction.Idle||action==SFAction.Shoot||action==SFAction.Reload))
            {sheet="civil_rifle";index=Crouching?3:action==SFAction.Shoot?(actionClock<attackAt?1:2):Aiming?1:0;}
            if(hero&&FieldUniform)
            {
                // Limited military key-pose set: retain the selected outfit across every state.
                int offset=Helmet?0:4;
                if(art["field_actions"]!=null)
                {
                    sheet="field_actions";index=offset;
                    if(Armed||action==SFAction.Shoot)index=offset+3;
                    if(action==SFAction.Punch)index=offset+(actionClock>=attackAt-.05f&&actionClock<attackAt+.15f?1:0);
                    if(action==SFAction.ClinchKnee||action==SFAction.Grapple)index=offset+2;
                    if(Crouching||action==SFAction.Sweep||action==SFAction.GroundStrike)index=offset+2;
                }
                if(Armed&&art["field_utility"]!=null){if(Rifle){sheet="field_utility";index=offset+(Crouching?1:0);}else if(Crouching){sheet="field_utility";index=offset+2;}}
                if(Armed&&Rifle&&art["field_rifle"]!=null&&(action==SFAction.Idle||action==SFAction.Shoot||action==SFAction.Reload))
                {
                    sheet="field_rifle";index=offset+(Crouching?3:action==SFAction.Shoot?(actionClock<attackAt?1:2):Aiming?1:0);
                    if(action==SFAction.Idle&&!Crouching&&!Aiming&&motor.IsGrounded&&moveAmount>.15f&&art["field_rifle_walk"]!=null)
                    {sheet="field_rifle_walk";index=offset+(int)(strideDistance/2.6f*4)%4;}
                }
                if(!Armed&&Crouching&&art["field_ground"]!=null){sheet="field_ground";index=Helmet?0:3;}
                if((action==SFAction.GroundStrike||action==SFAction.Sweep)&&art["field_ground"]!=null){sheet="field_ground";index=(Helmet?0:3)+(GroundSequence?(int)(Mathf.Max(0,actionClock-.38f)/.12f)%2:actionClock>=attackAt-.05f?1:0);}
                if(Helmet&&(!Alive||action==SFAction.Knocked||action==SFAction.Hurt)&&art["field_reaction"]!=null){sheet="field_reaction";index=!Alive||action==SFAction.Knocked?3:1;}
                if((!Alive||action==SFAction.Knocked)&&art["field_ground"]!=null){sheet="field_ground";index=(Helmet?0:3)+2;}
                if(action==SFAction.Kick)
                {
                    string aerialSheet=Helmet?"field_aerial":"field_aerial_bare";
                    if(AerialAttack&&art.TryGetValue(aerialSheet,out var aerial)&&aerial!=null&&aerial.frames.Length==6)
                    {sheet=aerialSheet;index=SFAerialKick.Frame(actionClock,AerialLandingRemaining);}
                    else if(art["field_utility"]!=null){sheet="field_utility";index=offset+3;}
                }
            }
            // Grounded unarmed kicks share the authored hit time; airborne and weapon poses retain priority.
            if(hero&&Alive&&!Armed&&action==SFAction.Kick&&!AerialAttack)
            {
                string roundhouseSheet=FieldUniform?(Helmet?"field_roundhouse_helmet":"field_roundhouse_bare"):"roundhouse";
                if(art.TryGetValue(roundhouseSheet,out var roundhouse)&&roundhouse!=null&&roundhouse.frames.Length==8)
                {sheet=roundhouseSheet;index=SFStrikeAnimation.EightPoseFrame(actionClock,attackAt,attackDuration);}
            }
            if(hero&&Alive&&FieldUniform&&!Armed&&action==SFAction.Punch&&ComboAttack&&(ComboMove.kind==SFStrike.Jab||ComboMove.kind==SFStrike.Straight))
            {
                string boxingSheet=ComboMove.kind==SFStrike.Jab?"field_jab":"field_straight";
                if(art.TryGetValue(boxingSheet,out var boxingArt)&&boxingArt!=null&&boxingArt.frames.Length==6)
                {sheet=boxingSheet;index=(Helmet?0:3)+SFStrikeAnimation.ThreePoseFrame(actionClock,attackAt,attackDuration);}
            }
            if(hero&&Alive&&!Armed&&action==SFAction.Punch&&ComboAttack&&ComboMove.kind==SFStrike.Uppercut)
            {
                string uppercutSheet=FieldUniform?(Helmet?"field_uppercut_helmet":"field_uppercut_bare"):"uppercut";
                if(art.TryGetValue(uppercutSheet,out var uppercut)&&uppercut!=null&&uppercut.frames.Length==8)
                {sheet=uppercutSheet;index=SFStrikeAnimation.UppercutFrame(actionClock,attackAt,attackDuration);}
            }
            if(hero&&Alive&&!Armed&&action==SFAction.Punch&&ComboAttack&&ComboMove.kind==SFStrike.Hook)
            {
                string hookSheet=FieldUniform?(Helmet?"field_hook_helmet":"field_hook_bare"):"hook";
                if(art.TryGetValue(hookSheet,out var hook)&&hook!=null&&hook.frames.Length==8)
                {sheet=hookSheet;index=SFStrikeAnimation.EightPoseFrame(actionClock,attackAt,attackDuration);}
            }
            if(hero&&Alive&&FieldUniform&&Armed&&Rifle&&Reloading&&art.TryGetValue("field_rifle_reload",out var reload)&&reload!=null&&reload.frames.Length==8)
            {sheet="field_rifle_reload";index=(Helmet?0:4)+SFReloadAnimation.Frame(actionClock,attackDuration);}
            if(hero&&Alive&&!Armed&&!FieldUniform&&GroundSequence&&action==SFAction.GroundStrike&&art.TryGetValue("ground_combo",out var groundArt)&&groundArt!=null&&groundArt.frames.Length==4)
            {sheet="ground_combo";index=SFGroundAnimation.Frame(actionClock);}
            // Only loaded weapon-specific depth art may override the side rig; absent outfit/direction assets retain their matching rig.
            bool depthPose=hero&&!Crouching&&crouchBlend<.02f&&!Aiming&&Alive&&action==SFAction.Idle&&motor.IsGrounded&&DepthMotion.Moving;
            string depthSheet=(FieldUniform?"field_":"civil_")+(Armed?(Rifle?"rifle_":"pistol_"):"")+(DepthMotion.Direction>0?"north":"south");
            depthPose&=art.TryGetValue(depthSheet,out var depthArt)&&depthArt!=null;
            // Legacy civilian sheets use the lower eight poses; eight-frame civilian composites use their full walk/run rows.
            if(depthPose)
            {
                sheet=depthSheet;index=DepthMotion.Frame(FieldUniform&&Helmet||!FieldUniform&&depthArt.frames.Length==8);
                // The reviewed Fernando front pistol sheet puts each raised knee after its own contact.
                // Play 0,3,2,1 within each row so the opposite leg passes the planted contact foot.
                if(identity=="fernando"&&sheet=="field_pistol_south")index=index/4*4+(4-index%4)%4;
                // Individually authored low walking phases replace only unarmed field walks.
                string walkSheet="field_walk_"+(DepthMotion.Direction>0?"north":"south");
                if(FieldUniform&&!Armed&&!DepthMotion.Running&&art.TryGetValue(walkSheet,out var walkArt)&&walkArt!=null&&walkArt.frames.Length==8)
                {sheet=walkSheet;index=(Helmet?0:4)+index%4;}
            }
            bool pairedClinch=UsePairedClinch;
            if(pairedClinch){sheet="clinch_latch";index=SFClinchAnimation.Frame(action,actionClock,attackAt,attackDuration);}
            bool pairedGround=UsePairedGround,pairedBody=pairedClinch||pairedGround;
            if(pairedGround)
            {
                index=SFTakedownAnimation.Frame(actionClock);sheet="takedown_latch";
                if(index==4){sheet="ground_straight_latch";index=0;}
            }
            if(!art.TryGetValue(sheet,out var selected)||selected==null)selected=art[hero?"move":"enemy"];
            if(selected==null)return;
            CurrentSheet=sheet;CurrentFrame=Mathf.Clamp(index,0,selected.frames.Length-1);
            visual.sprite=selected.Frame(index);visual.flipX=!depthPose&&facing<0;
            visual.transform.localPosition=new Vector3(0,-3+lane,0);
            visual.transform.localRotation=Quaternion.identity;
            visual.sortingOrder=100+Mathf.RoundToInt(-lane*35);
            float fade=!hero&&!Alive?Mathf.Clamp01(1-(actionClock-1.5f)/2):1;
            if(ally)fade*=Mathf.Clamp01((game.Elapsed-spawnTime)/.45f);
            Color tint=hero?new Color(.83f,.88f,.96f,fade):new Color(.84f,.87f,.94f,fade);
            // Only a profiled id drawn with its layout's art is tinted, so a fallback reads as a different person.
            if(P!=null&&ArtId!=identity)tint*=P.tint;
            // Repeated enemy persons take a subtle per-instance variant (SFFeel.cs) before the hit and wind-up lerps; 0 changes nothing.
            if(Variety>0)tint*=VarietyTint(Variety);
            if(invulnerable>0&&Alive)tint=Color.Lerp(tint,Color.white,.55f);
            if(!Friendly&&Alive&&(action==SFAction.Punch||action==SFAction.Kick||action==SFAction.Shoot)&&actionClock<attackAt)tint=Color.Lerp(tint,new Color(1,.55f,.4f),.28f+.22f*Mathf.Sin(actionClock*20));
            visual.color=tint;
            bool pistolStudy=!FieldUniform&&Armed&&!Rifle&&game.MotionStudyPreview&&!Aiming;
            // Slow analog depth movement still needs walking legs when directional art is absent.
            bool rigEligible=!depthPose&&hero&&(FieldUniform||Armed)&&!pistolStudy&&Alive&&action==SFAction.Idle&&!Crouching&&motor.IsGrounded&&(moveAmount>.15f||DepthMotion.Moving);
            if(rigEligible&&FieldUniform&&LocomotionRig==null)
            {
                var candidate=new GameObject("Authored field locomotion").AddComponent<SFLocomotionRig>();
                if(candidate.Initialize(this,art["field_rifle_walk"],Resources.Load<SFArt>("SF/field_leg_parts")))LocomotionRig=candidate;
                else Destroy(candidate.gameObject);
            }
            if(rigEligible&&!FieldUniform&&CivilianLocomotionRig==null)
            {
                var candidate=new GameObject("Authored civilian firearm locomotion").AddComponent<SFLocomotionRig>();
                if(candidate.Initialize(this,art["civil_rifle"],Resources.Load<SFArt>("SF/civil_leg_parts"),true,art["pistol"]))CivilianLocomotionRig=candidate;
                else Destroy(candidate.gameObject);
            }
            var activeRig=rigEligible?(FieldUniform?LocomotionRig:CivilianLocomotionRig):null;
            if(LocomotionRig!=null&&LocomotionRig!=activeRig)LocomotionRig.Suspend(strideDistance);
            if(CivilianLocomotionRig!=null&&CivilianLocomotionRig!=activeRig)CivilianLocomotionRig.Suspend(strideDistance);
            visual.enabled=activeRig==null&&(HeldBy==null||!HeldBy.UsePairedBody);
            if(pairedBody&&GripTarget.visual!=null)GripTarget.visual.enabled=false;
            if(activeRig!=null)
            {
                int bodyMode=!Armed?1:Rifle?0:2;
                activeRig.Advance(strideDistance,moveAmount>.8f,Helmet,facing,tint,visual.sortingOrder,bodyMode,Aiming,motor.Body.linearVelocity.x*facing<-.1f,dt);
                CurrentSheet=FieldUniform?(bodyMode==0?"field_rifle_rig":bodyMode==1?"field_unarmed_rig":"field_pistol_rig"):(Rifle?"civil_rifle_rig":"civil_pistol_rig");
                CurrentFrame=(int)(activeRig.Phase*8);
            }
            bool surfaceShadow=game.CourseMode&&motor.IsGrounded&&motor.Body.linearVelocity.y<=.1f; // Course: a grounded actor's shadow sits under its feet, platforms included.
            float s=surfaceShadow?1:1/(1+Mathf.Max(0,Height)*.18f);
            shadow.transform.position=new Vector3(X,-3+(surfaceShadow?Height:0)+lane+.035f,0);shadow.transform.localScale=new Vector3(1.1f*s,.20f*s,1);
            shadow.color=new Color(.01f,.02f,.03f,.32f*fade);
            // Outfit/weapon/identity changes must not leave an opponent hidden until its next tick.
            if(previousPair&&!pairedBody&&GripTarget!=null&&GripTarget.visual!=null)GripTarget.Render(0);
        }
        public SFLocomotionRig LocomotionRig {get;private set;}
        public SFLocomotionRig CivilianLocomotionRig {get;private set;}
    }
}
