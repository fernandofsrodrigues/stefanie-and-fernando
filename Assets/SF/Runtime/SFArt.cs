using System;
using UnityEngine;

namespace StefanieAndFernando
{
    // Authored frame order follows the sheet: top-left to bottom-right.
    public sealed class SFArt : ScriptableObject
    {
        public string source;
        public string sourceHash;
        public Sprite[] frames;
        public Sprite Frame(int index) => frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }

    [Serializable]
    public struct SFCommand
    {
        public Vector2 move;
        public bool jump, punch, kick, shoot, guard, dash, crouch, aim, grapple, ground;
        public bool reload, cycleWeapon, toggleArmed, interact, assist, swap, walk, run, toggleWalk, backup;
        public int weapon, aimFacing;
        public float zoom;
    }

    public static class SFMath
    {
        public static bool WithinStrike(Vector2 from, float elevation, int facing, Vector2 to,
            float targetElevation, float reach, float laneTolerance = .65f)
        {
            float dx = to.x - from.x;
            return dx * facing >= -.3f && Mathf.Abs(dx) <= reach &&
                Mathf.Abs(to.y - from.y) <= laneTolerance && Mathf.Abs(elevation - targetElevation) < 1.35f;
        }

    }

    [Serializable]
    public struct SFPlatform
    {
        public float x, width, height;
        public SFPlatform(float x, float width, float height) { this.x=x; this.width=width; this.height=height; }
    }
}
