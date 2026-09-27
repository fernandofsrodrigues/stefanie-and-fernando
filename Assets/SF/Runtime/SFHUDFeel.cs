using System.Collections.Generic;
using UnityEngine;

namespace StefanieAndFernando
{
    public sealed partial class SFHUD
    {
        // Live-play overlays from SFFeel. Both stay zero in automated runs.
        // The boss knockout flash, drawn over the HUD (SFHUD.OnGUI calls this after DrawHUD).
        void FeelOverlay()
        {
            if(game.State!=SFState.Playing)return;
            if(game.KnockoutFlash>0)Box(0,0,width,900,new Color(1,.96f,.88f,.3f*game.KnockoutFlash));
        }

        // Band fill for the boss title card. Chapters 1-2 fight on raised tiers that sit behind this band, so there it stays light
        // and the decks, hazard strips and actors read through it. Curitiba's band covers open sky and keeps the solid card.
        internal float CardFill=>game.InChapter?.35f:.84f;

        // World-label rects from the last GUI pass while a card is up (DrawWorldLabels adds them through CardLabel). The labels stay on top of
        // the card; when one covers the role row above the name (the 4.5-tier hazard and pickup labels do on the top-tier boss cards), the
        // role line moves under the name for the rest of that card, if that row is clear. Nothing is recorded while no card is up.
        // The move is a .2 s cross-fade from the card time it was decided (roleDropAt), so a label arriving after the fade-in never makes
        // the visible role jump rows.
        readonly List<Rect> cardLabels=new List<Rect>();
        float cardLeft,roleDropAt;bool roleLow;
        void CardLabel(float x,float y,float w,float h){if(game.State==SFState.Playing&&game.BossCardTime>0)cardLabels.Add(new Rect(x,y,w,h));}
        internal static bool DropRole(List<Rect> labels,Rect top,Rect low)
        {
            bool hit=false;
            foreach(var r in labels){if(r.Overlaps(low))return false;hit|=r.Overlaps(top);}
            return hit;
        }

        // The arcade-style boss title card. SFHUD.OnGUI draws it before DrawWorldLabels and DrawHUD, so hazard labels, enemy warnings,
        // health bars and pickup labels stay on top of it. The band sits between the combat label (y 276) and the speech box (y 722);
        // the name and role slide in from opposite sides (a fade only when "Camera impact motion" is OFF).
        void FeelCard()
        {
            if(game.State!=SFState.Playing)return;
            float left=game.BossCardTime;if(left<=0)return;
            // A new card (its clock went up) starts with the role above the name and drops the last card's label rects.
            if(left>cardLeft){roleLow=false;cardLabels.Clear();}
            cardLeft=left;
            float shown=SFGame.BossCardLength-left,ease=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(shown,left)/.3f)),slide=game.ShakeEnabled?(1-ease)*160:0;
            const float y=318,h=124;
            float rw=Style(16,game.Teal,true,TextAnchor.MiddleCenter).CalcSize(new GUIContent(game.BossCardRole)).x+12,rx=width/2+slide-rw/2;
            if(!roleLow&&DropRole(cardLabels,new Rect(rx,y+14,rw,26),new Rect(rx,y+96,rw,24))){roleLow=true;roleDropAt=shown;}
            cardLabels.Clear();
            Box(0,y,width,h,new Color(.012f,.025f,.04f,CardFill*ease));
            Box(0,y,width,3,new Color(game.Gold.r,game.Gold.g,game.Gold.b,ease));Box(0,y+h-3,width,3,new Color(game.Gold.r,game.Gold.g,game.Gold.b,ease));
            // Fade the text through GUI.color so the style cache keys stay constant. drop runs 0 to 1 over the role's cross-fade to the lower row.
            float drop=roleLow?Mathf.Clamp01((shown-roleDropAt)/.2f):0;
            if(drop<1){GUI.color=new Color(1,1,1,ease*(1-drop));Text(game.BossCardRole,width/2-600+slide,y+14,1200,26,16,game.Teal,true,TextAnchor.MiddleCenter);}
            if(drop>0){GUI.color=new Color(1,1,1,ease*drop);Text(game.BossCardRole,width/2-600+slide,y+96,1200,24,16,game.Teal,true,TextAnchor.MiddleCenter);}
            GUI.color=new Color(1,1,1,ease);
            Text(game.BossCardName,width/2-600-slide,y+40,1200,70,56,game.Gold,true,TextAnchor.MiddleCenter);
            GUI.color=Color.white;
        }
    }
}
