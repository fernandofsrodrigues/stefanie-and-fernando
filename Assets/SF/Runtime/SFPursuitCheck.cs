using System.Collections;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFDriveCheck
    {
        void CheckPursuitMotion()
        {
            var m=new SFDriveModel(false);m.SetRole(SFDriveRole.Gunner);m.Board();Tick(m,40,default);
            var e=new SFConvoyEnemy(0,0,0){AttackClock=100};m.Enemies.Add(e);
            float startX=e.X;m.Tick(.1f,default,false);
            Check(e.Active&&e.X>startX&&e.GroundSpeed>m.Speed,"pursuer catching up has road speed above the pickup");
            Check(e.WheelAngle>0&&e.WheelAngle<360,"pursuit wheels rotate with bounded accumulated angle");
            Tick(m,80,default);float angle=e.WheelAngle,x=e.X,age=e.Age,speed=e.GroundSpeed;
            m.Tick(.25f,new SFCommand{swap=true,shoot=true},true);
            Check(e.WheelAngle==angle&&e.X==x&&e.Age==age&&e.GroundSpeed==speed,"pause freezes pursuit translation wheel rotation and age");
            e.X=SFDriveModel.PlayerX-10;m.Tick(.02f,default,false);
            Check(Mathf.Abs(e.GroundSpeed-m.Speed)<.01f,"matched pursuit spacing still rolls at road speed");
            e.Health=0;e.Resolved=true;e.Warning=true;e.MuzzleTime=.09f;int shots=e.Shots;speed=e.GroundSpeed;x=e.X;
            m.Tick(.2f,default,false);
            Check(e.GroundSpeed<speed&&e.GroundSpeed>0&&e.X<x,"disabled vehicle coasts while losing ground");
            Check(!e.Warning&&e.MuzzleTime==0&&e.Shots==shots,"disabled vehicle clears attack warnings and cannot fire");
            Tick(m,30,default);angle=e.WheelAngle;speed=e.GroundSpeed;x=e.X;m.Tick(.02f,default,false);
            Check(speed==0&&e.WheelAngle==angle&&e.X<x,"stopped disabled wheels remain still as the camera passes");
            Tick(m,100,default);Check(!e.Active,"retired pursuit render ownership ends after leaving the screen");
            var parked=new SFDriveModel(false);parked.Board();Tick(parked,40,default);foreach(var mag in parked.Magazines)mag.Clear();
            var stationary=new SFConvoyEnemy(0,0,0){X=-2,AttackClock=100};parked.Enemies.Add(stationary);Tick(parked,20,default);
            Check(stationary.GroundSpeed==0&&stationary.WheelAngle==0,"parked matched vehicles do not spin their wheels");
            stationary.AttackClock=.03f;Tick(parked,1,default);
            Check(stationary.Shots==1&&stationary.MuzzleTime>0&&parked.Shots[0].SourceEnemy==0,"hostile shot records its source vehicle and brief muzzle time");
            Tick(parked,4,default);Check(stationary.MuzzleTime==0,"hostile muzzle expires on the road clock");
            for(int i=0;i<20&&parked.Phase==SFDrivePhase.Driving;i++){stationary.AttackClock=.01f;Tick(parked,1,default);}
            angle=stationary.WheelAngle;x=stationary.X;parked.Tick(.2f,default,false);
            Check(parked.Phase==SFDrivePhase.Recovery&&stationary.WheelAngle==angle&&stationary.X==x,"recovery stops pursuit simulation until retry");
            Check(parked.Retry()&&stationary.WheelAngle==0&&stationary.GroundSpeed==0&&stationary.MuzzleTime==0&&stationary.Shots==0,"retry clears pursuit motion and shot presentation");
            var a=new SFConvoyEnemy(0,0,0);var b=new SFConvoyEnemy(1,0,0);a.Roll(.25f);b.Roll(.25f);
            Check(Mathf.Abs(Mathf.DeltaAngle(0,a.WheelAngle))>Mathf.Abs(Mathf.DeltaAngle(0,b.WheelAngle)),"larger van tire turns less for the same road distance");
        }
        IEnumerator CheckPursuitVisuals()
        {
            Check(g.StartDrive(),"pursuit presentation opens a separate road session");
            Check(g.PursuitVisualCount==6&&g.PursuitMeshCount==12,"six pursuit vehicles allocate exactly twelve rim meshes once");
            g.Drive.SetRole(SFDriveRole.Gunner);g.Drive.Board();for(int i=0;i<40;i++)g.TickDrive(.05f,default);
            for(int type=0;type<2;type++)
            {
                var e=g.Drive.Enemies[type];e.Active=true;e.X=-1;e.Age=3;e.Warning=true;e.AttackClock=.3f;e.LockedLane=g.Drive.Lane;
                g.RenderDrive();var rear=g.PursuitWheelPosition(type,0);var front=g.PursuitWheelPosition(type,1);var muzzle=g.PursuitGunMuzzle(type);
                Check(rear.x<e.X&&front.x>e.X&&Mathf.Abs(rear.y-front.y)<.001f,"pursuit type "+type+" has distinct registered front and rear rims");
                Check(rear.y>SFGame.DriveRoadY(e.Lane)+.25f&&rear.y<SFGame.DriveRoadY(e.Lane)+.4f,"pursuit type "+type+" rims meet the imported tire centers");
                Check(g.PursuitWarningVisible(type)&&!g.PursuitMuzzleVisible(type),"pursuit type "+type+" countdown identifies attacker before a shot");
                Check(muzzle.x>e.X&&muzzle.x<e.X+1.25f&&muzzle.y>SFGame.DriveRoadY(e.Lane)+1,"pursuit type "+type+" muzzle is registered to the visible passenger weapon");
                yield return View("pursuit-"+type+"-warning");
                e.Warning=false;e.MuzzleTime=.09f;e.WheelAngle=110;g.RenderDrive();
                Check(!g.PursuitWarningVisible(type)&&g.PursuitMuzzleVisible(type),"pursuit type "+type+" shot replaces countdown with muzzle feedback");
                Check(Mathf.Abs(Mathf.DeltaAngle(g.PursuitWheelAngle(type),110))<.01f,"pursuit type "+type+" wheel renderer follows independent model angle");
                yield return View("pursuit-"+type+"-shot");
                e.Health=20;e.MuzzleTime=0;g.RenderDrive();
                Check(g.PursuitSmokeVisible(type)==3&&g.PursuitBodyColor(type).r<1,"pursuit type "+type+" low vehicle condition shows bounded smoke and wear");
                yield return View("pursuit-"+type+"-damaged");
                g.SetState(SFState.Paused);float angle=e.WheelAngle;rear=g.PursuitWheelPosition(type,0);g.TickDrive(.2f,new SFCommand{shoot=true});
                Check(e.WheelAngle==angle&&g.PursuitWheelPosition(type,0)==rear,"pursuit type "+type+" pause leaves rendered wheel registration stable");g.SetState(SFState.Playing);
                e.Health=100;e.Resolved=true;g.RenderDrive();
                Check(g.PursuitSmokeVisible(type)==0&&!g.PursuitWarningVisible(type)&&!g.PursuitMuzzleVisible(type),"pursuit type "+type+" healthy escape is not shown as a damaged firing vehicle");
                e.Active=false;g.RenderDrive();Check(g.PursuitSmokeVisible(type)==0&&!g.PursuitWarningVisible(type),"pursuit type "+type+" inactive vehicle disables transient effects");
            }
            g.EndDrive();yield return null;
            Check(g.PursuitVisualCount==0&&GameObject.Find("INTERCEPTOR rear rim")==null,"road exit releases pursuit meshes and presentation ownership");
        }
    }
}
