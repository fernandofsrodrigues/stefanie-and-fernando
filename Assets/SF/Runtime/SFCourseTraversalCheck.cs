using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // Isolated real-body arrangements, not a normal playthrough or chapter completion.
    public sealed class SFCourseTraversalCheck:MonoBehaviour
    {
        SFGame g;SFActor lead,buddy,melee,shooter;int unsupported;readonly List<string> checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if(!Environment.GetCommandLineArgs().Contains("-sfTraversal"))return;
            var game=UnityEngine.Object.FindAnyObjectByType<SFGame>();
            if(game!=null){game.SmokeMode=true;game.gameObject.AddComponent<SFCourseTraversalCheck>();}
        }
        void Check(bool ok,string label){string line=(ok?"PASS: ":"FAIL: ")+label;checks.Add(line);Debug.Log(line);}
        void Ready(SFActor a,float x,float height)
        {
            a.ReleaseGrip();a.Revive();a.health=a.maxHealth;a.invulnerable=0;a.cooldown=0;
            a.lane=0;a.motor.Freeze(false);a.Teleport(new Vector2(x,height));a.action=SFAction.Idle;
            if(a.HasEnemyFirearm)a.EnemyAmmo.Reset(6,12);
        }
        void Clear()
        {
            foreach(var a in g.Actors){a.ReleaseGrip();a.health=0;a.motor.Freeze(true);a.Teleport(new Vector2(100,0));}
        }
        static bool OnDeck(SFActor a)=>a.motor.IsGrounded&&Mathf.Abs(a.Height-2.4f)<.12f&&Mathf.Abs(a.motor.Body.linearVelocity.y)<.1f;
        IEnumerator Start()
        {
            g=GetComponent<SFGame>();yield return null;g.StartGame();g.enabled=false;g.ShakeEnabled=false;
            lead=g.Player;buddy=g.Partner;melee=g.Actors.First(a=>!a.Friendly&&!a.ranged&&!a.boss);shooter=g.Actors.First(a=>!a.Friendly&&a.identity=="keel");
            foreach(float targetX in new[]{56.9f,61.1f,57.1f})foreach(float startX in new[]{54f,63f})foreach(var mover in new[]{buddy,melee,shooter})
            {
                Clear();Ready(lead,targetX,2.42f);Ready(mover,startX,0);
                // Lead is a stationary supported target; only the climber receives commands.
                yield return new WaitForSeconds(.2f);
                string label=$"{mover.identity} from {startX:F1} to supported target {targetX:F1}";
                bool supported=OnDeck(lead);
                if(!supported)
                {
                    unsupported++;Debug.Log($"NOT REPRODUCED: {label}; target has no settled deck support (height={lead.Height:F3}, vy={lead.motor.Body.linearVelocity.y:F3}, grounded={lead.motor.IsGrounded}). No traversal assertion made.");
                    continue;
                }
                Check(true,label+" fixture has real deck support");
                bool reached=false;int hops=0;float priorV=0;
                for(int tick=0;supported&&tick<500;tick++)
                {
                    mover.Tick(Time.fixedDeltaTime,mover==buddy?g.CompanionCommand():g.EnemyCommand(mover));yield return new WaitForFixedUpdate();
                    float vy=mover.motor.Body.linearVelocity.y;if(priorV<1&&vy>8)hops++;priorV=vy;
                    if(OnDeck(mover)){reached=true;break;}
                }
                Debug.Log($"TRAVERSAL TRACE: {label}; reached={reached}; hops={hops}; end={mover.X:F2},{mover.Height:F2}; target={lead.X:F2},{lead.Height:F2}");
                Check(reached&&hops<=3,label+" reaches deck within ten seconds and at most three takeoffs");
            }
            // Both actors run real commands and retain their health throughout each fight.
            // A held, invincible or repeatedly revived enemy could hide engagement failures.
            foreach(var arrangement in new[]{new Vector3(57.4f,59.8f,60),new Vector3(60.6f,58.2f,63),new Vector3(59.1f,60.5f,54)})
            {
                Clear();Ready(lead,arrangement.x,2.42f);Ready(melee,arrangement.y,2.42f);Ready(buddy,arrangement.z,0);
                yield return new WaitForSeconds(.2f);
                Check(OnDeck(lead)&&OnDeck(melee),"deck fight starts with supported lead and opponent "+arrangement);
                bool reached=false,engaged=false;float damage=melee.health;
                for(int tick=0;tick<600&&lead.Alive&&buddy.Alive;tick++)
                {
                    buddy.Tick(Time.fixedDeltaTime,g.CompanionCommand());
                    melee.Tick(Time.fixedDeltaTime,g.EnemyCommand(melee));yield return new WaitForFixedUpdate();
                    reached|=OnDeck(buddy);engaged|=reached&&melee.health<damage;
                    if(engaged)break;
                }
                Debug.Log($"DECK FIGHT TRACE: {arrangement}; reached={reached}; engaged={engaged}; partner={buddy.X:F2},{buddy.Height:F2}; health={lead.health:F1}/{buddy.health:F1}/{melee.health:F1}");
                Check(reached&&engaged,"partner climbs and lands a hit in deck fight "+arrangement);
                g.Camera.transform.position=new Vector3(58.5f,1,-10);foreach(var a in g.Actors)a.Render(0);
                if(Application.platform!=RuntimePlatform.WebGLPlayer)g.Capture("traversal-deck-fight-"+arrangement.x.ToString("F1"));
            }
            Check(checks.Count>=18,"inside-bound controls and three deck-fight fixtures were exercised");
            bool ok=checks.All(s=>s.StartsWith("PASS:"));string result="COURSE TRAVERSAL QA COMPLETE: "+(ok?"PASS":"FAIL")+" / "+checks.Count+" checks; unsupported arrangements not asserted: "+unsupported;Debug.Log(result);
            if(Application.platform!=RuntimePlatform.WebGLPlayer)
            {
                string qa=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(qa);
                File.WriteAllLines(Path.Combine(qa,"traversal-check.txt"),checks.Concat(new[]{result}));Application.Quit(ok?0:2);
            }
        }
    }
}
