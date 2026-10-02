using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RaiTilePackageManager
{
    /// <summary>The map preview window: the project's tiles to pan and zoom, read straight from the folder.</summary>
    sealed class MapPreview : BandForm
    {
        readonly MapView map = new MapView { Dock = DockStyle.Fill };
        readonly Label status = new Label { AutoSize = false, AutoEllipsis = true, Height = 20, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft };
        readonly RaiButton fit = new RaiButton("Adatta all'area", false) { Enabled = false };

        public MapPreview(string dir) : base("Anteprima mappa", Path.GetFileName(dir))
        {
            SuspendLayout();
            Text = "Anteprima mappa - " + Path.GetFileName(dir);
            ClientSize = new Size(1100, 760);
            StartPosition = FormStartPosition.CenterScreen;
            var line = Ui.Line(0, status, fit);
            line.Dock = DockStyle.Fill;
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(20, 6, 20, 6) };
            footer.Paint += (s, e) => { using (var pen = new Pen(Theme.Rule)) e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); };
            footer.Controls.Add(line);
            Controls.Add(map); // docking runs from the last control added: header, then footer, then the map fills the rest
            Controls.Add(footer);
            Controls.Add(Header);
            ResumeLayout(false);
            PerformLayout();

            status.Text = "Lettura del progetto…";
            map.StatusChanged += (s, e) => status.Text = map.Status;
            fit.Click += (s, e) => map.Fit();
            Load += (s, e) =>
            {
                var area = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                MinimumSize = new Size(Width / 2, Height / 2);
                CenterToScreen();
            };
            Shown += async (s, e) =>
            {
                map.Focus();
                try
                {
                    // Read afresh, so packages added since the main window opened the project show up.
                    var (project, sample) = await Task.Run(() =>
                    {
                        var p = TileFolder.Open(dir);
                        return (p, p != null && p.Bounds == null ? TileFolder.SampleTile(dir) : null);
                    });
                    if (project == null)
                    {
                        status.Text = "Questa cartella non contiene più un progetto di tile.";
                        return;
                    }
                    map.ShowProject(project, sample);
                    fit.Enabled = true;
                }
                catch (Exception error) when (!(error is OutOfMemoryException))
                {
                    status.Text = error.Message;
                }
            };
        }
    }

    /// <summary>
    /// The project's tiles at their real size, panned with the mouse and zoomed one level at a time; missing tiles are
    /// hatched. The frame in the middle is what a Full HD comp takes in at the zoom on its label, halved one level at a
    /// time until it fits the window.
    /// </summary>
    sealed class MapView : Control
    {
        const int TileSize = WebMercator.TileSize;
        const int CacheLimit = 384; // decoded tiles kept, about 100 MB
        const TextFormatFlags LabelFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        static readonly Font LabelFont = Fonts.SemiBold(9.75f);
        static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

        sealed class Cached { public Bitmap Image; public long Used; } // no image: the project has no such tile
        sealed class View { public int Z, X0, Y0, X1, Y1; }

        readonly Dictionary<(int Z, int X, int Y), Cached> cache = new Dictionary<(int, int, int), Cached>();
        readonly HashSet<(int Z, int X, int Y)> loading = new HashSet<(int, int, int)>();
        volatile View wanted; // the tiles of the last paint: a load that starts after its tile scrolled away is skipped
        string dir, extension;
        int[] levels = new int[0];
        double[] bounds;
        (int Z, int X, int Y, string File)? sample;
        int min, max, zoom, wheel;
        double cx, cy; // the view's centre in world pixels at the current zoom
        Point? drag, pointer;
        long stamp;

        public MapView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Theme.Monitor;
            AccessibleRole = AccessibleRole.Graphic;
            Text = AccessibleName = "Anteprima della mappa"; // UI Automation reads the window text
        }

        /// <summary>The zoom, and the position and tile under the mouse, for the line under the map.</summary>
        public string Status { get; private set; } = "";
        public event EventHandler StatusChanged;

        /// <summary><paramref name="fallback"/>: a tile to centre on when the project has no bounds recorded.</summary>
        public void ShowProject(ProjectInfo project, (int Z, int X, int Y, string File)? fallback)
        {
            dir = project.Dir;
            extension = project.Format ?? "png";
            levels = project.Levels ?? new int[0];
            bounds = project.Bounds;
            sample = fallback;
            min = levels.Length > 0 ? levels.Min() : 0;
            max = levels.Length > 0 ? levels.Max() : 0;
            Fit();
        }

        /// <summary>Back to the project's extent, at the deepest zoom that shows all of it.</summary>
        public void Fit()
        {
            if (dir == null) return;
            if (bounds != null)
            {
                zoom = WebMercator.FitZoom(bounds, Width * 9 / 10, Height * 9 / 10, min, max);
                cx = (WebMercator.X(bounds[0], zoom) + WebMercator.X(bounds[2], zoom)) / 2;
                cy = (WebMercator.Y(bounds[1], zoom) + WebMercator.Y(bounds[3], zoom)) / 2;
            }
            else if (sample.HasValue)
            {
                zoom = sample.Value.Z;
                cx = (sample.Value.X + 0.5) * TileSize;
                cy = (sample.Value.Y + 0.5) * TileSize;
            }
            else
            {
                zoom = min;
                cx = cy = WebMercator.WorldSize(zoom) / 2;
            }
            Changed();
        }

        float K => DeviceDpi / 96f;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Monitor);
            if (dir == null) return;
            int n = 1 << zoom;
            long ox = (long)Math.Round(Width / 2.0 - cx), oy = (long)Math.Round(Height / 2.0 - cy); // the world's corner on screen
            int x0 = (int)Math.Max(0, FloorDiv(-ox, TileSize)), x1 = (int)Math.Min(n - 1, FloorDiv(Width - 1 - ox, TileSize));
            int y0 = (int)Math.Max(0, FloorDiv(-oy, TileSize)), y1 = (int)Math.Min(n - 1, FloorDiv(Height - 1 - oy, TileSize));
            wanted = new View { Z = zoom, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 };
            stamp++;
            bool missing = false;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; // tiles pixel for pixel; an enlarged ancestor stays sharp
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (var hatch = Hatch())
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var r = new Rectangle((int)(ox + (long)x * TileSize), (int)(oy + (long)y * TileSize), TileSize, TileSize);
                        if (cache.TryGetValue((zoom, x, y), out var tile))
                        {
                            tile.Used = stamp;
                            if (tile.Image != null) g.DrawImage(tile.Image, r);
                            else
                            {
                                g.FillRectangle(hatch, r);
                                missing = true;
                            }
                        }
                        else
                        {
                            DrawAncestor(g, r, x, y);
                            Load(zoom, x, y);
                        }
                    }
            g.InterpolationMode = InterpolationMode.Default;
            g.PixelOffsetMode = PixelOffsetMode.Default;
            DrawFrame(g);
            if (missing) DrawLegend(g);
        }

        static long FloorDiv(long a, int b) => (long)Math.Floor(a / (double)b);

        static HatchBrush Hatch() => new HatchBrush(HatchStyle.WideUpwardDiagonal, Theme.MonitorGrid, Theme.Monitor);

        /// <summary>While a tile loads, the nearest ancestor already in memory stands in for it, enlarged.</summary>
        void DrawAncestor(Graphics g, Rectangle r, int x, int y)
        {
            for (int d = 1; d <= 4 && zoom - d >= 0; d++)
            {
                if (!cache.TryGetValue((zoom - d, x >> d, y >> d), out var parent) || parent.Image == null) continue;
                parent.Used = stamp;
                int part = TileSize >> d, mask = (1 << d) - 1;
                g.DrawImage(parent.Image, r, new Rectangle((x & mask) * part, (y & mask) * part, part, part), GraphicsUnit.Pixel);
                return;
            }
        }

        void Load(int z, int x, int y)
        {
            var key = (z, x, y);
            if (!loading.Add(key)) return;
            var file = Path.Combine(dir, z.ToString(CultureInfo.InvariantCulture), x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture) + "." + extension);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var view = wanted;
                Bitmap image = null;
                bool known = false; // read, or surely not there
                if (view.Z == z && x >= view.X0 && x <= view.X1 && y >= view.Y0 && y <= view.Y1)
                    try
                    {
                        image = Read(file);
                        known = true;
                    }
                    catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException || e is ArgumentException || e is OutOfMemoryException)
                    {
                        known = true; // no tile, or not an image (GDI+ reports some as out of memory): hatched
                    }
                    catch (Exception) { } // a network share that hiccups: asked again at the next paint
                try
                {
                    BeginInvoke((Action)(() => Loaded(key, image, known)));
                }
                catch (InvalidOperationException)
                {
                    image?.Dispose(); // the window is gone
                }
            });
        }

        void Loaded((int Z, int X, int Y) key, Bitmap image, bool known)
        {
            loading.Remove(key);
            if (!known) return;
            cache[key] = new Cached { Image = image, Used = stamp };
            if (cache.Count > CacheLimit)
                foreach (var old in cache.OrderBy(c => c.Value.Used).Take(cache.Count - CacheLimit * 3 / 4).ToList())
                {
                    old.Value.Image?.Dispose();
                    cache.Remove(old.Key);
                }
            if (key.Z == zoom) Invalidate();
        }

        /// <summary>The tile decoded once into a premultiplied bitmap, the fastest to draw; the file is not kept open.</summary>
        static Bitmap Read(string file)
        {
            using (var stream = new MemoryStream(File.ReadAllBytes(file)))
            using (var source = Image.FromStream(stream))
            {
                var image = new Bitmap(TileSize, TileSize, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(image)) g.DrawImage(source, 0, 0, TileSize, TileSize);
                return image;
            }
        }

        void ClearCache()
        {
            foreach (var tile in cache.Values) tile.Image?.Dispose();
            cache.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ClearCache();
            base.Dispose(disposing);
        }

        /// <summary>The comp frame and its label: the zoom a Full HD and a 4K comp need to take in that area, and its width.</summary>
        void DrawFrame(Graphics g)
        {
            float k = K;
            int pad = (int)(6 * k), margin = (int)(16 * k), room = (int)(40 * k); // room above the frame for its label
            int steps = WebMercator.FrameSteps(1920, 1080, Width - 2 * margin, Height - 2 * room);
            int w = 1920 >> steps, h = 1080 >> steps, fullHd = zoom + steps;
            if (w < 8 * k) return;
            var frame = new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);
            using (var shade = new Pen(Theme.Monitor, 4 * k)) g.DrawRectangle(shade, frame);
            using (var edge = new Pen(Has(fullHd) ? Theme.BandText : Theme.Warn, 2 * k)) g.DrawRectangle(edge, frame);

            double metres = w * WebMercator.MetresPerPixel(WebMercator.Lat(cy, zoom), zoom);
            var label = $"Comp Full HD: zoom {fullHd}{Missing(fullHd)} · 4K: zoom {fullHd + 1}{Missing(fullHd + 1)} · {Distance(metres)}";
            var size = TextRenderer.MeasureText(g, label, LabelFont, Size.Empty, LabelFlags);
            var box = new Rectangle(frame.X - (int)(2 * k), frame.Y - (int)(2 * k) - size.Height - 2 * pad, size.Width + 2 * pad, size.Height + 2 * pad);
            using (var band = new SolidBrush(Theme.Band)) g.FillRectangle(band, box);
            TextRenderer.DrawText(g, label, LabelFont, new Point(box.X + pad, box.Y + pad), Theme.BandText, LabelFlags);
        }

        bool Has(int z) => Array.IndexOf(levels, z) >= 0;
        string Missing(int z) => Has(z) ? "" : " (manca)";

        static string Distance(double metres) =>
            metres >= 10000 ? (metres / 1000).ToString("#,0", Italian) + " km"
            : metres >= 1000 ? (metres / 1000).ToString("0.0", Italian) + " km"
            : metres.ToString("0", Italian) + " m";

        /// <summary>What the hatching means, shown only while some is on screen.</summary>
        void DrawLegend(Graphics g)
        {
            float k = K;
            const string text = "Nessuna tile a questo zoom";
            int pad = (int)(6 * k), margin = (int)(16 * k), swatch = (int)(14 * k);
            var size = TextRenderer.MeasureText(g, text, LabelFont, Size.Empty, LabelFlags);
            var box = new Rectangle(margin, Height - margin - size.Height - 2 * pad, swatch + 3 * pad + size.Width, size.Height + 2 * pad);
            using (var band = new SolidBrush(Theme.Band)) g.FillRectangle(band, box);
            var mark = new Rectangle(box.X + pad, box.Y + (box.Height - swatch) / 2, swatch, swatch);
            using (var hatch = Hatch()) g.FillRectangle(hatch, mark);
            using (var edge = new Pen(Theme.BandSoft)) g.DrawRectangle(edge, mark);
            TextRenderer.DrawText(g, text, LabelFont, new Point(mark.Right + 2 * pad, box.Y + pad), Theme.BandText, LabelFlags);
        }

        void ZoomAt(int z, Point p)
        {
            z = Math.Max(min, Math.Min(max, z));
            if (z == zoom || dir == null) return;
            double f = Math.Pow(2, z - zoom), dx = p.X - Width / 2.0, dy = p.Y - Height / 2.0;
            cx = (cx + dx) * f - dx; // the point under the mouse stays put
            cy = (cy + dy) * f - dy;
            zoom = z;
            Changed();
        }

        void Changed()
        {
            double world = WebMercator.WorldSize(zoom);
            cx = Math.Max(0, Math.Min(world, cx));
            cy = Math.Max(0, Math.Min(world, cy));
            Invalidate();
            UpdateStatus();
        }

        void UpdateStatus()
        {
            if (dir == null) return;
            var text = $"Zoom {zoom} · progetto: zoom {Ui.Zooms(levels)}";
            double wx = 0, wy = 0, world = WebMercator.WorldSize(zoom);
            if (pointer is Point p) { wx = cx + p.X - Width / 2.0; wy = cy + p.Y - Height / 2.0; }
            if (pointer == null)
                text += " · trascina per spostarti, rotella o tasti + e − per lo zoom";
            else if (wx >= 0 && wy >= 0 && wx < world && wy < world)
                text += string.Format(CultureInfo.InvariantCulture, " · lat {0:0.00000}°, lon {1:0.00000}° · tile {2}/{3}/{4}",
                    WebMercator.Lat(wy, zoom), WebMercator.Lon(wx, zoom), zoom, (int)(wx / TileSize), (int)(wy / TileSize));
            if (text == Status) return;
            Status = AccessibleDescription = text;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left) return;
            drag = e.Location;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            pointer = e.Location;
            if (drag is Point from)
            {
                cx -= e.X - from.X;
                cy -= e.Y - from.Y;
                drag = e.Location;
                Changed();
            }
            else UpdateStatus();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            drag = null;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            pointer = null;
            UpdateStatus();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            wheel += e.Delta;
            for (; Math.Abs(wheel) >= 120; wheel -= 120 * Math.Sign(wheel)) // a notch, or the same amount from a touchpad
                ZoomAt(zoom + Math.Sign(wheel), e.Location);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) ZoomAt(zoom + 1, e.Location);
        }

        protected override bool IsInputKey(Keys key) =>
            key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(key);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            var centre = new Point(Width / 2, Height / 2);
            const int step = TileSize / 2;
            switch (e.KeyCode)
            {
                case Keys.Add: case Keys.Oemplus: ZoomAt(zoom + 1, centre); break;
                case Keys.Subtract: case Keys.OemMinus: ZoomAt(zoom - 1, centre); break;
                case Keys.Left: cx -= step; Changed(); break;
                case Keys.Right: cx += step; Changed(); break;
                case Keys.Up: cy -= step; Changed(); break;
                case Keys.Down: cy += step; Changed(); break;
                case Keys.Home: Fit(); break;
                default: return;
            }
            e.Handled = true;
        }
    }
}
