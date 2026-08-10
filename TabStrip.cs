// The row of tabs across the top of the window.
//
// This is drawn by hand rather than being a TabControl. A TabControl under
// visual styles paints its own strip from the system theme and ignores
// BackColor, so it would sit as a grey slab inside a dark window; it also has no
// close button. Since both of those had to be drawn anyway, the whole strip is.
//
// Layout, left to right:  [<] [ tab ][ tab ][ tab ] [>] [+]
// The two arrows appear only when the tabs do not all fit.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public delegate void TabIndexHandler(int index);

public class TabStrip : Control
{
    public event TabIndexHandler TabSelected;
    public event TabIndexHandler TabCloseRequested;
    public event EventHandler NewTabRequested;

    private const int MinTabWidth = 96;
    private const int MaxTabWidth = 240;
    private const int CloseSize = 15;
    private const int NewButtonWidth = 30;
    private const int ArrowWidth = 24;

    // What the mouse is over, so hover states and clicks agree on one answer.
    private enum Part { None, Tab, Close, ScrollLeft, ScrollRight, NewTab }

    private readonly List<string> labels = new List<string>();
    private int selected = -1;
    private int hoverIndex = -1;
    private Part hoverPart = Part.None;
    private int scroll = 0;             // pixels the tabs are scrolled by

