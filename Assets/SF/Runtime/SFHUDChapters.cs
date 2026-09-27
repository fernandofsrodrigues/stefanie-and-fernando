using System.Linq;
using UnityEngine;

namespace StefanieAndFernando
{
    // C9 chapter select on the course menu: one pill per chapter, labelled with the chapter data's city (CURITIBA / RIO / AL ANBAR), and a
    // 'CHAPTER n OF 3' caption after them ('KEYS  1 / 2 / 3' below it for keyboard players). CourseMenu draws it after every existing
    // control, so FERNANDO, STEFANIE, START, RETURN TO CAMPAIGN, TOUCH, MUSIC & CREDITS and QUIT keep their draw order, rects and default
    // controller focus (FERNANDO, the first control drawn).
    // The row sits in the empty band above the title (y 20-76, focus bars 16-80; the title starts at y 94) and spans the menu column
    // (width/2-348 .. width/2+358) at every landscape width; the highest touch control starts at y 206, and none is drawn on the menu.
    // Pills are 56 tall (the menu's other buttons are 64-82) so they stay tappable on a phone; their centre line (y 48) sets the controller targets.
    // Pill centres sit at width/2 -265, -87 and +91 (relative to START's centre line), which keeps SFMenuNavigation's targets for the existing
    // controls: no pill is within 83 of START's centre, so Up from START still reaches FERNANDO, and none is right of STEFANIE's centre, so
    // Right from STEFANIE still reaches QUIT. FERNANDO and STEFANIE, which had nothing above them, now reach CURITIBA and AL ANBAR.
    // SFChapterSelectCheck proves this at 4:3, 16:9, 21:9 and the narrowest touch width, with and without QUIT. The offsets suit three chapters.
    public sealed partial class SFHUD
    {
        internal const float ChapterRowY=20,ChapterRowH=56,ChapterRowLeft=-348,ChapterPillW=166,ChapterPillGap=12,ChapterCaptionGap=16,ChapterCaptionW=168;
        internal static Rect ChapterPillRect(float width,int i,int count)=>new Rect(width/2+ChapterRowLeft+i*(ChapterPillW+ChapterPillGap),ChapterRowY,ChapterPillW,ChapterRowH);
        internal static Rect ChapterCaptionRect(float width,int count)=>new Rect(width/2+ChapterRowLeft+count*(ChapterPillW+ChapterPillGap)-ChapterPillGap+ChapterCaptionGap,ChapterRowY,ChapterCaptionW,ChapterRowH);

        // The frame of the last repaint that drew the pills, and the pill it highlighted (read by SFChapterSelectCheck).
        int chapterPillsFrame=-10;
        internal bool ChapterPillsDrawn=>Time.frameCount-chapterPillsFrame<=1;
        internal int ChapterPillHighlighted {get;private set;}=-1;

        // Hidden in the campaign, under -sfNoChapters, off the menu and in automated (SmokeMode) runs unless SFChapterSelectCheck forces it
        // (SFGame.ChapterSelectVisible), so nothing is drawn or registered with PadMenu there. A pill calls SelectChapter, which loads that
        // chapter fresh at its own menu through GoToChapter; the current chapter's pill does nothing.
        void ChapterSelect()
        {
            if(!game.ChapterSelectVisible)return;
            // The scene's own chapter, not the queued one: after a pill or RETURN TO CAMPAIGN the closing frame keeps this scene's pill and caption.
            var all=SFCourseChapters.All;int current=game.SceneChapter;
            for(int i=0;i<all.Length;i++)
                if(ChapterPill(all[i].city,ChapterPillRect(width,i,all.Length),i==current))game.SelectChapter(i);
            var caption=ChapterCaptionRect(width,all.Length);
            // Keyboard players also get the number keys (SFGame.ChapterSelectKeys) on a second line; touch and controller keep one line.
            string keys=!game.TouchControls&&!game.GamepadActive?"\nKEYS  "+string.Join(" / ",Enumerable.Range(1,all.Length)):"";
            Text("CHAPTER "+(current+1)+" OF "+all.Length+keys,caption.x,caption.y,caption.width,caption.height,15,muted,true,TextAnchor.MiddleLeft);
            if(Event.current.type==EventType.Repaint){chapterPillsFrame=Time.frameCount;ChapterPillHighlighted=current;}
        }

        // Button's click and controller contract (PadMenu registration, teal focus bars, activation consumed on Repaint) in a compact style.
        // The current chapter is highlighted: a gold frame, a deep teal fill, a gold underline and a gold label.
        bool ChapterPill(string label,Rect r,bool current)
        {
            bool pad=game.GamepadActive;
            if(Event.current.type==EventType.Repaint)PadMenu.Add(r);
            bool focused=pad&&PadMenu.IsFocused(r);
            bool hover=r.Contains(Event.current.mousePosition)||focused;
            if(current)Box(r.x-2,r.y-2,r.width+4,r.height+4,game.Gold);
            Box(r.x,r.y,r.width,r.height,current?new Color(.06f,.22f,.23f,.97f):hover?new Color(.15f,.25f,.27f,.95f):ink);
            Box(r.x,r.y+r.height-2,r.width,2,current?game.Gold:new Color(.32f,.42f,.43f));
            Text(label,r.x+8,r.y,r.width-16,r.height,16,current?game.Gold:hover?white:muted,true,TextAnchor.MiddleCenter);
            if(focused){Box(r.x-4,r.y-4,r.width+8,3,game.Teal);Box(r.x-4,r.y+r.height+1,r.width+8,3,game.Teal);}
            bool clicked=GUI.Button(r,GUIContent.none,GUIStyle.none);
            return clicked||(pad&&Event.current.type==EventType.Repaint&&PadMenu.Consume(r));
        }
    }
}
