using UnityEngine;

namespace StefanieAndFernando
{
    // Scene markers for contextual pickups, extraction and hazards. SFGame evaluates
    // continuous hazards on its simulation clock, independently of Rigidbody2D sleep.
    public sealed class SFInteraction : MonoBehaviour
    {
        public SFGame game;
        [System.NonSerialized] public SFPickup pickup;
        public bool hazard,goal;
    }
}
