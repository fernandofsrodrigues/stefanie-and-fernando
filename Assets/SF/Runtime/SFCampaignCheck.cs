#if !UNITY_WEBGL
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFGame
    {
        // A deterministic controller drives normal movement and attacks. No damage or health overrides.
        internal SFCommand CampaignCheckCommand()
        {
            var p=Player;var c=new SFCommand{run=true,interact=true};
            if(p.GripTarget!=null){c.punch=p.ClinchHits==0;c.kick=p.ClinchHits>0;return c;}
            var foe=Actors.Where(a=>!a.Friendly&&a.Alive).OrderBy(a=>Vector2.Distance(a.GroundPosition,p.GroundPosition)).FirstOrDefault();
            if(foe==null){c.move=Navigate(p,new Vector2(128,0)).move;return c;}
            var med=Pickups.Where(a=>!a.taken&&(a.kind=="food"||a.kind=="medical")&&Mathf.Abs(a.x-p.X)<5).OrderBy(a=>Mathf.Abs(a.x-p.X)).FirstOrDefault();
            if(p.health<p.maxHealth*.6f&&med!=null&&Mathf.Abs(foe.X-p.X)>2.5f){c.move=Navigate(p,new Vector2(med.x,med.lane)).move;return c;}
            float dx=foe.X-p.X;int face=dx>=0?1:-1;
            bool firearm=Ammo>0&&foe.ranged;
            c.toggleArmed=p.Armed!=firearm;if(firearm&&!p.Rifle)c.weapon=2;
            float distance=firearm?4.5f:1.05f;
            var goal=new Vector2(foe.X-face*distance,foe.lane);
            if(firearm&&ShotCover(p,foe)!=null)
            {
                // Move around solid cover before firing; never shoot through the test geometry.
                var cover=ShotCover(p,foe);goal=new Vector2(foe.X-face*2,Mathf.Clamp(cover.footprint.yMin-.7f,-1.1f,1.1f));
            }
            c.move=Navigate(p,goal).move;
            if(!p.Busy)p.facing=face;
            bool lined=Mathf.Abs(foe.lane-p.lane)<.5f;
            if(firearm){c.aim=true;c.aimFacing=face;c.shoot=p.Armed&&lined&&Mathf.Abs(dx)<10&&ShotCover(p,foe)==null;if(c.shoot){c.move=Vector2.zero;if(p.Rifle&&p.PrecisionRifle)c.shoot=!p.Busy&&p.cooldown<=0;}}
            else if(lined&&Mathf.Abs(dx)<1.65f&&ClearAttack(p,foe,false))
            {c.ground=foe.action==SFAction.Knocked;c.grapple=!foe.boss&&!foe.Restrained&&foe.health>34;c.punch=!c.grapple&&!c.ground;}
            return c;
        }
    }
    public sealed class SFCampaignCheck:MonoBehaviour
    {
        IEnumerator Start()
        {
            var g=GetComponent<SFGame>();var report=new List<string>();yield return null;
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QA/campaign-checks.txt"));
            for(int route=0;route<6;route++)
            {
                g.ReturnToLoadout(route);g.Selected=route%2;g.Loadouts[0].outfit=g.Loadouts[1].outfit=route%2;
                g.Loadouts[0].gear=g.Loadouts[1].gear=0;g.StartGame();float start=Time.realtimeSinceStartup,minHealth=g.Player.health,nextLog=30;
                while(g.State==SFState.Playing&&Time.realtimeSinceStartup-start<180)
                {
                    g.SmokeCommand=g.CampaignCheckCommand();minHealth=Mathf.Min(minHealth,g.Player.health);
                    if(g.Elapsed>=nextLog){Debug.Log("CAMPAIGN "+route+" t="+g.Elapsed.ToString("F0")+" x="+g.Player.X.ToString("F1")+" lane="+g.Player.lane.ToString("F2")+" remaining="+g.HostilesRemaining);nextLog+=30;}
                    yield return null;
                }
                bool passed=g.State==SFState.Won&&g.HostilesRemaining==0;
                string result=(passed?"PASS: ":"FAIL: ")+g.RouteName+" natural controller run; state="+g.State+" remaining="+g.HostilesRemaining+" time="+g.Elapsed.ToString("F1")+" leadX="+g.Player.X.ToString("F1")+" partnerX="+g.Partner.X.ToString("F1")+" minLeadHealth="+minHealth.ToString("F1")+" ammo="+g.Ammo;
                report.Add(result);Debug.Log(result);File.WriteAllLines(path,report);g.Capture("v07-campaign-"+route);
                if(!passed)break;
            }
            Application.Quit(report.Count==6&&report.All(r=>r.StartsWith("PASS"))?0:2);
        }
    }
}
#endif
