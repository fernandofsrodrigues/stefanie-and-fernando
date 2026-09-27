using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // F13 chapter route bot: -sfChapter N -sfRoute on the desktop, or RunChapterQA("N") in a local WebGL build (qa=rio / qa=anbar).
    // Normal commands and the public player actions only (swap, assist, RETRY FROM CHECKPOINT, RESUME): no teleports, health edits,
    // forced pickups or forced defeats. The lead walks the fixture waypoints; between two of them a small planner over the chapter's decks
    // picks the next climb (jump from under the tier above), hop (jump a same-height gap; run for the 2.0 leap) or walk-off, so a fall is
    // simply climbed again. It fights foes within |dx|<7 and |dh|<1.2 on its own surface and jumps same-level strips from within 1.8.
    // PASS: Won within 240 s real time with 3 intel and the final boss down, both heroes past x124 within .6 of the extraction height.
    // Logs 'CHAPTER n QA COMPLETE: PASS|FAIL' and writes QA/chapter-n-route.txt (desktop), with the time of every waypoint for tuning.
    public sealed class SFChapterRouteCheck:MonoBehaviour
    {
        // Chapter a local WebGL run asked for (RunChapterQA, set before the reload). Awake dispatches on it and Start clears it.
        internal static int Want;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){Want=0;}

        enum Kind{Pass,Intel,Boss,Extract}
        // A waypoint: stand on the surface at height h near x. A Pass waypoint also counts once the lead is on it past x, toward the next one.
        sealed class Goal{public float x,h,reached=-1;public Kind kind;public string note;}
        static Goal G(float x,float h,string note,Kind kind=Kind.Pass)=>new Goal{x=x,h=h,note=note,kind=kind};

        // F13 Rio: (16,1.5) (24,1.5) (30.2,1.5 jump > R3, I1 32) (36,0) (41 jump Hz1) (66,1.5) (70,3) (75,4.5, I2 77) (walk off > R7 > street 86)
        // (88.2 jump Hz3) (95,1.5) (98,3) (102.5,4.5, I3 104) (106.3 run-jump > R14) (boss) (129,4.5).
        static Goal[] Rio()=>new[]{G(16,1.5f,"R1"),G(24,1.5f,"R2"),G(30.2f,1.5f,"R2 under R3"),G(32,3,"I1 on R3",Kind.Intel),G(36,0,"walk off R3 to the street"),
            G(41,0,"street past Hz1 (jump)"),G(66,1.5f,"R6"),G(70,3,"R7"),G(75,4.5f,"R8"),G(77,4.5f,"I2 on R8",Kind.Intel),G(80,3,"walk off R8 to R7"),
            G(86,0,"walk off R7 to the street"),G(91,0,"street past Hz3 (jump)"),G(95,1.5f,"R9"),G(98,3,"R10"),G(102.5f,4.5f,"R11"),G(104,4.5f,"I3 on R11",Kind.Intel),
            G(111,4.5f,"run-jump R11 > R14"),G(124,4.5f,"the Broker",Kind.Boss),G(129,4.5f,"Redeemer terrace extraction",Kind.Extract)};
        // F13 Al Anbar: (10,1.5) (14,3, I1 16) (street 19.8 jump Hz0) (26.5,1.5) (30.5 jump Hz1 > A4) (walk off > A2 45.5 jump > A6) (53.2 jump Hz2 > A7)
        // (Cantilever, I2 60.5) (walk off > A6) (67,3) (70.5,4.5) (hops > A12, I3 85) (walk off > A13 > street) (96,1.5) (100,3) (104,4.5) (110.2 jump Hz3)
        // (boss) (129,4.5).
        static Goal[] Anbar()=>new[]{G(10,1.5f,"A0 scaffold"),G(14,3,"A1"),G(16,3,"I1 on A1",Kind.Intel),G(19.8f,0,"walk off A1 to the street"),G(23.5f,0,"street past Hz0 (jump)"),
            G(26.5f,1.5f,"A2 floor one"),G(30.5f,1.5f,"A2 under A4"),G(33,3,"A4 girder (Hz1 below)"),G(45.5f,1.5f,"walk off A4, along A2"),G(48.5f,1.5f,"hop A2 > A6"),
            G(53.2f,1.5f,"A6 under A7"),G(60.5f,3,"I2 on A7 behind Cantilever",Kind.Intel),G(63.5f,1.5f,"walk off A7 to A6"),G(67,3,"A8"),G(70.5f,4.5f,"A9 crane"),
            G(85,4.5f,"I3 on A12 (hops A10, A11, A12)",Kind.Intel),G(88.5f,3,"walk off A12 to A13"),G(93,0,"walk off A13 to the street"),G(96,1.5f,"A14"),G(100,3,"A15"),
            G(104,4.5f,"A16 roof"),G(114,4.5f,"roof past Hz3 (jump)"),G(124,4.5f,"the Architect",Kind.Boss),G(129,4.5f,"roof extraction",Kind.Extract)};

        // Hero jog speed and gravity (9.81 x 2.7), as in SFChapterLayoutRules; Budget is the real-time limit in seconds.
        const float Gravity=9.81f*2.7f,Jog=3.744f,Budget=240;
        SFGame g;SFPlatform[] P;int Street;string deckTag="#";
        readonly List<string> log=new List<string>();
        // Steering while airborne: the x the lead heads for until it lands, and whether it runs there.
        SFActor airHero;float airTarget,lastJump=-9;bool airRun;
        int retries,resumes;

        void Check(bool ok,string name){string line=(ok?"PASS: ":"FAIL: ")+name;log.Add(line);Debug.Log(line);}
        void Info(string text){string line="INFO: "+text;log.Add(line);Debug.Log(line);}

        IEnumerator Start()
        {
            g=GetComponent<SFGame>();int want=Want;Want=0;
            yield return new WaitForSeconds(.2f);
            var ch=g.Chapter;int n=g.InChapter?g.CourseChapter:want>0?want:g.CourseChapter;
            var route=!g.InChapter?null:ch.key=="rio"?Rio():ch.key=="anbar"?Anbar():null;
            Debug.Log("Chapter route QA: chapter "+n+", loaded chapter "+g.CourseChapter+", course "+g.CourseMode+", chapters enabled "+SFGame.ChaptersEnabled);
            if(route==null){Check(false,"chapter "+n+": the route bot needs a built chapter with a waypoint list (loaded chapter "+g.CourseChapter+" '"+ch.key+"')");Finish(n);yield break;}
            P=g.Platforms;Street=P.Length;deckTag=ch.key=="rio"?"R":"A";
            g.StartGame();
            float start=Time.realtimeSinceStartup,deadline=start+Budget,held=-1,progress=start;int index=0;bool stallNoted=false;
            while(Time.realtimeSinceStartup<deadline&&g.State!=SFState.Won)
            {
                // Result and pause cards: RETRY FROM CHECKPOINT on Lost (at most 3), RESUME after a focus-loss pause, as a player would.
                if(g.State!=SFState.Playing)
                {
                    g.SmokeCommand=default;
                    if(held<0)held=Time.realtimeSinceStartup;
                    else if(Time.realtimeSinceStartup-held>.6f)
                    {
                        held=-1;
                        if(g.State==SFState.Lost&&retries<3){retries++;Info("both heroes down at "+Where(route,index,g.Player)+"; RETRY FROM CHECKPOINT x"+g.Checkpoint+" h"+g.CheckpointHeight);g.RetryCheckpoint();}
                        else if(g.State==SFState.Paused){resumes++;Info("paused (focus loss); RESUME");g.SetState(SFState.Playing);}
                        else break;
                    }
                    yield return null;continue;
                }
                held=-1;
                var hero=g.Player;
                if(hero.health<40&&g.Partner.health>60)g.Swap();
                hero=g.Player;
                if(g.Partner.health<65&&g.AssistCooldown<=0&&g.Bond>=35&&Mathf.Abs(g.Partner.X-hero.X)<=7)g.TryAssist();
                while(index<route.Length&&Reached(route,index,hero))
                {
                    route[index].reached=g.Elapsed;progress=Time.realtimeSinceStartup;stallNoted=false;
                    Info("waypoint "+index+" "+route[index].note+" at game "+g.Elapsed.ToString("0.0")+" s, real "+(progress-start).ToString("0.0")+" s ("+hero.identity+" x"+hero.X.ToString("0.0")+" h"+hero.Height.ToString("0.0")+")");
                    index++;
                }
                if(!stallNoted&&Time.realtimeSinceStartup-progress>30){stallNoted=true;Info("no waypoint for 30 s: "+Where(route,index,hero));}
                g.SmokeCommand=index<route.Length?Command(hero,route[index]):default;
                yield return null;
            }
            g.SmokeCommand=default;
            // Won ends the loop before the next pass can mark it: credit the waypoints the win completes (the extraction, and a boss down on the same frame).
            while(g.State==SFState.Won&&index<route.Length&&Reached(route,index,g.Player))
            {
                route[index].reached=g.Elapsed;
                Info("waypoint "+index+" "+route[index].note+" at game "+g.Elapsed.ToString("0.0")+" s, real "+(Time.realtimeSinceStartup-start).ToString("0.0")+" s (Won)");
                index++;
            }
            float took=Time.realtimeSinceStartup-start,ex=ch.extractionX-6,eh=ch.extractionHeight;var boss=g.FinalBoss;
            bool Home(SFActor a)=>a.X>ex&&Mathf.Abs(a.Height-eh)<.6f;
            string At(SFActor a)=>a.identity+" x"+a.X.ToString("0.00")+" h"+a.Height.ToString("0.00");
            Check(g.State==SFState.Won&&took<=Budget,"chapter "+n+" ("+ch.key+") Won with normal commands within "+Budget+" s real time (state "+g.State+", "+took.ToString("0.0")+" s)");
            Check(g.DataCount==3,"3 intel recovered ("+g.DataCount+")");
            Check(boss!=null&&!boss.Alive,"final boss "+(boss!=null?boss.identity:"(none)")+" down");
            Check(Home(g.Fernando)&&Home(g.Stefanie),"both heroes past x"+ex+" within .6 of h"+eh+" ("+At(g.Fernando)+", "+At(g.Stefanie)+")");
            Info("waypoints "+Mathf.Min(index,route.Length)+" of "+route.Length+(index<route.Length?", stopped at "+Where(route,index,g.Player):""));
            Info("game "+g.Elapsed.ToString("0.0")+" s; defeated "+g.KOs+"; assists "+g.Assists+"; checkpoint retries "+retries+"; resumes "+resumes+"; vertical catch-ups "+g.VerticalCatchUps+
                "; team health "+g.Fernando.health.ToString("0")+" / "+g.Stefanie.health.ToString("0")+"; lead "+g.Player.identity);
            Finish(n);
        }

        void Finish(int n)
        {
            bool pass=log.Count>0&&log.All(l=>!l.StartsWith("FAIL"));
            string result="CHAPTER "+n+" QA COMPLETE: "+(pass?"PASS":"FAIL");log.Add(result);Debug.Log(result);
            if(Application.platform==RuntimePlatform.WebGLPlayer)return;
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(path);
            File.WriteAllLines(Path.Combine(path,"chapter-"+n+"-route.txt"),log);Application.Quit(pass?0:2);
        }

        // Surfaces: the chapter's decks as authored, and the street (index P.Length) from the x.7 level clamp to x132.
        float Lo(int s)=>s==Street?.7f:P[s].x;
        float Hi(int s)=>s==Street?132:P[s].x+P[s].width;
        float H(int s)=>s==Street?0:P[s].height;
        string Name(int s)=>s==Street?"street":s<0?"no surface":deckTag+s+"("+P[s].x+"-"+(P[s].x+P[s].width)+"@"+P[s].height+")";
        // The surface an actor is on: the street below .3, else the deck within .25 of its height under it (.3 past either end); -1 otherwise.
        int NodeOf(SFActor a)
        {
            if(a.Height<.3f)return Street;int best=-1;
            for(int i=0;i<P.Length;i++)if(Mathf.Abs(a.Height-P[i].height)<.25f&&a.X>P[i].x-.3f&&a.X<P[i].x+P[i].width+.3f&&(best<0||Mathf.Abs(a.Height-P[i].height)<Mathf.Abs(a.Height-P[best].height)))best=i;
            return best;
        }
        int NodeAt(float x,float h){if(h<.3f)return Street;for(int i=0;i<P.Length;i++)if(Mathf.Abs(h-P[i].height)<.05f&&x>=P[i].x-.01f&&x<=P[i].x+P[i].width+.01f)return i;return -1;}
        SFPickup Intel(Goal q)=>g.Pickups.FirstOrDefault(p=>p.kind=="data"&&!p.taken&&Mathf.Abs(p.x-q.x)<1.5f&&Mathf.Abs(p.height-(q.h+.75f))<.3f);

        bool Reached(Goal[] route,int i,SFActor hero)
        {
            var q=route[i];
            if(q.kind==Kind.Intel)return Intel(q)==null;
            if(q.kind==Kind.Boss)return g.FinalBoss==null||!g.FinalBoss.Alive;
            if(q.kind==Kind.Extract)return g.State==SFState.Won;
            bool settled=hero.motor.IsGrounded&&hero.motor.Body.linearVelocity.y<=.1f;
            if(!settled||NodeOf(hero)!=NodeAt(q.x,q.h))return false;
            if(Mathf.Abs(hero.X-q.x)<.5f)return true;
            return i+1<route.Length&&(hero.X-q.x)*Mathf.Sign(route[i+1].x-q.x)>0;
        }

        string Where(Goal[] route,int index,SFActor hero)
        {
            var q=route[Mathf.Clamp(index,0,route.Length-1)];var mate=g.Partner;
            return "waypoint "+index+" ("+q.note+"): "+hero.identity+" x"+hero.X.ToString("0.00")+" h"+hero.Height.ToString("0.00")+" on "+Name(NodeOf(hero))+
                ", partner x"+mate.X.ToString("0.0")+" h"+mate.Height.ToString("0.0")+(mate.Alive?"":" (down)")+", intel "+g.DataCount+", state "+g.State;
        }

        // Partner ready to extract: alive and standing past x124.3 at the extraction height, within 5.5 of the lead. Until then the lead
        // does not interact at extraction, so an early Won cannot leave the partner short of the PASS line.
        bool Ready(SFActor hero)
        {
            var m=g.Partner;var ch=g.Chapter;
            return m.Alive&&m.motor.IsGrounded&&m.X>ch.extractionX-5.7f&&Mathf.Abs(m.Height-ch.extractionHeight)<.5f&&Mathf.Abs(m.X-hero.X)<5.5f&&Mathf.Abs(m.Height-hero.Height)<.9f;
        }

        SFCommand Command(SFActor hero,Goal q)
        {
            bool settled=hero.motor.IsGrounded&&hero.motor.Body.linearVelocity.y<=.1f;int node=settled?NodeOf(hero):-1;
            if(hero!=airHero){airHero=hero;airTarget=hero.X;airRun=false;}
            // Target: a downed partner first (revive), then the final boss while it stands, then the waypoint (or its intel).
            var mate=g.Partner;int goal;float gx;
            if(!mate.Alive){int m=NodeOf(mate);goal=m>=0?m:NodeAt(q.x,q.h);gx=mate.X;}
            else if(q.kind==Kind.Boss&&g.FinalBoss!=null&&g.FinalBoss.Alive){int b=NodeOf(g.FinalBoss);goal=b>=0?b:NodeAt(q.x,q.h);gx=g.FinalBoss.X;}
            else{goal=NodeAt(q.x,q.h);gx=q.x;var item=q.kind==Kind.Intel?Intel(q):null;if(item!=null)gx=item.x;}
            if(goal<0)goal=Street;
            var foe=node>=0?Foe(hero,node):null;
            var c=foe!=null?Fight(hero,node,foe):Steer(hero,node,goal,gx);
            if(!mate.Alive&&node==Street&&Mathf.Abs(mate.X-hero.X)<2.5f&&Mathf.Abs(mate.lane-hero.lane)>.3f)c.move.y=Mathf.Sign(mate.lane-hero.lane);
            c.interact=!g.ExtractionReach||Ready(hero)||!mate.Alive;
            return c;
        }

        // Nearest living foe within |dx|<7 and |dh|<1.2 on the lead's own surface (or airborne within 3). Foes a gap or a tier away are left alone.
        SFActor Foe(SFActor hero,int node)
        {
            SFActor best=null;
            foreach(var a in g.Actors)
            {
                if(a.Friendly||!a.Alive)continue;float dx=Mathf.Abs(a.X-hero.X);
                if(dx>=7||Mathf.Abs(a.Height-hero.Height)>=1.2f)continue;
                int s=NodeOf(a);if(s!=node&&!(s<0&&dx<3))continue;
                if(best==null||dx<Mathf.Abs(best.X-hero.X))best=a;
            }
            return best;
        }

        // SFRouteCheck's melee: close to 1.65, match depth, kick inside 2.2 when facing the foe. On a deck the lead stops .35 short of its ends.
        SFCommand Fight(SFActor hero,int node,SFActor foe)
        {
            float dx=foe.X-hero.X,d=Mathf.Abs(dx);bool behind=dx*hero.facing<0;
            var c=new SFCommand{move=new Vector2(d>1.65f||behind?Mathf.Sign(dx):0,Mathf.Abs(foe.lane-hero.lane)>.1f?Mathf.Sign(foe.lane-hero.lane):0),kick=d<2.2f&&!behind};
            if(node!=Street&&c.move.x!=0)
            {
                float next=hero.X+c.move.x*.5f;
                if(next<Lo(node)+.35f||next>Hi(node)-.35f)c.move.x=behind?Mathf.Sign(dx)*.12f:0;
            }
            airTarget=hero.X;airRun=false;
            HazardPass(hero,foe.X,ref c,true);
            return c;
        }

        // One planner move off a surface: 1 climb (jump from inside [lo,hi], under deck 'to'), 2 hop (jump the gap past 'edge' toward dir),
        // 3 drop (walk off 'edge' toward dir). 'land' is where the move should set the lead down on 'to'. Kind 0 is a walk on the same surface.
        struct Step{public int kind,to,dir;public float lo,hi,edge,land;}

        IEnumerable<Step> Moves(int a)
        {
            float h=H(a);
            for(int b=0;b<P.Length;b++)
            {
                if(b==a)continue;float dh=P[b].height-h;
                float lo=Mathf.Max(Lo(a)+(a==Street?.3f:.25f),P[b].x+.5f),hi=Mathf.Min(Hi(a)-(a==Street?.5f:.25f),P[b].x+P[b].width-.5f);
                if(dh>=.3f&&dh<=1.8f&&hi-lo>=.2f){yield return new Step{kind=1,to=b,lo=lo,hi=hi,land=lo};continue;}
                if(a==Street||dh<=-.3f||dh>=.6f)continue;
                float right=P[b].x-Hi(a),left=Lo(a)-(P[b].x+P[b].width);
                if(right>=0&&right<=2.05f)yield return new Step{kind=2,to=b,dir=1,edge=Hi(a),land=Hi(a)+right+1.2f};
                else if(left>=0&&left<=2.05f)yield return new Step{kind=2,to=b,dir=-1,edge=Lo(a),land=Lo(a)-left-1.2f};
            }
            if(a==Street)yield break;
            // Walk-offs at a jog leave the edge .14 past it and fall freely onto the highest surface under them (never off the x.7/x132 bounds).
            for(int dir=-1;dir<=1;dir+=2)
            {
                float e=dir<0?Lo(a):Hi(a);if(e<1||e>131.5f)continue;
                int to=Street;float land=e+dir*(.14f+Jog*Mathf.Sqrt(2*h/Gravity));
                for(int s=0;s<P.Length;s++)
                {
                    if(s==a||P[s].height>h-.3f||to!=Street&&P[s].height<=P[to].height)continue;
                    float at=e+dir*(.14f+Jog*Mathf.Sqrt(2*(h-P[s].height)/Gravity));
                    if(at>=P[s].x&&at<=P[s].x+P[s].width){to=s;land=at;}
                }
                yield return new Step{kind=3,to=to,dir=dir,edge=e,land=land};
            }
        }

        // First move of the cheapest route from surface a (at x) to surface goal: Dijkstra over the surfaces, costing the distance walked
        // plus 2 per climb, 3 per hop and 1 per walk-off. False when the goal cannot be reached from here.
        bool Plan(int a,float x,int goal,out Step first)
        {
            int n=P.Length+1;var cost=new float[n];var at=new float[n];var head=new Step[n];var done=new bool[n];
            for(int i=0;i<n;i++)cost[i]=float.MaxValue;
            cost[a]=0;at[a]=x;first=default;
            while(true)
            {
                int u=-1;for(int i=0;i<n;i++)if(!done[i]&&cost[i]<float.MaxValue&&(u<0||cost[i]<cost[u]))u=i;
                if(u<0)return false;
                if(u==goal){if(u!=a)first=head[u];return true;}
                done[u]=true;
                foreach(var s in Moves(u))
                {
                    float from=s.kind==1?Mathf.Clamp(at[u],s.lo,s.hi):s.edge,land=s.kind==1?from:s.land;
                    float c=cost[u]+Mathf.Abs(at[u]-from)+(s.kind==1?2:s.kind==2?3:1);
                    if(c<cost[s.to]){cost[s.to]=c;at[s.to]=land;head[s.to]=u==a?s:head[u];}
                }
            }
        }

        // Takeoff spot for a climb: the nearest point of [lo,hi], moved out of any same-level strip (.45 past its half-width) when there is room.
        float ClimbSpot(float h,Step s,float x)
        {
            float t=Mathf.Clamp(x,s.lo,s.hi);
            for(int i=0;i<g.Hazards.Length;i++)
            {
                float hz=g.Hazards[i],clear=g.HzW(i)+.45f;if(!g.HzLevel(h,i)||Mathf.Abs(t-hz)>=clear)continue;
                float near=x<hz?hz-clear:hz+clear,far=x<hz?hz+clear:hz-clear;
                t=near>=s.lo&&near<=s.hi?near:far>=s.lo&&far<=s.hi?far:t;
            }
            return t;
        }

        SFCommand Steer(SFActor hero,int node,int goal,float gx)
        {
            var c=new SFCommand();
            if(node<0)
            {
                // Airborne: hold the course set at takeoff. Standing on no known surface (not expected) just heads for the goal.
                float d=hero.motor.IsGrounded&&hero.motor.Body.linearVelocity.y<=.1f?gx-hero.X:airTarget-hero.X;
                c.move.x=Mathf.Abs(d)>.25f?Mathf.Sign(d):0;c.run=airRun;return c;
            }
            airRun=false;float target=gx,land=gx,approachTolerance=.18f;bool jump=false;
            Plan(node,hero.X,goal,out var s);
            if(s.kind==1)
            {
                // Climb: walk to the takeoff spot under the next tier, then jump toward the goal (clamped .6 inside that deck).
                var to=P[s.to];land=Mathf.Clamp(s.to==goal?gx:hero.X,to.x+.6f,to.x+to.width-.6f);
                // Aim inside the takeoff interval: stopping .18 short of its boundary
                // otherwise leaves the bot outside forever (Rio R6 > R7 at x68.35).
                float inset=Mathf.Min(.25f,(s.hi-s.lo)*.5f);
                approachTolerance=Mathf.Min(.18f,inset*.5f);
                float free=Mathf.Clamp(hero.X,s.lo+inset,s.hi-inset);target=ClimbSpot(H(node),s,free);
                jump=target==free?hero.X>=s.lo&&hero.X<=s.hi:Mathf.Abs(hero.X-target)<.3f;
            }
            else if(s.kind==2)
            {
                // Hop: take off within .45 of the edge at a jog, or .7 at a run for gaps over 1.55 (Rio's R11 > R14 leap), and fly at least 1 into the deck.
                var to=P[s.to];float gap=Mathf.Abs(s.land-s.edge)-1.2f,before=(s.edge-hero.X)*s.dir;bool run=gap>1.55f;
                land=Mathf.Clamp(s.to==goal?gx:s.land,to.x+.6f,to.x+to.width-.6f);land=s.dir>0?Mathf.Max(land,s.edge+gap+1):Mathf.Min(land,s.edge-gap-1);
                target=s.edge+s.dir*2;c.run=run;jump=before<=(run?.7f:.45f)&&before>-.15f;if(jump)airRun=run;
            }
            else if(s.kind==3){target=s.edge+s.dir*1.5f;land=s.land+s.dir*.3f;}
            if(jump)target=land;
            float dx=target-hero.X;c.move.x=Mathf.Abs(dx)>approachTolerance?Mathf.Sign(dx):0;
            airTarget=s.kind==3||jump?land:target;
            if(jump){if(Time.time-lastJump>.35f){c.jump=true;lastJump=Time.time;}}
            else HazardPass(hero,target,ref c,false);
            return c;
        }

        // Same-level strips on the way to target: run from 3.5 out and jump from within 1.85 when the target lies past the strip. While fighting,
        // a foe standing in (or short of) a strip is met at its near edge instead, and a lead inside a strip steps back out.
        void HazardPass(SFActor hero,float target,ref SFCommand c,bool fighting)
        {
            int dir=c.move.x>.5f?1:c.move.x<-.5f?-1:0;if(dir==0)return;
            bool grounded=hero.motor.IsGrounded&&hero.motor.Body.linearVelocity.y<=.1f;
            for(int i=0;i<g.Hazards.Length;i++)
            {
                if(!g.HzLevel(hero.Height,i))continue;
                float hz=g.Hazards[i],half=g.HzW(i),ahead=(hz-hero.X)*dir,beyond=(target-hz)*dir;
                if(ahead<-(half+.1f))continue;
                if(fighting&&beyond<half+.2f){if(Mathf.Abs(hz-hero.X)<half+.05f)c.move.x=-dir;else if(ahead-half<.5f)c.move.x=0;continue;}
                if(beyond<-(half+.3f))continue;
                if(ahead<3.5f)c.run=true;
                if(ahead<=1.85f&&grounded&&Time.time-lastJump>.35f)
                {
                    c.jump=true;c.kick=false;lastJump=Time.time;airRun=true;
                    airTarget=dir>0?Mathf.Max(target,hz+half+1):Mathf.Min(target,hz-half-1);
                }
            }
        }
    }
}