    public TabStrip()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        Height = 30;
        Dock = DockStyle.Top;
    }

    public int Count { get { return labels.Count; } }

    public int SelectedIndex { get { return selected; } }

    /// <summary>Replace the whole strip. Cheap enough to call on every change.</summary>
    public void SetTabs(IList<string> newLabels, int selectedIndex)
    {
        labels.Clear();
        labels.AddRange(newLabels);
        selected = selectedIndex;
        ClampScroll();
        EnsureVisible(selectedIndex);
        Invalidate();
    }

    /// <summary>Height that suits the current zoom, so tabs grow with the text.</summary>
    public int PreferredHeight
    {
        get
        {
            using (Font font = TabFont())
                return Math.Max(26, font.Height + 12);
        }
    }

    private Font TabFont()
    {
        return new Font("Segoe UI", Theme.ScaleFont(8.5f));
    }

    // ---- geometry ----

    // Everything left of the new-tab button belongs to the tabs and their arrows.
    private int StripWidth { get { return Math.Max(0, Width - NewButtonWidth); } }

    private int TabWidth()
    {
        if (labels.Count == 0) return MinTabWidth;
        int even = StripWidth / labels.Count;
        return Math.Max(MinTabWidth, Math.Min(MaxTabWidth, even));
    }

    private int TotalTabsWidth() { return labels.Count * TabWidth(); }

    /// <summary>True when the tabs cannot all be shown at once.</summary>
    public bool Overflowing { get { return TotalTabsWidth() > StripWidth; } }

    // With arrows showing, the tabs live in the gap between them.
    private int TabsLeft { get { return Overflowing ? ArrowWidth : 0; } }

    private int ViewportWidth
    {
        get { return Math.Max(0, StripWidth - (Overflowing ? ArrowWidth * 2 : 0)); }
    }

    private int MaxScroll() { return Math.Max(0, TotalTabsWidth() - ViewportWidth); }

    private void ClampScroll()
    {
        scroll = Math.Max(0, Math.Min(MaxScroll(), scroll));
    }

    public bool CanScrollLeft { get { return Overflowing && scroll > 0; } }

    public bool CanScrollRight { get { return Overflowing && scroll < MaxScroll(); } }

    private void EnsureVisible(int index)
    {
        if (index < 0 || index >= labels.Count) return;
        int width = TabWidth();
        int left = index * width;
        if (left - scroll < 0) scroll = left;
        else if (left + width - scroll > ViewportWidth) scroll = left + width - ViewportWidth;
        ClampScroll();
    }

    private Rectangle TabBounds(int index)
    {
        int width = TabWidth();
        return new Rectangle(TabsLeft + index * width - scroll, 0, width, Height);
    }

    private Rectangle CloseBounds(int index)
    {
        Rectangle tab = TabBounds(index);
        return new Rectangle(tab.Right - CloseSize - 7,
                             (Height - CloseSize) / 2, CloseSize, CloseSize);
    }

    private Rectangle LeftArrowBounds() { return new Rectangle(0, 0, ArrowWidth, Height); }

    private Rectangle RightArrowBounds()
    {
        return new Rectangle(StripWidth - ArrowWidth, 0, ArrowWidth, Height);
    }

    private Rectangle NewButtonBounds()
    {
        return new Rectangle(Width - NewButtonWidth, 0, NewButtonWidth, Height);
    }

    private Rectangle ViewportBounds()
    {
        return new Rectangle(TabsLeft, 0, ViewportWidth, Height);
    }

    private Part HitTest(Point point, out int index)
    {
        index = -1;
        if (NewButtonBounds().Contains(point)) return Part.NewTab;
        if (Overflowing)
        {
            if (LeftArrowBounds().Contains(point)) return Part.ScrollLeft;
            if (RightArrowBounds().Contains(point)) return Part.ScrollRight;
        }
        if (!ViewportBounds().Contains(point)) return Part.None;

        int width = TabWidth();
        if (width <= 0) return Part.None;
        int candidate = (point.X - TabsLeft + scroll) / width;
        if (candidate < 0 || candidate >= labels.Count) return Part.None;

        index = candidate;
        return CloseBounds(candidate).Contains(point) ? Part.Close : Part.Tab;
    }

    // ---- scrolling ----

    /// <summary>Scroll by whole tabs, so a click always lands somewhere sensible.</summary>
    public void ScrollBy(int tabs)
    {
        int before = scroll;
        scroll += tabs * TabWidth();
        ClampScroll();
        if (scroll != before) Invalidate();
    }

    // ---- painting ----

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color back = Theme.WindowBack;
        Color border = Theme.Border;

        using (SolidBrush fill = new SolidBrush(back))
            g.FillRectangle(fill, ClientRectangle);
        using (Pen pen = new Pen(border))
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

        // Tabs are clipped to their viewport so none can bleed over an arrow.
        //
        // ⚠ `g.Clip` is a GETTER THAT ALLOCATES - it hands back a new Region that
        // the caller owns. Assigning it back does not dispose it, so the old code
        // leaked one Region per paint, and paints happen on every hover move.
        using (Region saved = g.Clip)
        {
            g.SetClip(ViewportBounds());
            using (Font font = TabFont())
            {
                for (int index = 0; index < labels.Count; index++)
                {
                    Rectangle bounds = TabBounds(index);
                    if (bounds.Right < TabsLeft || bounds.Left > TabsLeft + ViewportWidth) continue;
                    PaintTab(g, font, index, bounds, Theme.EditorBack, border);
                }
            }
            g.Clip = saved;
        }

        if (Overflowing)
        {
            PaintArrow(g, LeftArrowBounds(), true, CanScrollLeft,
                       hoverPart == Part.ScrollLeft);
            PaintArrow(g, RightArrowBounds(), false, CanScrollRight,
                       hoverPart == Part.ScrollRight);
        }
        PaintNewButton(g);
    }

    private void PaintTab(Graphics g, Font font, int index, Rectangle bounds,
                          Color activeBack, Color border)
    {
        bool active = index == selected;
        bool hovered = index == hoverIndex && (hoverPart == Part.Tab || hoverPart == Part.Close);

        Color fillColour = active ? activeBack
                         : hovered ? Theme.MenuHighlight
                         : Theme.WindowBack;
        using (SolidBrush brush = new SolidBrush(fillColour))
            g.FillRectangle(brush, bounds);

        if (active)
        {
            // A browser-style accent along the top, and no bottom line, so the
            // tab reads as joined to the page below it.
            using (SolidBrush brush = new SolidBrush(Theme.UiAccent))
                g.FillRectangle(brush, bounds.Left, 0, bounds.Width, 2);
            using (Pen pen = new Pen(border))
            {
                g.DrawLine(pen, bounds.Left, 0, bounds.Left, Height - 1);
                g.DrawLine(pen, bounds.Right - 1, 0, bounds.Right - 1, Height - 1);
            }
            using (SolidBrush brush = new SolidBrush(activeBack))
                g.FillRectangle(brush, bounds.Left + 1, Height - 1, bounds.Width - 2, 1);
        }
        else
        {
            using (Pen pen = new Pen(border))
                g.DrawLine(pen, bounds.Right - 1, 6, bounds.Right - 1, Height - 7);
        }

        Rectangle text = new Rectangle(bounds.Left + 9, 0, bounds.Width - CloseSize - 18, Height);
        TextRenderer.DrawText(g, labels[index], font, text,
                              active ? Theme.EditorText : Theme.UiDim,
                              TextFormatFlags.VerticalCenter | TextFormatFlags.Left
                              | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        Rectangle close = CloseBounds(index);
        bool closeHot = index == hoverIndex && hoverPart == Part.Close;
        if (closeHot)
        {
            using (SolidBrush brush = new SolidBrush(Theme.Border))
                g.FillEllipse(brush, close);
        }
        using (Pen pen = new Pen(closeHot || active ? Theme.EditorText : Theme.UiDim, 1.3f))
        {
            int pad = 4;
            g.DrawLine(pen, close.Left + pad, close.Top + pad,
                            close.Right - pad, close.Bottom - pad);
            g.DrawLine(pen, close.Right - pad, close.Top + pad,
                            close.Left + pad, close.Bottom - pad);
        }
    }

    private void PaintArrow(Graphics g, Rectangle bounds, bool pointsLeft,
                            bool enabled, bool hovered)
    {
        using (SolidBrush brush = new SolidBrush(Theme.WindowBack))
            g.FillRectangle(brush, bounds);
        if (hovered && enabled)
        {
            using (SolidBrush brush = new SolidBrush(Theme.MenuHighlight))
                g.FillRectangle(brush, Rectangle.Inflate(bounds, -4, -5));
        }

        // Greyed rather than hidden at the ends: a control that vanishes is
        // harder to aim at than one that simply stops responding.
        Color ink = !enabled ? Theme.Border
                  : hovered ? Theme.EditorText
                  : Theme.UiDim;
        int cx = bounds.Left + bounds.Width / 2;
        int cy = bounds.Top + Height / 2;
        int reach = 4;
        using (Pen pen = new Pen(ink, 1.7f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            if (pointsLeft)
            {
                g.DrawLines(pen, new Point[] {
                    new Point(cx + 2, cy - reach), new Point(cx - 2, cy),
                    new Point(cx + 2, cy + reach) });
            }
            else
            {
                g.DrawLines(pen, new Point[] {
                    new Point(cx - 2, cy - reach), new Point(cx + 2, cy),
                    new Point(cx - 2, cy + reach) });
            }
        }
        using (Pen pen = new Pen(Theme.Border))
            g.DrawLine(pen, bounds.Left, Height - 1, bounds.Right, Height - 1);
    }

    private void PaintNewButton(Graphics g)
    {
        Rectangle plus = NewButtonBounds();
        bool hovered = hoverPart == Part.NewTab;

        using (SolidBrush fill = new SolidBrush(Theme.WindowBack))
            g.FillRectangle(fill, plus);
        if (hovered)
        {
            using (SolidBrush fill = new SolidBrush(Theme.MenuHighlight))
                g.FillRectangle(fill, Rectangle.Inflate(plus, -5, -5));
        }
        using (Pen pen = new Pen(hovered ? Theme.EditorText : Theme.UiDim, 1.4f))
        {
            int cx = plus.Left + plus.Width / 2;
            int cy = plus.Top + plus.Height / 2;
            g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
            g.DrawLine(pen, cx, cy - 5, cx, cy + 5);
        }
        using (Pen pen = new Pen(Theme.Border))
            g.DrawLine(pen, plus.Left, Height - 1, plus.Right, Height - 1);
    }

    // ---- mouse ----

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index;
        Part part = HitTest(e.Location, out index);
        if (part == hoverPart && index == hoverIndex) return;
        hoverPart = part;
        hoverIndex = index;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hoverPart == Part.None && hoverIndex == -1) return;
        hoverPart = Part.None;
        hoverIndex = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int index;
        Part part = HitTest(e.Location, out index);

        if (e.Button == MouseButtons.Middle)
        {
            // Middle-click closes, anywhere on the tab, as browsers do.
            if ((part == Part.Tab || part == Part.Close) && TabCloseRequested != null)
                TabCloseRequested(index);
            return;
        }
        if (e.Button != MouseButtons.Left) return;

        switch (part)
        {
            case Part.ScrollLeft:
                ScrollBy(-1);
                break;
            case Part.ScrollRight:
                ScrollBy(1);
                break;
            case Part.NewTab:
                if (NewTabRequested != null) NewTabRequested(this, EventArgs.Empty);
                break;
            case Part.Close:
                if (TabCloseRequested != null) TabCloseRequested(index);
                break;
            case Part.Tab:
                if (TabSelected != null) TabSelected(index);
                break;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Overflowing) return;
        ScrollBy(e.Delta > 0 ? -1 : 1);
    }

    // Narrowing the window can push the tab you are working in off the end, so
    // the scroll position is rechecked whenever the strip changes size rather
    // than only when the tabs themselves change.
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ClampScroll();
        EnsureVisible(selected);
    }
}
