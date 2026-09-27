using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    // A previous rendered frame owns navigation; activation is consumed once on Repaint.
    public sealed class SFMenuNavigation
    {
        readonly List<Rect> controls=new List<Rect>(),building=new List<Rect>();
        string context="";Rect focused,pending;bool hasFocus,hasPending;
        public Rect Focus=>focused;
        public int Count=>controls.Count;
        public void Begin(string key)
        {
            if(context!=key){context=key;controls.Clear();hasFocus=false;hasPending=false;}
            building.Clear();
        }
        public void Add(Rect rect){if(!building.Contains(rect))building.Add(rect);}
        public void End()
        {
            controls.Clear();controls.AddRange(building);
            if(!hasFocus||!controls.Contains(focused)){hasFocus=controls.Count>0;if(hasFocus)focused=controls[0];hasPending=false;}
        }
        public bool IsFocused(Rect rect)=>hasFocus&&focused==rect;
        public void Activate(){if(hasFocus){pending=focused;hasPending=true;}}
        public bool Consume(Rect rect){if(!hasPending||rect!=pending)return false;hasPending=false;return true;}
        public void Cancel(){hasPending=false;}
        public void Move(Vector2 direction)
        {
            if(!hasFocus||direction.sqrMagnitude<.01f)return;
            // Strongly favor the same row/column; no wrap can jump into Quit accidentally.
            direction=direction.normalized;float best=float.MaxValue;Rect next=focused;
            foreach(var rect in controls)
            {
                Vector2 delta=rect.center-focused.center;float forward=Vector2.Dot(delta,direction);
                if(forward<1)continue;
                float cross=Mathf.Abs(delta.x*direction.y-delta.y*direction.x);
                float cost=forward+cross*4;if(cost<best){best=cost;next=rect;}
            }
            focused=next;hasPending=false;
        }
    }
}
