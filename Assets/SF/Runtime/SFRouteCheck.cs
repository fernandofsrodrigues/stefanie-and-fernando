using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // Headless-friendly playthrough: normal commands and public actions only.
    // No teleporting, health edits, forced collectibles or forced enemy defeats.
    public sealed class SFRouteCheck : MonoBehaviour
    {
        IEnumerator Start()
        {
            var game=GetComponent<SFGame>();
            yield return new WaitForSeconds(.2f);game.StartGame();
            float deadline=Time.realtimeSinceStartup+160;
            while(game.State==SFState.Playing&&Time.realtimeSinceStartup<deadline)
            {
                var player=game.Player;
                if(player.health<40&&game.Partner.health>60)game.Swap();
                player=game.Player;
                if(game.Partner.health<65&&game.AssistCooldown<=0&&game.Bond>=35)game.TryAssist();
                var signal=game.Pickups.FirstOrDefault(p=>p.kind=="data"&&!p.taken);
                var enemy=game.Actors.Where(a=>!a.hero&&a.Alive&&Mathf.Abs(a.X-player.X)<7)
                    .OrderBy(a=>Mathf.Abs(a.X-player.X)).FirstOrDefault();
                var command=new SFCommand();
                command.interact=true;
                float target=signal!=null?signal.x:130;
                if(enemy!=null)
                {
                    float dx=enemy.X-player.X,distance=Mathf.Abs(dx);
                    command.move=new Vector2(distance>1.65f||dx*player.facing<0?Mathf.Sign(dx):0,
                        Mathf.Abs(enemy.lane-player.lane)>.1f?Mathf.Sign(enemy.lane-player.lane):0);
                    command.kick=distance<2.2f;
                    command.shoot=distance>2.5f&&distance<7&&game.Ammo>0;
                }
                else
                {
                    float dx=target-player.X;
                    command.move.x=Mathf.Abs(dx)>.18f?Mathf.Sign(dx):0;
                    command.jump=signal!=null&&Mathf.Abs(dx)<3&&player.motor.IsGrounded;
                }
                if(game.Hazards.Any(x=>Mathf.Abs(x-player.X)<1.8f)&&player.motor.IsGrounded)command.jump=true;
                game.SmokeCommand=command;
                yield return null;
            }
            game.SmokeCommand=default;
            string result=(game.State==SFState.Won?"PASS":"FAIL")+": normal-command chapter completion\n"+
                "State: "+game.State+"; Signals: "+game.DataCount+"; Defeated: "+game.KOs+"; Assists: "+game.Assists+
                "; Game seconds: "+game.Elapsed.ToString("0.0")+"; Lead x: "+game.Player.X.ToString("0.0")+
                "; Team health: "+game.Fernando.health.ToString("0.0")+" / "+game.Stefanie.health.ToString("0.0");
            Debug.Log(result);Debug.Log("COURSE QA COMPLETE: "+(game.State==SFState.Won?"PASS":"FAIL"));
            if(Application.platform!=RuntimePlatform.WebGLPlayer)
            {
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA"));Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path,"route-check.txt"),result);Application.Quit(game.State==SFState.Won?0:2);
            }
        }
    }
}
