using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // Ground-plane footprints are independent from jump elevation and from decorative pixels.
    public sealed class SFCover
    {
        public Rect footprint;
        public float height;
        public string label;
        public SpriteRenderer visual;
        public bool Blocks(Vector2 from,float fromHeight,Vector2 to,float toHeight)=>Clip(from,fromHeight,to,toHeight,out _);
        public bool BlocksMovement(Vector2 from,Vector2 to)
        {
            // Navigation must clear the same body radius enforced by physical movement.
            float enter=0,exit=1;Vector2 d=to-from;
            return Slab(from.x,d.x,footprint.xMin-.34f,footprint.xMax+.34f,ref enter,ref exit)&&
                Slab(from.y,d.y,footprint.yMin-.26f,footprint.yMax+.26f,ref enter,ref exit);
        }
        public bool Clip(Vector2 from,float fromHeight,Vector2 to,float toHeight,out float enter)
        {
            enter=0;float exit=1;Vector2 d=to-from;
            if(!Slab(from.x,d.x,footprint.xMin,footprint.xMax,ref enter,ref exit)||
               !Slab(from.y,d.y,footprint.yMin,footprint.yMax,ref enter,ref exit))return false;
            return Mathf.Min(Mathf.Lerp(fromHeight,toHeight,enter),Mathf.Lerp(fromHeight,toHeight,exit))<height;
        }
        static bool Slab(float start,float delta,float min,float max,ref float enter,ref float exit)
        {
            if(Mathf.Abs(delta)<.00001f)return start>=min&&start<=max;
            float a=(min-start)/delta,b=(max-start)/delta;if(a>b){float t=a;a=b;b=t;}
            enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);return enter<=exit;
        }
    }

    public sealed partial class SFGame
    {
        public bool CourseMode;
        public int RouteIndex;
        public static readonly string[] RouteNames={"CURITIBA","MIAMI","RIO / HILLS","CAMP LEJEUNE","FORT LIBERTY","AL ANBAR"};
        public static readonly string[] RouteAllies={"tigre","swat","bope","marine","airborne","marine"};
        public static readonly string[] AllyNames={"PCPR / TIGRE","US POLICE SWAT","BOPE","US MARINES","82ND AIRBORNE","US MARINES"};
        public string RouteName=>CourseMode?(InChapter?Chapter.city:"CURITIBA"):RouteNames[RouteIndex];
        public readonly List<SFCover> Covers=new List<SFCover>();
        public float BackupCooldown,BackupArrival,BackupRemaining;
        public int BackupCalls;
        public bool BackupActive=>BackupArrival>0||BackupRemaining>0;
        public string BackupStatus=>BackupArrival>0?"ARRIVING "+BackupArrival.ToString("0.0")+"s":BackupRemaining>4?"ON STATION "+(BackupRemaining-4).ToString("0")+"s":BackupRemaining>0?"DEPARTING":BackupCooldown>0?"RECHARGING "+BackupCooldown.ToString("0")+"s":"READY";
        Transform sandboxStage;
        Transform StageRoot=>sandboxStage!=null?sandboxStage:World;
        SpriteRenderer supportVehicle;
        float vehicleStop;
        readonly List<Texture2D> joinTextures=new List<Texture2D>();
        readonly List<Sprite> joinSprites=new List<Sprite>();

        public void SelectRoute(int index)
        {
            if(State!=SFState.Menu||CourseMode)return;
            RouteIndex=(index+RouteNames.Length)%RouteNames.Length;
            if(sandboxStage!=null){sandboxStage.gameObject.SetActive(false);Destroy(sandboxStage.gameObject);}
            foreach(var s in joinSprites)Destroy(s);foreach(var t in joinTextures)Destroy(t);
            joinSprites.Clear();joinTextures.Clear();Pickups.Clear();stageProps.Clear();Covers.Clear();
            BuildSandboxLevel();SpawnEnemies();SetState(SFState.Menu);
        }

        void BuildSandboxLevel()
        {
            Platforms=Array.Empty<SFPlatform>();Hazards=Array.Empty<float>();
            sandboxStage=new GameObject("Combat sandbox scenery").transform;sandboxStage.SetParent(World,false);
            plates=new SpriteRenderer[6];
            for(int i=0;i<6;i++)
            {
                string id=RouteIndex==0?"curitiba_"+i:"plate_"+RouteIndex+"_"+i;
                var art=Resources.Load<SFArt>("SF/"+id);
                var sr=CreateSprite(RouteName+" / plate "+(i+1),art.Frame(0),Color.white,-100+i);
                sr.transform.SetParent(StageRoot,false);sr.transform.position=new Vector3(i*22+11,.35f,0);
                sr.transform.localScale=Vector3.one*(24/sr.sprite.bounds.size.x);
                if(i>0)sr.sprite=FeatherPlate(sr.sprite);
                plates[i]=sr;
            }
            var ground=new GameObject("Sandbox ground");ground.layer=6;ground.transform.SetParent(StageRoot);ground.transform.position=new Vector3(66,-.25f,0);
            ground.AddComponent<BoxCollider2D>().size=new Vector2(140,.5f);
            // Authored cover uses supplied prop artwork; no platform bars, objective rings or hazards.
            if(RouteIndex==1)
            {
                AddCover("props_coastal",0,15,0,2.5f,1.25f,"PLANTER / LOW COVER");
                AddCover("props_coastal",1,36,.05f,3,1.35f,"STUCCO WALL / LOW COVER");
                AddCover("props_coastal",4,56,0,2.6f,2.25f,"UTILITY CABINET / TALL COVER");
                AddCover("props_coastal",2,79,-.05f,3,1.5f,"BENCH / LOW COVER");
                AddCover("props_coastal",5,102,0,3.2f,1.8f,"KIOSK / COVER");
            }
            else if(RouteIndex>=3)
            {
                AddCover("props_sideon",1,15,0,3,1.4f,"SANDBAGS / LOW COVER");
                AddCover("props_sideon",0,36,.05f,2.7f,1.25f,"CONCRETE / LOW COVER");
                AddCover("props_desert",5,56,0,3,1.6f,"GABION / COVER");
                AddCover("props_desert",1,79,-.05f,3,1.65f,"MASONRY / COVER");
                AddCover("props_desert",3,102,0,2.5f,1.35f,"EQUIPMENT CHEST / LOW COVER");
            }
            else
            {
                AddCover("props_urban",0,15,0,2.5f,1.25f,"PLANTER / LOW COVER");
                AddCover("props_sideon",0,36,.05f,2.7f,1.25f,"CONCRETE / LOW COVER");
                AddCover("props_urban",3,56,0,2.6f,2.25f,"CABINET / TALL COVER");
                AddCover("props_urban",4,79,-.05f,2.8f,1.5f,"BENCH / LOW COVER");
                AddCover("props_sideon",2,102,0,3,1.8f,"MASONRY / COVER");
            }
            AddPickup(10,.8f,-.75f,"food");AddPickup(30,.8f,-.75f,"medical");
            AddPickup(47,.8f,.8f,"drink");AddPickup(67,.8f,-.75f,"ammo");
            AddPickup(87,.8f,.8f,"food");AddPickup(111,.8f,-.75f,"medical");
        }

        Sprite FeatherPlate(Sprite source)
        {
            // Blend only the incoming edge over the preceding opaque plate. The source files stay intact.
            // This softens the join; it does not repair mismatched architecture or perspective.
            var tex=source.texture;var rt=RenderTexture.GetTemporary(tex.width,tex.height,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;Graphics.Blit(tex,rt);RenderTexture.active=rt;
            var copy=new Texture2D(tex.width,tex.height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);copy.Apply();
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
            var p=copy.GetPixels32();int blend=Mathf.CeilToInt(tex.width/12f);
            for(int y=0;y<tex.height;y++)for(int x=0;x<blend;x++)p[y*tex.width+x].a=(byte)(255*Mathf.SmoothStep(0,1,x/(float)blend));
            copy.SetPixels32(p);copy.Apply(false,true);copy.wrapMode=TextureWrapMode.Clamp;copy.filterMode=FilterMode.Bilinear;
            var sprite=Sprite.Create(copy,new Rect(0,0,copy.width,copy.height),new Vector2(.5f,.5f),source.pixelsPerUnit);
            joinTextures.Add(copy);joinSprites.Add(sprite);return sprite;
        }

        void AddCover(string sheet,int frame,float x,float lane,float width,float height,string label)
        {
            var art=Resources.Load<SFArt>("SF/"+sheet);var sr=CreateSprite(label,art.Frame(frame),Color.white,100-Mathf.RoundToInt(lane*35));
            sr.transform.SetParent(StageRoot,false);sr.transform.position=new Vector3(x,-3+lane,0);
            sr.transform.localScale=Vector3.one*(width/sr.sprite.bounds.size.x);stageProps.Add(sr);
            if(sheet=="props_sideon")height=sr.sprite.bounds.size.y*sr.transform.localScale.y;
            Covers.Add(new SFCover{footprint=new Rect(x-width*.43f,lane-.22f,width*.86f,.44f),height=height,label=label,visual=sr});
        }

        public float ClampCoverX(SFActor actor,float next)
        {
            foreach(var c in Covers)
            {
                if(actor.Height>c.height||actor.lane<c.footprint.yMin-.19f||actor.lane>c.footprint.yMax+.19f)continue;
                float left=c.footprint.xMin-.28f,right=c.footprint.xMax+.28f;
                if(actor.X<=left+.01f&&next>left)next=left;
                else if(actor.X>=right-.01f&&next<right)next=right;
            }
            return next;
        }
        public float ClampCoverLane(SFActor actor,float next)
        {
            foreach(var c in Covers)
            {
                if(actor.Height>c.height||actor.X<c.footprint.xMin-.28f||actor.X>c.footprint.xMax+.28f)continue;
                float back=c.footprint.yMax+.20f,front=c.footprint.yMin-.20f;
                if(actor.lane>=back-.01f&&next<back)next=back;
                else if(actor.lane<=front+.01f&&next>front)next=front;
            }
            return Mathf.Clamp(next,CourseMode?-.55f:-1.1f,CourseMode?.55f:1.1f);
        }
        public SFCover ShotCover(SFActor from,SFActor to)=>Covers.Where(c=>c.Blocks(from.GroundPosition,from.Height+from.MuzzleHeight,to.GroundPosition,to.Height+(to.Crouching?.85f:1.55f))).OrderBy(c=>Mathf.Abs(c.footprint.center.x-from.X)).FirstOrDefault();
        public Vector3 ShotEndpoint(SFActor actor,float range)
        {
            Vector2 from=actor.GroundPosition,to=from+Vector2.right*actor.facing*range;
            float height=actor.Height+actor.MuzzleHeight,fraction=1;
            foreach(var c in Covers)if(c.Clip(from,height,to,height,out float enter))fraction=Mathf.Min(fraction,enter);
            Vector2 end=Vector2.Lerp(from,to,fraction);
            return new Vector3(end.x,-3+end.y+height,0);
        }

        void OnDestroy()
        {
            foreach(var sprite in joinSprites)if(sprite!=null)Destroy(sprite);
            foreach(var texture in joinTextures)if(texture!=null)Destroy(texture);
            foreach(var sprite in new[]{Pixel,Ellipse,Ring})if(sprite!=null){Destroy(sprite.texture);Destroy(sprite);}
        }

        public bool ClearAttack(SFActor from,SFActor to,bool shot)=>shot?ShotCover(from,to)==null:!Covers.Any(c=>c.Blocks(from.GroundPosition,from.Height+.55f,to.GroundPosition,to.Height+.55f));
        public string CoverHint()
        {
            var c=Covers.FirstOrDefault(p=>Mathf.Abs(p.footprint.center.x-Player.X)<p.footprint.width*.5f+1.4f&&Mathf.Abs(p.footprint.center.y-Player.lane)<.65f);
            if(c==null)return "";
            var threat=Actors.Where(a=>!a.Friendly&&a.Alive&&Mathf.Abs(a.X-Player.X)<12).OrderBy(a=>Mathf.Abs(a.X-Player.X)).FirstOrDefault();
            if(Player.Crouching&&threat!=null)return ShotCover(threat,Player)!=null?"LOW STANCE  /  NEAREST THREAT BLOCKED":"LOW STANCE  /  EXPOSED TO NEAREST THREAT";
            return Player.Crouching?"LOW STANCE  /  WATCH THE FLANKS":TouchControls?"HOLD CROUCH FOR COVER   ·   STICK TO FLANK":GamepadActive?"LT  LOW COVER   ·   LEFT STICK  MOVE AROUND":"CTRL  LOW COVER   ·   W / S  MOVE AROUND";
        }

        SFCommand Navigate(SFActor actor,Vector2 goal)
        {
            goal.x=Mathf.Clamp(goal.x,1,131.5f);goal.y=Mathf.Clamp(goal.y,-1.1f,1.1f);
            Vector2 delta=goal-actor.GroundPosition;
            var obstacle=Covers.Where(c=>actor.Height<=c.height&&c.BlocksMovement(actor.GroundPosition,goal)).OrderBy(c=>Mathf.Abs(c.footprint.center.x-actor.X)).FirstOrDefault();
            if(obstacle!=null)
            {
                // Stay on the nearer open side until the entire footprint has been passed.
                float side=actor.lane>=obstacle.footprint.center.y?1:-1;
                goal.y=obstacle.footprint.center.y+side*.8f;
                if(Mathf.Abs(actor.X-obstacle.footprint.center.x)<obstacle.footprint.width*.5f+.9f&&Mathf.Abs(actor.lane-goal.y)>.12f)delta.x=0;
                delta.y=goal.y-actor.lane;
            }
            return new SFCommand{move=new Vector2(Mathf.Abs(delta.x)>.32f?Mathf.Clamp(delta.x,-1,1):0,Mathf.Abs(delta.y)>.10f?Mathf.Clamp(delta.y*3,-1,1):0),walk=actor.ally};
        }

        public Vector2 FormationGoal(float distance,float lane)
        {
            float x=Player.X-Player.facing*distance;
            // Place the follower on the available side at either map boundary, preserving spacing.
            if(x<1||x>131.5f)x=Player.X+Player.facing*distance;
            return new Vector2(Mathf.Clamp(x,1,131.5f),Mathf.Clamp(lane,CourseMode?-.55f:-1.1f,CourseMode?.55f:1.1f));
        }

        public void ToggleMotionPreview()
        {
            MotionStudyPreview=!MotionStudyPreview;
            Notify(MotionStudyPreview?"MOVEMENT STUDIES ON  /  F6 TO COMPARE":"MOVEMENT STUDIES OFF");
        }

        public bool CallBackup()
        {
            if(CourseMode){Notify("BACKUP UNAVAILABLE IN THIS CHAPTER");return false;}
            if(State!=SFState.Playing||!Player.Alive||BackupCooldown>0||BackupActive)return false;
            BackupCalls++;BackupArrival=2.5f;BackupCooldown=50;vehicleStop=Mathf.Clamp(Player.X-5,5,125);
            Say(AllyNames[RouteIndex],"Support acknowledged. Moving to your position.");Audio.Play("assist",.22f);return true;
        }
        void TickBackup(float dt)
        {
            BackupCooldown=Mathf.Max(0,BackupCooldown-dt);
            if(BackupArrival>0)
            {
                BackupArrival-=dt;
                if(BackupArrival<=0)
                {
                    BackupRemaining=24;
                    for(int i=0;i<2;i++)
                    {
                        var a=CreateActor("ally_"+RouteAllies[RouteIndex],false,new Vector2(Mathf.Max(1,vehicleStop-2-i),i==0?.85f:-.85f));
                        a.motor.Freeze(false);
                    }
                    if(RouteIndex==1)
                    {
                        var vehicle=Resources.Load<SFArt>("SF/vehicle_bearcat");supportVehicle=CreateSprite("SWAT support vehicle",vehicle.Frame(0),Color.white,50);
                        supportVehicle.transform.SetParent(World);supportVehicle.transform.position=new Vector3(vehicleStop-10,-2.1f,0);supportVehicle.transform.localScale=Vector3.one*(6.5f/supportVehicle.sprite.bounds.size.x);
                    }
                    Notify(AllyNames[RouteIndex]+"  /  SUPPORT ON STATION");
                }
            }
            if(BackupRemaining<=0)return;
            BackupRemaining=Mathf.Max(0,BackupRemaining-dt);
            if(supportVehicle!=null){float target=BackupRemaining>4?vehicleStop:vehicleStop-15;var p=supportVehicle.transform.position;p.x=Mathf.MoveTowards(p.x,target,dt*4);supportVehicle.transform.position=p;}
            if(BackupRemaining<=0)
            {
                foreach(var ally in Actors.Where(a=>a.ally).ToArray()){Actors.Remove(ally);Destroy(ally.shadow.gameObject);Destroy(ally.gameObject);}
                if(supportVehicle!=null)Destroy(supportVehicle.gameObject);
                Notify("SUPPORT WITHDRAWN  /  RECHARGING");
            }
        }
        SFCommand AllyCommand(SFActor ally)
        {
            if(!ally.Alive)return default;
            if(BackupRemaining<4)return Navigate(ally,new Vector2(Mathf.Max(1,vehicleStop-12),ally.lane));
            var target=Actors.Where(a=>!a.Friendly&&a.Alive&&Mathf.Abs(a.X-Player.X)<11).OrderBy(a=>Vector2.Distance(a.GroundPosition,ally.GroundPosition)).FirstOrDefault();
            if(target==null)return Navigate(ally,FormationGoal(3+(Actors.IndexOf(ally)%2)*1.5f,ally.lane>=0?.85f:-.85f));
            float direction=Mathf.Sign(target.X-ally.X);ally.facing=direction>=0?1:-1;
            if(Mathf.Abs(target.X-ally.X)<9&&Mathf.Abs(target.lane-ally.lane)<.5f&&ClearAttack(ally,target,true))
            {if(!ally.Busy&&ally.action!=SFAction.Hurt&&ally.cooldown<=0)ally.BeginAttack(SFAction.Shoot);return default;}
            return Navigate(ally,new Vector2(target.X-direction*5,target.lane));
        }
    }
}
