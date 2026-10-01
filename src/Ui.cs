using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RaiTilePackageManager
{
    /// <summary>
    /// Colours of the "TG sottopancia" world, read from the rai.it and rainews.it stylesheets (PRODUCT.md).
    /// In Windows high-contrast mode every role falls back to a system colour.
    /// </summary>
    static class Theme
    {
        static bool HighContrast => SystemInformation.HighContrast;
        public static Color Desk => HighContrast ? SystemColors.Control : Rgb(0xF5F7FF);
        public static Color Panel => HighContrast ? SystemColors.Window : Color.White;
        public static Color Ink => HighContrast ? SystemColors.WindowText : Rgb(0x10193C);
        public static Color Muted => HighContrast ? SystemColors.GrayText : Rgb(0x4E6573);
        public static Color Rule => HighContrast ? SystemColors.WindowFrame : Rgb(0xD3DAEE);
        public static Color Band => HighContrast ? SystemColors.Highlight : Rgb(0x000099);
        public static Color BandText => HighContrast ? SystemColors.HighlightText : Color.White;
        public static Color BandSoft => HighContrast ? SystemColors.HighlightText : Rgb(0xD6DDFF);
        public static Color Monitor => HighContrast ? SystemColors.Window : Rgb(0x10193C);
        public static Color MonitorGrid => HighContrast ? SystemColors.WindowFrame : Rgb(0x1E2A5C);
        public static Color MonitorGridHover => HighContrast ? SystemColors.Highlight : Rgb(0x2E4196);
        public static Color Action => HighContrast ? SystemColors.Highlight : Rgb(0x0060E6);
        public static Color ActionHover => HighContrast ? SystemColors.Highlight : Rgb(0x1061D4);
        public static Color ActionPressed => HighContrast ? SystemColors.Highlight : Rgb(0x000099);
        public static Color ActionTint => HighContrast ? SystemColors.Window : Rgb(0xEEF1FC);
        public static Color Disabled => HighContrast ? SystemColors.GrayText : Rgb(0xB9C3E0);
        public static Color Live => HighContrast ? SystemColors.Highlight : Rgb(0xC22C2F);
        public static Color Ok => HighContrast ? SystemColors.Highlight : Rgb(0x1F8555);
        public static Color Warn => HighContrast ? SystemColors.Highlight : Rgb(0xFF510C); // rainews.it orange label colour
        public static Color Off => HighContrast ? SystemColors.GrayText : Rgb(0x4E6573);

        public static Color Of(Status status) =>
            status == Status.Ok ? Ok : status == Status.Warning ? Warn : status == Status.Error ? Live : Action;

        static Color Rgb(int rgb) => Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    }

    /// <summary>
    /// Inter Tight, the display face of rai.it (SIL Open Font License, src/fonts/OFL.txt), embedded in the exe and
    /// registered for GDI+ and GDI so both Font objects and TextRenderer find it.
    /// </summary>
    static class Fonts
    {
        static readonly PrivateFontCollection Collection = new PrivateFontCollection();
        static readonly FontFamily SemiBoldFamily, Family;

        static Fonts()
        {
            foreach (var file in new[] { "InterTight-Regular.ttf", "InterTight-SemiBold.ttf", "InterTight-Bold.ttf" })
                using (var s = typeof(Fonts).Assembly.GetManifestResourceStream(file))
                using (var m = new MemoryStream())
                {
                    s.CopyTo(m);
                    var memory = Marshal.AllocCoTaskMem((int)m.Length); // never freed: GDI and GDI+ read it for the whole session
                    Marshal.Copy(m.ToArray(), 0, memory, (int)m.Length);
                    Collection.AddMemoryFont(memory, (int)m.Length);
                    uint added = 0;
                    AddFontMemResourceEx(memory, (uint)m.Length, IntPtr.Zero, ref added);
                }
            // For GDI a static SemiBold is a family of its own; Regular and Bold are the two styles of "Inter Tight".
            SemiBoldFamily = Collection.Families.FirstOrDefault(f => f.Name == "Inter Tight SemiBold");
            Family = Collection.Families.FirstOrDefault(f => f.Name == "Inter Tight");
        }

        public static Font Regular(float size) =>
            Family != null ? new Font(Family, size) : new Font(SystemFonts.MessageBoxFont.FontFamily, size);

        public static Font SemiBold(float size) =>
            SemiBoldFamily != null ? new Font(SemiBoldFamily, size) : new Font(SystemFonts.MessageBoxFont.FontFamily, size, FontStyle.Bold);

        public static Font Bold(float size) =>
            Family != null ? new Font(Family, size, FontStyle.Bold) : new Font(SystemFonts.MessageBoxFont.FontFamily, size, FontStyle.Bold);

        [DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr font, uint length, IntPtr reserved, ref uint fonts);
    }

    enum Glyph { None, Info, Ok, Warning, Error, Drop }

    /// <summary>
    /// The lower third ("sottopancia") every status is drawn as: a blue band, a full-height tab carrying the state
    /// as a drawn glyph or a short label, a bold title and an optional detail. Shared by Strap and DropZone.
    /// </summary>
    static class Sottopancia
    {
        public static readonly Font TitleFont = Fonts.Bold(10f);
        public static readonly Font DetailFont = Fonts.Regular(9.25f);
        const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;

        struct Layout { public Rectangle Tab, Title, Detail; public int Height; }

        static Layout Measure(float k, int width, string tabLabel, string title, string detail, Font titleFont, Font detailFont)
        {
            // Paddings follow the title line, so a broadcast-size strap keeps the proportions of a status strap.
            int titleLine = TextRenderer.MeasureText("Ag", titleFont).Height;
            int padX = (int)(titleLine * 0.8f), padY = (int)(titleLine * 0.5f), gap = (int)(2 * k);
            int tabWidth = tabLabel == null ? titleLine + 2 * padY : TextRenderer.MeasureText(tabLabel, titleFont).Width + 2 * padX;
            int textX = tabWidth + padX, textWidth = Math.Max(10, width - textX - padX);
            var titleSize = TextRenderer.MeasureText(title, titleFont, new Size(textWidth, int.MaxValue), Wrap);
            var detailSize = string.IsNullOrEmpty(detail) ? Size.Empty : TextRenderer.MeasureText(detail, detailFont, new Size(textWidth, int.MaxValue), Wrap);
            int height = Math.Max(titleLine + 2 * padY, padY + titleSize.Height + (detailSize.Height > 0 ? gap + detailSize.Height : 0) + padY);
            return new Layout
            {
                Tab = new Rectangle(0, 0, tabWidth, height),
                Title = new Rectangle(textX, padY, textWidth, titleSize.Height),
                Detail = new Rectangle(textX, padY + titleSize.Height + gap, textWidth, detailSize.Height),
                Height = height,
            };
        }

        public static int Height(float k, int width, string tabLabel, string title, string detail, Font titleFont = null, Font detailFont = null) =>
            Measure(k, width, tabLabel, title, detail, titleFont ?? TitleFont, detailFont ?? DetailFont).Height;

        /// <summary>The width that fits every line unwrapped: the answer to an unconstrained layout pass.</summary>
        public static int NaturalWidth(float k, string tabLabel, string title, string detail)
        {
            var l = Measure(k, int.MaxValue / 4, tabLabel, title, "", TitleFont, DetailFont);
            int text = Math.Max(TextRenderer.MeasureText(title, TitleFont).Width,
                (detail ?? "").Split('\n').Max(line => TextRenderer.MeasureText(line, DetailFont).Width));
            return l.Title.X + text + l.Title.X - l.Tab.Width;
        }

        /// <summary>Draws at <paramref name="origin"/>; <paramref name="reveal"/> &lt; 1 is the on-air wipe in progress.</summary>
        public static void Paint(Graphics g, Point origin, int width, float k, float reveal, Color tabColor, Glyph glyph, string tabLabel, string title, string detail,
            Font titleFont = null, Font detailFont = null)
        {
            titleFont = titleFont ?? TitleFont;
            detailFont = detailFont ?? DetailFont;
            var l = Measure(k, width, tabLabel, title, detail, titleFont, detailFont);
            var state = g.Save();
            g.TranslateTransform(origin.X, origin.Y);
            g.SetClip(new Rectangle(0, 0, (int)Math.Ceiling(width * reveal), l.Height));
            using (var band = new SolidBrush(Theme.Band)) g.FillRectangle(band, 0, 0, width, l.Height);
            using (var tab = new SolidBrush(tabColor)) g.FillRectangle(tab, l.Tab);
            if (tabLabel != null)
                TextRenderer.DrawText(g, tabLabel, titleFont, l.Tab, Theme.BandText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
            else
                DrawGlyph(g, new Rectangle(l.Tab.X, l.Tab.Y, l.Tab.Width, l.Tab.Width), glyph, k);
            const TextFormatFlags flags = Wrap | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;
            TextRenderer.DrawText(g, title, titleFont, l.Title, Theme.BandText, flags);
            if (!string.IsNullOrEmpty(detail)) TextRenderer.DrawText(g, detail, detailFont, l.Detail, Theme.BandSoft, flags);
            g.Restore(state);
        }

        /// <summary>The state icons: one 2 px round-capped stroke, drawn, never font glyphs.</summary>
        public static void DrawGlyph(Graphics g, Rectangle box, Glyph glyph, float k)
        {
            if (glyph == Glyph.None) return;
            float c = box.X + box.Width / 2f, m = box.Y + box.Height / 2f, s = box.Width * 0.2f;
            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Theme.BandText, 2f * k) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var ink = new SolidBrush(Theme.BandText))
            {
                float dot = 2.6f * k;
                switch (glyph)
                {
                    case Glyph.Ok:
                        g.DrawLines(pen, new[] { new PointF(c - s, m + s * 0.05f), new PointF(c - s * 0.3f, m + s * 0.75f), new PointF(c + s * 1.05f, m - s * 0.7f) });
                        break;
                    case Glyph.Error:
                        g.DrawLine(pen, c - s * 0.8f, m - s * 0.8f, c + s * 0.8f, m + s * 0.8f);
                        g.DrawLine(pen, c + s * 0.8f, m - s * 0.8f, c - s * 0.8f, m + s * 0.8f);
                        break;
                    case Glyph.Warning:
                        g.DrawLine(pen, c, m - s, c, m + s * 0.25f);
                        g.FillEllipse(ink, c - dot / 2, m + s * 0.85f - dot / 2, dot, dot);
                        break;
                    case Glyph.Info:
                        g.FillEllipse(ink, c - dot / 2, m - s * 0.95f - dot / 2, dot, dot);
                        g.DrawLine(pen, c, m - s * 0.25f, c, m + s);
                        break;
                    case Glyph.Drop:
                        g.DrawLine(pen, c, m - s * 1.1f, c, m + s * 0.35f);
                        g.DrawLines(pen, new[] { new PointF(c - s * 0.6f, m - s * 0.2f), new PointF(c, m + s * 0.4f), new PointF(c + s * 0.6f, m - s * 0.2f) });
                        g.DrawLines(pen, new[] { new PointF(c - s * 1.1f, m + s * 0.45f), new PointF(c - s * 1.1f, m + s * 1.1f), new PointF(c + s * 1.1f, m + s * 1.1f), new PointF(c + s * 1.1f, m + s * 0.45f) });
                        break;
                }
            }
            g.SmoothingMode = mode;
        }

        public static Glyph GlyphOf(Status status) =>
            status == Status.Ok ? Glyph.Ok : status == Status.Warning ? Glyph.Warning : status == Status.Error ? Glyph.Error : Glyph.Info;
    }

    /// <summary>
    /// The on-air entrance: new content wipes in from the left like a lower third. Exponential ease-out, 240 ms,
    /// starting a third visible; skipped when Windows animations are off.
    /// </summary>
    sealed class Wipe
    {
        readonly Control owner;
        readonly Timer timer = new Timer { Interval = 15 };
        DateTime start;
        public float Reveal { get; private set; } = 1;

        public Wipe(Control owner)
        {
            this.owner = owner;
            timer.Tick += (s, e) =>
            {
                double t = (DateTime.UtcNow - start).TotalMilliseconds / 240;
                Reveal = t >= 1 ? 1 : (float)(0.35 + 0.65 * (1 - Math.Pow(2, -10 * t)));
                if (t >= 1) timer.Stop();
                owner.Invalidate();
            };
        }

        public void Run()
        {
            if (!SystemInformation.UIEffectsEnabled || !owner.IsHandleCreated)
            {
                Reveal = 1;
                return;
            }
            start = DateTime.UtcNow;
            Reveal = 0.35f;
            timer.Start();
        }
    }

    /// <summary>A status line in the page: a sottopancia that sizes itself to its text.</summary>
    sealed class Strap : Control
    {
        readonly Wipe wipe;
        Color tabColor = Theme.Action;
        Glyph glyph = Glyph.Info;
        string tabLabel, title = "", detail = "";

        public Strap()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AutoSize = true;
            Anchor = AnchorStyles.Left | AnchorStyles.Right;
            Margin = new Padding(0, 6, 0, 6);
            AccessibleRole = AccessibleRole.StaticText;
            Visible = false;
            wipe = new Wipe(this);
        }

        public void Show(Status status, string newTitle, string newDetail = null) =>
            Set(Theme.Of(status), Sottopancia.GlyphOf(status), null, newTitle, newDetail);

        /// <summary>The server tally: red IN ONDA while serving, grey FUORI ONDA when stopped.</summary>
        public void ShowTally(bool onAir, string newTitle, string newDetail) =>
            Set(onAir ? Theme.Live : Theme.Off, Glyph.None, onAir ? "IN ONDA" : "FUORI ONDA", newTitle, newDetail);

        public void Clear()
        {
            Visible = false;
            title = detail = "";
            tabLabel = null;
        }

        void Set(Color color, Glyph newGlyph, string newTabLabel, string newTitle, string newDetail)
        {
            bool entrance = !Visible || newTitle != title || newTabLabel != tabLabel || color != tabColor;
            bool resized = entrance || (newDetail ?? "") != detail;
            tabColor = color;
            glyph = newGlyph;
            tabLabel = newTabLabel;
            title = newTitle ?? "";
            detail = newDetail ?? "";
            AccessibleName = string.IsNullOrEmpty(detail) ? title : title + ". " + detail;
            Visible = true;
            if (resized) Parent?.PerformLayout(this, "Bounds");
            if (entrance) wipe.Run(); // counters ticking in the detail do not replay the entrance
            Invalidate();
        }

        float K => DeviceDpi / 96f;

        public override Size GetPreferredSize(Size proposed)
        {
            // Layout asks first without a width (0, 1 or huge): answer unwrapped, then wrap to the column it gets.
            bool constrained = proposed.Width > 1 && proposed.Width < 20000;
            int width = constrained ? proposed.Width : Sottopancia.NaturalWidth(K, tabLabel, title, detail);
            return new Size(width, Sottopancia.Height(K, width, tabLabel, title, detail));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Desk);
            Sottopancia.Paint(e.Graphics, Point.Empty, Width, K, wipe.Reveal, tabColor, glyph, tabLabel, title, detail);
        }
    }

    /// <summary>
    /// The program monitor: a dark field of tiles where the package is dropped, or clicked to browse; its lower third
    /// says what is on air in this tab. Keyboard: Enter or Space browses.
    /// </summary>
    sealed class DropZone : Control
    {
        // Dropping the package is the main action: its lower third is drawn at broadcast scale, not status scale.
        static readonly Font TitleFont = Fonts.Bold(15.5f), DetailFont = Fonts.Regular(10.5f);
        readonly Wipe wipe;
        string title = "", detail = "";
        Glyph glyph = Glyph.Drop;
        Color tabColor = Theme.Action;
        bool? dragValid; // null = no drag over the monitor
        bool hover;

        public event EventHandler Browse;

        public DropZone()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Anchor = AnchorStyles.Left | AnchorStyles.Right;
            Margin = new Padding(0, 0, 0, 6);
            Height = 214;
            AccessibleRole = AccessibleRole.PushButton;
            wipe = new Wipe(this);
        }

        public void Set(Glyph newGlyph, Color color, string newTitle, string newDetail)
        {
            if (newTitle != title || color != tabColor) wipe.Run();
            glyph = newGlyph;
            tabColor = color;
            title = newTitle;
            detail = newDetail;
            AccessibleName = title + ". " + detail;
            Invalidate();
        }

        /// <summary>Highlights a drag over the monitor: true = a package, false = something else, null = drag over.</summary>
        public void Dragging(bool? valid)
        {
            dragValid = valid;
            Invalidate();
        }

        float K => DeviceDpi / 96f;

        public override Size GetPreferredSize(Size proposed) => new Size(proposed.Width > 1 && proposed.Width < 20000 ? proposed.Width : Width, Height);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float k = K;
            g.Clear(Theme.Monitor);
            // The tile grid the app writes, as the monitor's texture.
            int cell = (int)(44 * k);
            using (var grid = new Pen(hover && Enabled ? Theme.MonitorGridHover : Theme.MonitorGrid))
            {
                for (int x = cell / 2; x < Width; x += cell) g.DrawLine(grid, x, 0, x, Height);
                for (int y = cell / 3; y < Height; y += cell) g.DrawLine(grid, 0, y, Width, y);
            }

            string t = title, d = detail;
            var gl = glyph;
            var color = tabColor;
            if (dragValid == true)
            {
                t = "Rilascia per aprire il pacchetto";
                d = "Il file resta dov'è: l'app legge le tile e le copia nel progetto.";
                gl = Glyph.Drop;
                color = Theme.Ok;
            }
            else if (dragValid == false)
            {
                t = "Questo non è un tile package";
                d = "Serve un file .tpkx o .tpk esportato da ArcGIS Pro.";
                gl = Glyph.Warning;
                color = Theme.Warn;
            }
            int margin = (int)(24 * k);
            int width = Math.Min(Width - 2 * margin, (int)(660 * k));
            int height = Sottopancia.Height(k, width, null, t, d, TitleFont, DetailFont);
            Sottopancia.Paint(g, new Point(margin, Height - margin - height), width, k, dragValid == null ? wipe.Reveal : 1,
                Enabled ? color : Theme.Off, gl, null, t, d, TitleFont, DetailFont);

            if (dragValid != null || (Focused && ShowFocusCues))
                using (var pen = new Pen(dragValid == false ? Theme.Warn : dragValid == true ? Theme.Ok : Theme.BandText, (dragValid != null ? 3 : 2) * k))
                {
                    int inset = (int)Math.Ceiling(pen.Width / 2);
                    g.DrawRectangle(pen, inset, inset, Width - 2 * inset - 1, Height - 2 * inset - 1);
                }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Focus();
            Browse?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space) return;
            e.Handled = true;
            Browse?.Invoke(this, EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData);
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    /// <summary>A tab of the header band: white text, a white bar under the open one.</summary>
    sealed class TabButton : Control
    {
        static readonly Font TabFont = Fonts.SemiBold(10.5f);
        bool selected, hover, onAir;

        public event EventHandler Chosen;

        public TabButton(string text)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Text = text;
            TabStop = true;
            AutoSize = true;
            Margin = Padding.Empty;
            BackColor = Theme.Band;
            AccessibleRole = AccessibleRole.PageTab;
            AccessibleName = text;
        }

        public bool Selected
        {
            get => selected;
            set { selected = value; Describe(); Invalidate(); }
        }

        /// <summary>A small red tally beside the label: the server is on air whatever tab is open.</summary>
        public bool OnAir
        {
            get => onAir;
            set
            {
                if (onAir == value) return;
                onAir = value;
                Describe();
                Parent?.PerformLayout(this, "Bounds");
                Invalidate();
            }
        }

        void Describe() =>
            AccessibleDescription = string.Join(", ", new[] { selected ? "scheda aperta" : null, onAir ? "server in onda" : null }.Where(x => x != null));

        float K => DeviceDpi / 96f;
        int TallySize => (int)(9 * K);
        int TallyGap => (int)(8 * K);

        public override Size GetPreferredSize(Size proposed) =>
            new Size(TextRenderer.MeasureText(Text, TabFont).Width + (int)(28 * K) + (onAir ? TallySize + TallyGap : 0), (int)(56 * K));

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float k = K;
            g.Clear(Theme.Band);
            var text = ClientRectangle;
            if (onAir)
            {
                int x = (int)(14 * k), y = (Height - TallySize) / 2;
                using (var red = new SolidBrush(Theme.Live)) g.FillRectangle(red, x, y, TallySize, TallySize);
                using (var edge = new Pen(Theme.BandText, Math.Max(1, k))) g.DrawRectangle(edge, x, y, TallySize, TallySize); // red alone is 2.5:1 on the band
                text = new Rectangle(x + TallySize + TallyGap, 0, Width - x - TallySize - TallyGap - (int)(14 * k), Height);
            }
            TextRenderer.DrawText(g, Text, TabFont, text, selected ? Theme.BandText : Theme.BandSoft,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int bar = selected ? (int)(3 * k) : hover ? (int)Math.Max(1, k) : 0;
            if (bar > 0)
                using (var b = new SolidBrush(selected ? Theme.BandText : Theme.BandSoft))
                    g.FillRectangle(b, (int)(12 * k), Height - bar, Width - (int)(24 * k), bar);
            if (Focused && ShowFocusCues)
                using (var pen = new Pen(Theme.BandText, Math.Max(1, k)) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(pen, (int)(6 * k), (int)(10 * k), Width - (int)(12 * k) - 1, Height - (int)(20 * k) - 1);
        }

        protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); Chosen?.Invoke(this, EventArgs.Empty); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space) return;
            e.Handled = true;
            Chosen?.Invoke(this, EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData);
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    /// <summary>
    /// Square buttons of the world: primary filled with the Rai action blue, secondary outlined. The width is fixed by the
    /// longest label the button can show, so changing its text never moves the row.
    /// </summary>
    sealed class RaiButton : Button
    {
        static readonly Font ButtonFont = Fonts.SemiBold(9.75f);
        readonly string[] labels;
        bool primary = true, hover, down;

        public RaiButton(string text, bool primary, params string[] otherLabels)
        {
            labels = otherLabels.Concat(new[] { text }).ToArray();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Text = text;
            this.primary = primary;
            Font = ButtonFont;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(6, 3, 0, 3);
        }

        public bool Primary
        {
            get => primary;
            set { primary = value; Invalidate(); }
        }

        float K => DeviceDpi / 96f;

        public override Size GetPreferredSize(Size proposed)
        {
            int text = labels.Max(l => TextRenderer.MeasureText(l, ButtonFont).Width);
            return new Size(Math.Max(text + (int)(32 * K), (int)(112 * K)), (int)(32 * K));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float k = K;
            Color fill, ink, border;
            if (!Enabled)
            {
                fill = primary ? Theme.Disabled : Theme.Panel;
                ink = primary ? Theme.Panel : Theme.Muted;
                border = primary ? fill : Theme.Rule;
            }
            else if (primary)
            {
                fill = down ? Theme.ActionPressed : hover ? Theme.ActionHover : Theme.Action;
                ink = Theme.BandText;
                border = fill;
            }
            else
            {
                fill = down ? Theme.Rule : hover ? Theme.ActionTint : Theme.Panel;
                ink = Theme.Action;
                border = Theme.Action;
            }
            g.Clear(fill);
            using (var pen = new Pen(border)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
            TextRenderer.DrawText(g, Text, ButtonFont, ClientRectangle, ink, flags);
            if (Focused && ShowFocusCues)
                using (var pen = new Pen(primary ? Theme.BandText : Theme.Ink, 2 * k))
                {
                    int inset = (int)(3 * k);
                    g.DrawRectangle(pen, inset, inset, Width - 2 * inset - 1, Height - 2 * inset - 1);
                }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    }

    /// <summary>A thin progress line in the action blue.</summary>
    sealed class ProgressLine : Control
    {
        float value;

        public ProgressLine()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Anchor = AnchorStyles.Left | AnchorStyles.Right;
            Height = 4;
            Margin = new Padding(0, 8, 0, 2);
            AccessibleRole = AccessibleRole.ProgressBar;
        }

        public float Value
        {
            get => value;
            set
            {
                this.value = Math.Max(0, Math.Min(1, value));
                AccessibleName = $"{this.value:P0}";
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Rule);
            using (var fill = new SolidBrush(Theme.Action)) e.Graphics.FillRectangle(fill, 0, 0, (int)(Width * value), Height);
        }
    }

    /// <summary>The frame of a text box or a numeric box: as tall as the buttons beside it; the border turns action blue while typing.</summary>
    sealed class FieldHost : Panel
    {
        readonly Control box;

        public FieldHost(Control box)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            this.box = box;
            BackColor = box.BackColor;
            Margin = new Padding(0, 3, 0, 3);
            Height = 32;
            Cursor = Cursors.IBeam;
            if (box is TextBoxBase text) text.BorderStyle = BorderStyle.None;
            if (box is UpDownBase updown) updown.BorderStyle = BorderStyle.None;
            Controls.Add(box);
            box.Enter += (s, e) => Invalidate();
            box.Leave += (s, e) => Invalidate();
            box.EnabledChanged += (s, e) => Invalidate(); // a disabled box is grey: the frame follows it
            Click += (s, e) => box.Focus();
        }

        float K => DeviceDpi / 96f;

        public override Size GetPreferredSize(Size proposed) =>
            new Size(proposed.Width > 1 && proposed.Width < 20000 ? proposed.Width : Width, (int)(32 * K));

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = (int)(9 * K);
            box.SetBounds(pad, (Height - box.Height) / 2, Width - 2 * pad, box.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(box.Enabled ? BackColor : SystemColors.Control);
            bool focused = box.ContainsFocus && !(box is TextBoxBase text && text.ReadOnly);
            using (var pen = new Pen(focused ? Theme.Action : Theme.Rule, focused ? 2 * K : 1))
            {
                int inset = (int)(pen.Width / 2);
                e.Graphics.DrawRectangle(pen, inset, inset, Width - 2 * inset - 1, Height - 2 * inset - 1);
            }
        }
    }

    /// <summary>
    /// The Rai logo as published on rai.it (inline SVG, 100×100: the square with the letters cut out), drawn as a vector
    /// so it stays sharp at any scale. On the blue band it is used reversed, in white.
    /// </summary>
    static class RaiLogo
    {
        const string Data =
            "M0 100h100V0H0v100zm31.352-30.302-9.608-15.062h-.103v15.062H11.46v-39.16h15.218c7.738 0 13.606 3.686 13.606 12.049 " +
            "0 5.401-3.01 10.075-8.569 11.063l12.309 16.048H31.352zm42.084 0h-9.451v-2.96h-.105c-1.662 2.648-4.934 3.842-8.05 3.842" +
            "-7.894 0-13.451-6.595-13.451-14.23 0-7.635 5.453-14.179 13.347-14.179 3.065 0 6.284 1.143 8.259 3.48v-2.596h9.45v26.643z" +
            "m5.443 0h9.452V43.055H78.88v26.643zM83.61 38.12c-2.908 0-5.299-2.39-5.299-5.298s2.39-5.297 5.3-5.297c2.908 0 5.295 2.389 " +
            "5.295 5.297 0 2.909-2.387 5.298-5.296 5.298zm-60.984.209h-.986v9.764h.986c3.324 0 7.063-.623 7.063-4.882 0-4.26-3.74-4.882" +
            "-7.063-4.882zM52.14 56.402c0-3.273 2.232-6.025 6.076-6.025 3.843 0 6.076 2.752 6.076 6.025 0 3.375-2.233 5.974-6.076 5.974" +
            "-3.844 0-6.076-2.599-6.076-5.974z";

        static readonly GraphicsPath Shape = Parse(Data);

        public static void Draw(Graphics g, Rectangle bounds, Color color)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(bounds.Width / 100f, bounds.Height / 100f);
            using (var brush = new SolidBrush(color)) g.FillPath(brush, Shape);
            g.Restore(state);
        }

        /// <summary>The subset of SVG path syntax the logo uses: M L H V C S Z, absolute and relative, even-odd fill.</summary>
        static GraphicsPath Parse(string d)
        {
            var path = new GraphicsPath(FillMode.Alternate);
            var tokens = Regex.Matches(d, @"[MLHVCSZmlhvcsz]|-?(?:\d+\.?\d*|\.\d+)").Cast<Match>().Select(m => m.Value).ToArray();
            int i = 0;
            char command = 'M';
            PointF current = PointF.Empty, start = PointF.Empty, control = PointF.Empty;
            float Number() => float.Parse(tokens[i++], CultureInfo.InvariantCulture);
            while (i < tokens.Length)
            {
                if (char.IsLetter(tokens[i][0])) command = tokens[i++][0];
                bool relative = char.IsLower(command);
                PointF Point() { float x = Number(), y = Number(); return relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y); }
                var next = current;
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        next = start = Point();
                        path.StartFigure();
                        command = relative ? 'l' : 'L'; // further pairs after a moveto are linetos
                        break;
                    case 'L': next = Point(); path.AddLine(current, next); break;
                    case 'H': { float x = Number(); next = new PointF(relative ? current.X + x : x, current.Y); path.AddLine(current, next); break; }
                    case 'V': { float y = Number(); next = new PointF(current.X, relative ? current.Y + y : y); path.AddLine(current, next); break; }
                    case 'C':
                    {
                        var c1 = Point(); var c2 = Point(); next = Point();
                        path.AddBezier(current, c1, c2, next);
                        control = c2;
                        break;
                    }
                    case 'S':
                    {
                        var c1 = new PointF(2 * current.X - control.X, 2 * current.Y - control.Y);
                        var c2 = Point(); next = Point();
                        path.AddBezier(current, c1, c2, next);
                        control = c2;
                        break;
                    }
                    case 'Z': path.CloseFigure(); next = start; break;
                }
                if ("CcSs".IndexOf(command) < 0) control = next;
                current = next;
            }
            return path;
        }
    }

    /// <summary>A panel that lets mouse hits through to the window, so its empty areas drag the window like a title bar.</summary>
    class CaptionPanel : Panel
    {
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84, HTTRANSPARENT = -1;
            if (m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)HTTRANSPARENT; return; }
            base.WndProc(ref m);
        }
    }

    enum CaptionAction { Minimize, Maximize, Close }

    /// <summary>The window buttons drawn in the header band, with the glyphs and hover colours Windows uses.</summary>
    sealed class CaptionButton : Control
    {
        readonly CaptionAction action;
        bool hover, down;

        public CaptionButton(CaptionAction action)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.Selectable, false);
            this.action = action;
            Margin = Padding.Empty;
            Size = new Size(46, 56);
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = action == CaptionAction.Minimize ? "Riduci a icona" : action == CaptionAction.Maximize ? "Ingrandisci" : "Chiudi";
        }

        float K => DeviceDpi / 96f;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float k = K;
            bool close = action == CaptionAction.Close;
            g.Clear(!hover ? Theme.Band
                : close ? (down ? Color.FromArgb(0x94, 0x1E, 0x14) : Color.FromArgb(0xC4, 0x2B, 0x1C))
                : (down ? Color.FromArgb(0x26, 0x26, 0xB3) : Color.FromArgb(0x1A, 0x1A, 0xAA)));
            float s = 5 * k, cx = Width / 2f, cy = Height / 2f;
            g.SmoothingMode = close ? SmoothingMode.AntiAlias : SmoothingMode.None;
            using (var pen = new Pen(Theme.BandText, Math.Max(1, k)))
                switch (action)
                {
                    case CaptionAction.Minimize:
                        g.DrawLine(pen, cx - s, cy, cx + s, cy);
                        break;
                    case CaptionAction.Maximize when FindForm()?.WindowState == FormWindowState.Maximized:
                        g.DrawRectangle(pen, cx - s, cy - s + 2 * k, 2 * s - 2 * k, 2 * s - 2 * k);
                        g.DrawLines(pen, new[] { new PointF(cx - s + 2 * k, cy - s), new PointF(cx + s, cy - s), new PointF(cx + s, cy + s - 2 * k) });
                        break;
                    case CaptionAction.Maximize:
                        g.DrawRectangle(pen, cx - s, cy - s, 2 * s, 2 * s);
                        break;
                    case CaptionAction.Close:
                        g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s);
                        g.DrawLine(pen, cx + s, cy - s, cx - s, cy + s);
                        break;
                }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var form = FindForm();
            if (form == null) return;
            if (action == CaptionAction.Close) form.Close();
            else if (action == CaptionAction.Minimize) form.WindowState = FormWindowState.Minimized;
            else form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    }

    /// <summary>
    /// The URL for GEOlayers as one large button: a real tile of the project as preview, the address with its {z}/{x}/{y}
    /// placeholders picked out in the action blue, and a copy mark. A click (or Enter, or Space) copies it.
    /// </summary>
    sealed class UrlButton : Control
    {
        static readonly Font UrlFont = Fonts.Regular(12.5f), NoteFont = Fonts.Regular(9.25f);
        readonly Timer reset = new Timer { Interval = 2000 };
        string url = "", note = "", copiedNote = "Copiato negli appunti: incollalo in GEOlayers.";
        Image preview;
        bool hover, down, copied;

        public UrlButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Anchor = AnchorStyles.Left | AnchorStyles.Right;
            Margin = new Padding(0, 4, 0, 4);
            Height = 92;
            AccessibleRole = AccessibleRole.PushButton;
            reset.Tick += (s, e) => { reset.Stop(); copied = false; Invalidate(); };
        }

        public string Url
        {
            get => url;
            set { url = value ?? ""; AccessibleName = "Copia l'URL per GEOlayers: " + url; Invalidate(); }
        }

        /// <summary>The line under the URL: what to do with it, or why it is not usable yet.</summary>
        public string Note
        {
            get => note;
            set { note = value ?? ""; AccessibleDescription = note; Invalidate(); }
        }

        /// <summary>The line shown for two seconds after a copy.</summary>
        public string CopiedNote
        {
            get => copiedNote;
            set { copiedNote = value ?? ""; Invalidate(); }
        }

        /// <summary>A tile of the project; the button owns it from now on.</summary>
        public Image Preview
        {
            set { preview?.Dispose(); preview = value; Invalidate(); }
        }

        float K => DeviceDpi / 96f;

        public override Size GetPreferredSize(Size proposed) => new Size(proposed.Width > 1 && proposed.Width < 20000 ? proposed.Width : Width, Height);

        void CopyUrl()
        {
            if (url.Length == 0) return;
            try { Clipboard.SetDataObject(url, true, 5, 100); }
            catch (ExternalException) { return; } // clipboard held by another program: nothing copied, nothing claimed
            copied = true;
            reset.Stop();
            reset.Start();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float k = K;
            g.Clear(down ? Theme.Rule : hover ? Theme.ActionTint : Theme.Panel);
            bool ring = Focused && ShowFocusCues;
            using (var border = new Pen(hover || ring ? Theme.Action : Theme.Rule, ring ? 2 * k : 1))
            {
                int inset = (int)(border.Width / 2);
                g.DrawRectangle(border, inset, inset, Width - 2 * inset - 1, Height - 2 * inset - 1);
            }

            int pad = (int)(14 * k), side = Height - 2 * pad;
            var thumb = new Rectangle(pad, pad, side, side);
            if (preview != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(preview, thumb);
            }
            else
            {
                using (var night = new SolidBrush(Theme.Monitor)) g.FillRectangle(night, thumb);
                using (var grid = new Pen(Theme.MonitorGrid))
                    for (int i = 1; i < 3; i++)
                    {
                        g.DrawLine(grid, thumb.X + side * i / 3, thumb.Y, thumb.X + side * i / 3, thumb.Bottom);
                        g.DrawLine(grid, thumb.X, thumb.Y + side * i / 3, thumb.Right, thumb.Y + side * i / 3);
                    }
            }
            using (var edge = new Pen(Theme.Rule)) g.DrawRectangle(edge, thumb);

            // The copy mark: two squares, the front one filled once the URL is on the clipboard.
            int mark = (int)(16 * k), mx = Width - pad - mark - (int)(6 * k), my = (Height - mark) / 2;
            using (var pen = new Pen(copied ? Theme.Ok : Theme.Action, 1.5f * k))
            {
                g.DrawRectangle(pen, mx + 4 * k, my - 4 * k, mark - 4 * k, mark - 4 * k);
                using (var fill = new SolidBrush(copied ? Theme.Ok : hover ? Theme.ActionTint : Theme.Panel))
                    g.FillRectangle(fill, mx, my, mark - 4 * k, mark - 4 * k);
                g.DrawRectangle(pen, mx, my, mark - 4 * k, mark - 4 * k);
            }

            int x = thumb.Right + (int)(16 * k), right = mx - (int)(16 * k);
            int urlHeight = TextRenderer.MeasureText("Ag", UrlFont).Height, noteHeight = TextRenderer.MeasureText("Ag", NoteFont).Height;
            int y = (Height - urlHeight - noteHeight - (int)(4 * k)) / 2;
            DrawUrl(g, new Rectangle(x, y, right - x, urlHeight));
            TextRenderer.DrawText(g, copied ? copiedNote : note, NoteFont,
                new Rectangle(x, y + urlHeight + (int)(4 * k), right - x, noteHeight), copied ? Theme.Ok : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        /// <summary>The URL with {z}, {x} and {y} in the action blue; a URL too long for the row is shortened with an ellipsis.</summary>
        void DrawUrl(Graphics g, Rectangle box)
        {
            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            var parts = Regex.Split(url, @"(\{[zxy]\})");
            int total = parts.Sum(part => TextRenderer.MeasureText(g, part, UrlFont, Size.Empty, flags).Width);
            if (total > box.Width)
            {
                TextRenderer.DrawText(g, url, UrlFont, box, Theme.Ink, flags | TextFormatFlags.PathEllipsis);
                return;
            }
            int x = box.X;
            foreach (var part in parts.Where(p => p.Length > 0))
            {
                bool token = part.Length == 3 && part[0] == '{';
                TextRenderer.DrawText(g, part, UrlFont, new Point(x, box.Y), token ? Theme.Action : Theme.Ink, flags);
                x += TextRenderer.MeasureText(g, part, UrlFont, Size.Empty, flags).Width;
            }
        }

        protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); CopyUrl(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space) return;
            e.Handled = true;
            CopyUrl();
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData);
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    /// <summary>Builders for the page layout: headings, hints, and rows where one control takes the spare width.</summary>
    static class Ui
    {
        public static readonly Font HeadingFont = Fonts.SemiBold(11.5f);

        public static Label Heading(string text) => new Label
        {
            Text = text, AutoSize = true, Font = HeadingFont, ForeColor = Theme.Ink, Margin = new Padding(0, 20, 0, 6),
        };

        public static Label Hint(string text) => new Label
        {
            Text = text, AutoSize = true, ForeColor = Theme.Muted, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 2, 0, 4),
        };

        public static RadioButton Choice(string text) => new RadioButton
        {
            Text = text, AutoSize = true, ForeColor = Theme.Ink, Margin = new Padding(0, 6, 0, 2),
        };

        /// <summary>A text box; read-only ones show a path or a URL and stay out of the Tab order (their buttons are in it).</summary>
        public static TextBox Field(string accessibleName, bool readOnly = true) => new TextBox
        {
            ReadOnly = readOnly, TabStop = !readOnly, BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Panel, ForeColor = Theme.Ink,
            AccessibleName = accessibleName,
        };

        /// <summary>A row of controls; the one at <paramref name="stretch"/> takes the remaining width.</summary>
        public static TableLayoutPanel Line(int stretch, params Control[] controls)
        {
            var line = new TableLayoutPanel
            {
                ColumnCount = controls.Length, RowCount = 1, AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 2, 0, 2),
            };
            for (int i = 0; i < controls.Length; i++)
            {
                var control = controls[i] is TextBox box ? new FieldHost(box) : controls[i];
                line.ColumnStyles.Add(i == stretch ? new ColumnStyle(SizeType.Percent, 100) : new ColumnStyle(SizeType.AutoSize));
                control.Anchor = i == stretch ? AnchorStyles.Left | AnchorStyles.Right : AnchorStyles.Left;
                line.Controls.Add(control, i, 0);
            }
            return line;
        }

        /// <summary>
        /// A tab page: a vertical stack that scrolls only on small screens, and an optional action bar docked at the bottom
        /// so the primary action never scrolls out of view.
        /// </summary>
        public static Panel Page(Control actionBar, params Control[] rows)
        {
            var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroller.Controls.Add(Stack(DockStyle.Top, new Padding(28, 14, 28, 18), rows));
            var page = new Panel { Dock = DockStyle.Fill, Visible = false };
            page.Controls.Add(scroller);
            if (actionBar != null) page.Controls.Add(actionBar); // added last, docked first: the scroller fills what is left
            return page;
        }

        public static TableLayoutPanel ActionBar(params Control[] rows)
        {
            var bar = Stack(DockStyle.Bottom, new Padding(28, 6, 28, 12), rows);
            bar.Paint += (s, e) => { using (var pen = new Pen(Theme.Rule)) e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0); };
            return bar;
        }

        /// <summary>Rows placed explicitly: a row that starts hidden keeps its place when it appears.</summary>
        static TableLayoutPanel Stack(DockStyle dock, Padding padding, Control[] rows)
        {
            var stack = new TableLayoutPanel { Dock = dock, AutoSize = true, ColumnCount = 1, RowCount = rows.Length, Padding = padding };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rows.Length; i++)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                stack.Controls.Add(rows[i], 0, i);
            }
            return stack;
        }
    }
}
