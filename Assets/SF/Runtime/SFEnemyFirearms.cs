using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFActor
    {
        // These NPC profiles match their existing shotgun/rifle art. Hero kits remain independent.
        public readonly SFMagazine EnemyAmmo=new SFMagazine();
        // Keyed on LayoutId (== identity for existing ids): a smuggler_shotgun carries the keel shotgun kit.
        public bool HasEnemyFirearm=>!Friendly&&(LayoutId=="keel"||LayoutId=="ratchet");
        public float EnemyReloadDuration=>LayoutId=="keel"?4.1f:2.65f;
        public bool EnemyOutOfAmmo=>HasEnemyFirearm&&EnemyAmmo.Total==0;
        public float ReloadProgress=>Reloading?Mathf.Clamp01(actionClock/Mathf.Max(.01f,attackDuration)):0;

        void ResetEnemyWeapon()
        {
            if(HasEnemyFirearm)EnemyAmmo.Reset(LayoutId=="keel"?6:30,LayoutId=="keel"?12:60);
        }
        public bool TryEnemyReload()
        {
            if(!HasEnemyFirearm||!Alive||!Armed||Busy||Restrained||action==SFAction.Hurt||GripTarget!=null||HeldBy!=null||cooldown>0||!motor.IsGrounded||!EnemyAmmo.CanReload)return false;
            action=SFAction.Reload;actionClock=0;attackDuration=EnemyReloadDuration;cooldown=attackDuration;
            Crouching=Aiming=false;motor.SetCommand(0,false);moveAmount=0;
            return true;
        }
        bool SpendShot()
        {
            if(hero)return (ShotRifle?RifleAmmo:SidearmAmmo).Fire();
            return !HasEnemyFirearm||EnemyAmmo.Fire();
        }
    }
}
