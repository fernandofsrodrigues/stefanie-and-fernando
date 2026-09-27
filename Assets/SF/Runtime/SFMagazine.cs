using UnityEngine;

namespace StefanieAndFernando
{
    // Ammo moves from reserve only when a reload finishes. Canceling cannot duplicate rounds.
    public sealed class SFMagazine
    {
        public int Capacity { get; private set; }
        public int Loaded { get; private set; }
        public int Reserve { get; private set; }
        public int Total => Loaded + Reserve;
        public bool CanReload => Loaded < Capacity && Reserve > 0;
        public void Reset(int capacity,int reserve){Capacity=Mathf.Max(1,capacity);Loaded=Capacity;Reserve=Mathf.Max(0,reserve);}
        public void Clear(){Loaded=Reserve=0;}
        public void Supply(int rounds){Reserve+=Mathf.Max(0,rounds);}
        public bool Fire(){if(Loaded<=0)return false;Loaded--;return true;}
        public void Reload(){int transfer=Mathf.Min(Capacity-Loaded,Reserve);Loaded+=transfer;Reserve-=transfer;}
    }
}
