using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RaiTilePackageManager
{
    /*
     * Direction contract (impeccable)
     * THESIS: the app speaks the TG's on-air language: every state is a sottopancia and the running server is IN ONDA.
     *   It refuses the stacked grey form and the generic dashed-dropzone dashboard.
     * OWN-WORLD: Rai News blue #000099 bands with white Inter Tight, a full-height tab carrying the state as a drawn glyph,
     *   live red #C22C2F only for on air and errors, rai.it blue #0060E6 for actions and focus, a light #F5F7FF desk
     *   under dark on-air elements, square corners everywhere.
     * STORY: the operator drops the package on the program monitor, reads in one strap whether it lines up, converts it
     *   into a dedicated project folder, puts it on air and copies the URL into GEOlayers.
     * FIRST VIEWPORT: blue header band with the Rai logo, the name, three tabs and the window buttons; a navy program monitor
     *   of tiles fills the top of Importa with its lower third "Trascina qui il tile package"; destination choice below;
     *   the primary action in a bar that never scrolls away.
     * FORM: TG sottopancia, candidate 5 of 7 grounded directions; seed 224c4e9a.
     * FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, and DESIGN.md
     */

    /// <summary>The main window: Importa (package to project), In onda (local server), Storage remoto (web server check).</summary>
    sealed class MainForm : Form
    {
        const string GuideUrl = "https://github.com/bitoesposito/rai-tile-package-manager#readme";
        const string NotAProject = "Scegli la cartella creata da questa app: contiene metadata.json e una cartella per ogni zoom.";
        static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rai - Package tile manager", "settings.txt");
        static readonly Font HeaderFont = Fonts.Bold(12.5f);

        readonly TabButton[] tabs = { new TabButton("Importa"), new TabButton("In onda"), new TabButton("Storage remoto") };
        readonly Panel[] pages;
        readonly CaptionPanel header = new CaptionPanel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.Band };
        readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer { Interval = 150 };

        // Importa
        readonly DropZone monitor = new DropZone();
        readonly Strap packageStrap = new Strap();
        readonly RadioButton newChoice = Ui.Choice("Nuovo progetto");
        readonly RadioButton addChoice = Ui.Choice("Aggiungi a un progetto esistente");
        readonly TextBox newPathBox = Ui.Field("Cartella del nuovo progetto");
        readonly TextBox addPathBox = Ui.Field("Progetto a cui aggiungere il pacchetto");
        readonly RaiButton locationButton = new RaiButton("Cambia posizione…", false, "Scegli progetto…");
        readonly RaiButton projectButton = new RaiButton("Scegli progetto…", false, "Cambia posizione…");
        readonly Label newHint = Ui.Hint("L'app crea una cartella apposta per il progetto, con il nome del pacchetto.");
        readonly Label addHint = Ui.Hint("Scegli la cartella di un progetto creato con questa app: le tile del pacchetto si aggiungono a quelle che contiene.");
        readonly Strap destinationStrap = new Strap();
        readonly RadioButton keepChoice = Ui.Choice("Tieni le tile già presenti (consigliato)");
        readonly RadioButton replaceChoice = Ui.Choice("Sostituisci le tile in comune");
        readonly FlowLayoutPanel policy = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(18, 0, 0, 4) };
        readonly ProgressLine progressLine = new ProgressLine { Visible = false };
        readonly Strap resultStrap = new Strap();
        readonly RaiButton openButton = new RaiButton("Apri cartella", false) { Visible = false };
        readonly RaiButton convertButton = new RaiButton("Converti", true, "Aggiungi al progetto", "Metti in onda", "Annulla");
        readonly Control newRow, addRow;

        // In onda
        readonly TextBox liveBox = Ui.Field("Progetto da mettere in onda");
        readonly RaiButton liveButton = new RaiButton("Cambia…", false);
        readonly Label liveHint = Ui.Hint("Scegli la cartella di un progetto. Dopo una conversione l'app propone quella appena creata.");
        readonly Strap liveStrap = new Strap();
        readonly NumericUpDown portBox = new NumericUpDown { Minimum = 1024, Maximum = 65535, Value = 8000, Width = 76, AccessibleName = "Porta" };
        readonly RaiButton serverButton = new RaiButton("Metti in onda", true, "Ferma");
        readonly Strap tally = new Strap();
        readonly UrlButton localUrl = new UrlButton { Margin = new Padding(0, 4, 0, 0) };
        readonly GeoFields localGeo = new GeoFields();

        // Storage remoto
        readonly TextBox remoteBox = Ui.Field("Indirizzo del web server", readOnly: false);
        readonly RaiButton verifyButton = new RaiButton("Verifica connessione", true);
        readonly Strap remoteStrap = new Strap();
        readonly Label remoteUrlHeading = Ui.Heading("Impostazioni per GEOlayers");
        readonly UrlButton remoteUrl = new UrlButton { Note = "Fai clic per copiarlo e incollalo in GEOlayers.", Margin = new Padding(0, 4, 0, 0) };
        readonly GeoFields remoteGeo = new GeoFields();

        TilePackage package;
        int packageTicket, addTicket;    // the latest load or lookup wins
        string location;                 // where new projects go; follows the package until the user picks one
        bool locationChosen;
        string newDir, locationError;    // the dedicated folder inside location, or why it cannot be used
        ProjectInfo addProject;
        string addNote;
        (string Title, string Detail)? addError;
        ProjectInfo liveProject;
        string convertedDir;             // after a conversion the primary action becomes "Metti in onda"
        CancellationTokenSource conversion;
        Task conversionTask;
        ExtractProgress progress;
        long progressTotal;
        TileServer server;
        bool verifying, remoteVerified;

        public MainForm(string[] startPaths)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            BackColor = Theme.Desk;
            ForeColor = Theme.Ink;
            Text = "Rai - Package tile manager";
            using (var icon = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico")) Icon = new Icon(icon);
            ClientSize = new Size(800, 700);
            StartPosition = FormStartPosition.CenterScreen;

            policy.Controls.AddRange(new Control[] { keepChoice, replaceChoice });
            keepChoice.Checked = true;
            newChoice.Checked = true;
            newRow = Ui.Line(0, newPathBox, locationButton);
            addRow = Ui.Line(0, addPathBox, projectButton);
            var portRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            var portField = new FieldHost(portBox) { Width = 88, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 3) };
            portRow.Controls.AddRange(new Control[] { new Label { Text = "Porta", AutoSize = true, ForeColor = Theme.Ink, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) }, portField, serverButton });
            serverButton.Anchor = AnchorStyles.Left;

            pages = new[]
            {
                Ui.Page(Ui.ActionBar(progressLine, Ui.Line(0, resultStrap, openButton, convertButton)),
                    monitor,
                    packageStrap,
                    Ui.Heading("Dove salvare le tile"),
                    newChoice, newRow, newHint,
                    addChoice, addRow, addHint,
                    destinationStrap, policy),
                Ui.Page(null,
                    Ui.Heading("Progetto"),
                    Ui.Line(0, liveBox, liveButton),
                    liveHint, liveStrap,
                    Ui.Heading("Server locale"),
                    portRow, tally,
                    Ui.Heading("Impostazioni per GEOlayers"),
                    localUrl, localGeo,
                    Ui.Hint("In GEOlayers 3 crea una Raster Source di tipo xyz. Il server resta attivo finché questa finestra è aperta.")),
                Ui.Page(null,
                    Ui.Heading("Indirizzo del web server"),
                    Ui.Hint("La cartella del progetto pubblicata sul server, per esempio https://tiles.azienda.it/mappa."),
                    Ui.Line(0, remoteBox, verifyButton),
                    remoteStrap,
                    remoteUrlHeading, remoteUrl, remoteGeo,
                    Ui.Hint("La verifica scarica una tile del progetto scelto nella scheda In onda e la confronta con quella su disco.")),
            };

            // Header band: the window has no Windows title bar; its empty areas drag the window, the buttons sit in the band.
            header.Paint += PaintHeader;
            var captionStrip = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Band, Margin = Padding.Empty };
            captionStrip.Controls.AddRange(new Control[] { new CaptionButton(CaptionAction.Minimize), new CaptionButton(CaptionAction.Maximize), new CaptionButton(CaptionAction.Close) });
            var tabStrip = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Band, Padding = new Padding(0, 0, 18, 0) };
            tabStrip.Controls.AddRange(tabs);
            header.Controls.Add(tabStrip);
            header.Controls.Add(captionStrip); // docked first: the window buttons take the far right
            Resize += (s, e) => captionStrip.Controls[1].Invalidate(); // maximize and restore glyphs

            var guide = new LinkLabel { Text = "Guida e aggiornamenti", AutoSize = true, LinkColor = Theme.Action, ActiveLinkColor = Theme.ActionPressed, LinkBehavior = LinkBehavior.HoverUnderline };
            var version = new Label { Text = "Versione " + typeof(MainForm).Assembly.GetName().Version.ToString(3), AutoSize = true, ForeColor = Theme.Muted };
            var footerLine = Ui.Line(0, version, guide);
            footerLine.Dock = DockStyle.Fill;
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(28, 6, 28, 4) };
            footer.Paint += (s, e) => { using (var pen = new Pen(Theme.Rule)) e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); };
            footer.Controls.Add(footerLine);

            var body = new Panel { Dock = DockStyle.Fill };
            body.Controls.AddRange(pages);
            Controls.Add(body); // docking runs from the last control added: header, then footer, then the body fills the rest
            Controls.Add(footer);
            Controls.Add(header);

            var tips = new ToolTip { AutoPopDelay = 20000 };
            tips.SetToolTip(keepChoice, "Le tile già presenti restano come sono. Un pacchetto di un'area piccola contiene anche gli zoom bassi, quasi vuoti:\n" +
                                        "sostituendoli, a quegli zoom sparirebbe il resto della mappa.");
            tips.SetToolTip(replaceChoice, "Le tile in comune prendono il contenuto del pacchetto. Serve quando hai riesportato la stessa mappa.");
            tips.SetToolTip(portBox, "La porta fa parte dell'URL: http://localhost:porta/{z}/{x}/{y}");
            tips.SetToolTip(monitor, "Trascina un file .tpkx o .tpk, oppure fai clic per sceglierlo.");

            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].Chosen += (s, e) => SelectTab(index);
            }
            monitor.Browse += (s, e) => BrowsePackage();
            newChoice.CheckedChanged += (s, e) => DestinationChanged();
            locationButton.Click += (s, e) => ChooseLocation();
            projectButton.Click += (s, e) => ChooseProject();
            convertButton.Click += (s, e) => Convert();
            openButton.Click += (s, e) => Process.Start("explorer.exe", "\"" + convertedDir + "\"");
            liveButton.Click += (s, e) => ChooseLiveProject();
            serverButton.Click += (s, e) => ToggleServer();
            portBox.ValueChanged += (s, e) => UpdateState();
            verifyButton.Click += (s, e) => VerifyRemote();
            remoteBox.TextChanged += (s, e) =>
            {
                remoteStrap.Clear();
                remoteVerified = remoteUrlHeading.Visible = remoteUrl.Visible = false;
                UpdateState();
            };
            remoteBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                VerifyRemote();
            };
            guide.LinkClicked += (s, e) => Process.Start(GuideUrl);
            ticker.Tick += (s, e) => Tick();
            ticker.Start();
            EnableDrop(this);
            monitor.DragEnter += (s, e) => monitor.Dragging(PackageDrop(e.Data));
            monitor.DragLeave += (s, e) => monitor.Dragging(null);
            monitor.DragDrop += (s, e) => monitor.Dragging(null);

            remoteUrlHeading.Visible = remoteUrl.Visible = false;
            ShowIdleMonitor();
            ShowNewDir();
            tally.ShowTally(false, "Server fermo", "Metti in onda un progetto per usarlo in GEOlayers.");
            var savedProject = LoadSettings();
            ResumeLayout(false);
            PerformLayout();
            SelectTab(0);
            UpdateState();

            Load += (s, e) =>
            {
                var area = Screen.FromControl(this).WorkingArea; // 1366×768 laptops: a shorter window, the page scrolls
                if (Height > area.Height) Height = area.Height;
                MinimumSize = new Size(Width * 4 / 5, Math.Min(Height, 480));
                CenterToScreen();
            };
            Shown += async (s, e) =>
            {
                if (savedProject != null) await SetLiveProject(savedProject, quiet: true);
                foreach (var path in startPaths) OpenPath(path);
            };
        }

        // --- window frame -----------------------------------------------------------------------------------------

        /// <summary>
        /// Removes only the Windows title bar: the side and bottom frame stay, so resizing, snapping, the shadow and the
        /// maximize animation remain the native ones. The header band takes the title bar's place.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_NCCALCSIZE = 0x83, WM_NCHITTEST = 0x84, HTCLIENT = 1, HTCAPTION = 2, HTTOP = 12;
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                var before = (NcCalcSizeParams)Marshal.PtrToStructure(m.LParam, typeof(NcCalcSizeParams));
                base.WndProc(ref m);
                var after = (NcCalcSizeParams)Marshal.PtrToStructure(m.LParam, typeof(NcCalcSizeParams));
                // A maximized window overhangs the screen by its frame: keep the band below the top edge.
                after.Client.Top = before.Client.Top + (WindowState == FormWindowState.Maximized ? GetSystemMetrics(33) + GetSystemMetrics(92) : 0);
                Marshal.StructureToPtr(after, m.LParam, false);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                long lp = m.LParam.ToInt64();
                var point = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                if (WindowState == FormWindowState.Normal && point.Y < (int)(6 * DeviceDpi / 96f)) m.Result = (IntPtr)HTTOP;
                else if (point.Y < header.Height) m.Result = (IntPtr)HTCAPTION;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_FRAMECHANGED = 0x20;
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        void PaintHeader(object sender, PaintEventArgs e)
        {
            float k = DeviceDpi / 96f;
            int logo = (int)(30 * k), x = (int)(24 * k);
            RaiLogo.Draw(e.Graphics, new Rectangle(x, (header.Height - logo) / 2, logo, logo), Theme.BandText);
            TextRenderer.DrawText(e.Graphics, "Package tile manager", HeaderFont, // the logo says "Rai"
                new Rectangle(x + logo + (int)(12 * k), 0, header.Width, header.Height), Theme.BandText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        void SelectTab(int index)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = i == index;
                tabs[i].Selected = i == index;
            }
        }

        int CurrentTab => Array.FindIndex(pages, p => p.Visible);

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.D1: SelectTab(0); return true;
                case Keys.Control | Keys.D2: SelectTab(1); return true;
                case Keys.Control | Keys.D3: SelectTab(2); return true;
                case Keys.Control | Keys.O: BrowsePackage(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // --- Importa: the package -----------------------------------------------------------------------------

        void ShowIdleMonitor() =>
            monitor.Set(Glyph.Drop, Theme.Action, "Trascina qui il tile package",
                "Il file .tpkx o .tpk esportato da ArcGIS Pro. Puoi anche fare clic qui per sceglierlo.");

        void BrowsePackage()
        {
            if (conversion != null) return;
            using (var dialog = new OpenFileDialog
            {
                Title = "Scegli il tile package",
                Filter = "Tile package di ArcGIS Pro (*.tpkx;*.tpk)|*.tpkx;*.tpk",
                InitialDirectory = package != null ? Path.GetDirectoryName(package.FilePath) : location ?? "",
            })
                if (dialog.ShowDialog(this) == DialogResult.OK) LoadPackage(dialog.FileName);
        }

        static bool IsPackage(string path) =>
            path.EndsWith(".tpkx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tpk", StringComparison.OrdinalIgnoreCase);

        /// <summary>For the monitor highlight: true = a package, null = a folder (handled elsewhere), false = anything else.</summary>
        static bool? PackageDrop(IDataObject data)
        {
            if (!(data.GetData(DataFormats.FileDrop) is string[] paths) || paths.Length == 0) return false;
            return IsPackage(paths[0]) ? true : Directory.Exists(paths[0]) ? (bool?)null : false;
        }

        /// <summary>Dropped on the window or passed on the command line: a package, or a project folder.</summary>
        void OpenPath(string path)
        {
            if (conversion != null) return;
            if (IsPackage(path))
                LoadPackage(path);
            else if (!Directory.Exists(path))
                (CurrentTab == 0 ? packageStrap : CurrentTab == 1 ? liveStrap : remoteStrap)
                    .Show(Status.Error, "Questo file non è un tile package", "Trascina un file .tpkx o .tpk esportato da ArcGIS Pro.");
            else if (CurrentTab == 0)
                LookUpAddProject(path, null); // a project dropped on Importa receives the package
            else if (server == null)
                _ = SetLiveProject(path);
        }

        async void LoadPackage(string path)
        {
            if (conversion != null) return;
            int ticket = ++packageTicket;
            SelectTab(0);
            package = null;
            convertedDir = null;
            packageStrap.Clear();
            resultStrap.Clear();
            monitor.Set(Glyph.Info, Theme.Action, Path.GetFileName(path), "Lettura del pacchetto…");
            UpdateState();
            TilePackage opened;
            try
            {
                opened = await Task.Run(() => TilePackage.Open(path));
            }
            catch (Exception e)
            {
                if (ticket != packageTicket) return;
                monitor.Set(Glyph.Error, Theme.Live, Path.GetFileName(path), "Il file non si apre come tile package. Trascinane un altro o fai clic qui.");
                packageStrap.Show(Status.Error, "Impossibile leggere il file", e.Message);
                UpdateState();
                return;
            }
            if (ticket != packageTicket) return;
            package = opened;
            var status = package.Problems.Count > 0 ? Status.Error : package.Warnings.Count > 0 ? Status.Warning : Status.Ok;
            monitor.Set(Sottopancia.GlyphOf(status), Theme.Of(status), package.Name,
                $"{Path.GetFileName(path)} · {package.Format} · zoom {Zooms(package.Levels)} · {FileSize(package.TotalBytes)}\n" +
                (status == Status.Ok ? "Schema Web Mercator standard: in GEOlayers le tile si allineano alla mappa." : "Leggi qui sotto prima di convertire."));
            if (status == Status.Error)
                packageStrap.Show(status, "Questo pacchetto non funziona in GEOlayers", string.Join("\n", package.Problems));
            else if (status == Status.Warning)
                packageStrap.Show(status, "Si può convertire, con un'avvertenza", string.Join("\n", package.Warnings));
            if (!locationChosen) location = Path.GetDirectoryName(path);
            await RefreshNewDir();
            ShowDestination();
            UpdateState();
        }

        // --- Importa: where the tiles go ------------------------------------------------------------------------

        /// <summary>The dedicated folder for a new project, checked off the UI thread (network shares can hang).</summary>
        async Task RefreshNewDir()
        {
            newDir = null;
            locationError = null;
            if (package != null && location != null)
            {
                string loc = location, name = Path.GetFileNameWithoutExtension(package.FilePath);
                try
                {
                    newDir = await Task.Run(() =>
                    {
                        if (!Directory.Exists(loc))
                            throw new DirectoryNotFoundException($"{loc} non è raggiungibile: controlla la connessione di rete, l'unità o i permessi.");
                        return TileFolder.NewProjectPath(loc, name);
                    });
                }
                catch (Exception e)
                {
                    locationError = e.Message;
                }
            }
            ShowNewDir();
        }

        /// <summary>The new project's folder, or a muted line saying when it appears (a read-only box shows no cue banner).</summary>
        void ShowNewDir()
        {
            bool known = newDir != null || location != null;
            newPathBox.Text = newDir ?? location ?? "Compare quando scegli il pacchetto";
            newPathBox.ForeColor = known ? Theme.Ink : Theme.Muted;
        }

        void DestinationChanged()
        {
            convertedDir = null;
            addError = null;
            resultStrap.Clear();
            ShowDestination();
            UpdateState();
        }

        async void ChooseLocation()
        {
            var picked = FolderPicker.Show(this, "Dove creare il nuovo progetto", location);
            if (picked == null) return;
            ProjectInfo existing;
            try
            {
                existing = await Task.Run(() => TileFolder.Find(picked));
            }
            catch (Exception e)
            {
                location = picked;
                locationChosen = true;
                newDir = null;
                locationError = e.Message;
                ShowNewDir();
                ShowDestination();
                UpdateState();
                return;
            }
            if (existing != null)
            {
                // A project inside a project is never what is meant: the package goes into the project picked.
                UseAddProject(existing, "Hai scelto un progetto esistente, quindi il pacchetto verrà aggiunto a questo.", null);
                return;
            }
            location = picked;
            locationChosen = true;
            convertedDir = null;
            await RefreshNewDir();
            ShowDestination();
            UpdateState();
        }

        void ChooseProject()
        {
            var picked = FolderPicker.Show(this, "Scegli il progetto a cui aggiungere il pacchetto", addProject?.Dir ?? location);
            if (picked != null) LookUpAddProject(picked, null);
        }

        async void LookUpAddProject(string dir, string note)
        {
            int ticket = ++addTicket;
            ProjectInfo found = null;
            (string, string)? error = null;
            try
            {
                found = await Task.Run(() => TileFolder.Find(dir));
            }
            catch (Exception e)
            {
                error = ("Cartella non raggiungibile", e.Message);
            }
            if (ticket != addTicket) return; // a newer pick is on its way
            if (found == null && error == null) error = ("Questa cartella non contiene un progetto di tile", dir + "\n" + NotAProject);
            UseAddProject(found, note, error);
        }

        void UseAddProject(ProjectInfo project, string note, (string Title, string Detail)? error)
        {
            addChoice.Checked = true; // first: switching mode clears the previous error
            addProject = project;
            addNote = note;
            addError = error;
            addPathBox.Text = project?.Dir ?? "";
            convertedDir = null;
            ShowDestination();
            UpdateState();
        }

        /// <summary>The only writer of the destination strap: errors and project facts survive any other update.</summary>
        void ShowDestination()
        {
            if (newChoice.Checked)
            {
                if (locationError != null) destinationStrap.Show(Status.Error, "Cartella non raggiungibile", locationError);
                else destinationStrap.Clear();
                return;
            }
            if (addProject == null)
            {
                if (addError != null) destinationStrap.Show(Status.Error, addError.Value.Title, addError.Value.Detail);
                else destinationStrap.Clear();
                return;
            }
            var title = $"{Path.GetFileName(addProject.Dir)}: tile {(addProject.Format ?? "?").ToUpperInvariant()}, zoom {Zooms(addProject.Levels)}";
            var incompatible = package == null ? null : TileFolder.Incompatibility(addProject, package);
            if (incompatible != null)
            {
                destinationStrap.Show(Status.Error, title, incompatible);
                return;
            }
            var lines = new List<string>();
            if (addNote != null) lines.Add(addNote);
            if (addProject.Sources.Count > 0)
                lines.Add("Contiene " + string.Join(", ", addProject.Sources.Select(s => $"{s.File} (zoom {Zooms(s.Levels)})")) + ".");
            if (package != null)
            {
                var added = package.Levels.Except(addProject.Levels).ToArray();
                var common = package.Levels.Intersect(addProject.Levels).ToArray();
                if (added.Length > 0) lines.Add($"Il pacchetto aggiunge gli zoom {Zooms(added)}.");
                if (common.Length > 0) lines.Add($"Gli zoom {Zooms(common)} ci sono già: scegli qui sotto cosa fare delle tile in comune.");
                var previous = addProject.Sources.LastOrDefault(s => string.Equals(s.File, Path.GetFileName(package.FilePath), StringComparison.OrdinalIgnoreCase));
                if (previous != null) lines.Add($"{previous.File} è già stato importato il {Date(previous.Date)}.");
            }
            destinationStrap.Show(Status.Info, title, string.Join("\n", lines));
        }

        // --- Importa: conversion --------------------------------------------------------------------------------

        async void Convert()
        {
            if (conversion != null)
            {
                conversion.Cancel();
                UpdateState();
                return;
            }
            if (convertedDir != null)
            {
                GoLive();
                return;
            }
            var pkg = package;
            bool adding = addChoice.Checked;
            bool overwrite = adding && policy.Visible && replaceChoice.Checked;
            string dir = adding ? addProject.Dir : null;
            convertButton.Enabled = false;
            resultStrap.Clear();
            try
            {
                if (!adding)
                {
                    string loc = location, name = Path.GetFileNameWithoutExtension(pkg.FilePath);
                    dir = await Task.Run(() => TileFolder.NewProjectPath(loc, name)); // again, right before writing
                }
                await Task.Run(() => TileFolder.CheckWritable(dir));
            }
            catch (Exception e)
            {
                resultStrap.Show(Status.Error, "Non riesco a scrivere nella cartella", e.Message +
                    (dir != null && dir.StartsWith(@"\\") ? " Se è una cartella di rete, controlla la connessione e i permessi." : ""));
                UpdateState();
                return;
            }

            conversion = new CancellationTokenSource();
            progress = new ExtractProgress();
            progressTotal = Math.Max(1, pkg.TotalBytes);
            var token = conversion.Token;
            var watch = Stopwatch.StartNew();
            progressLine.Value = 0;
            UpdateState();
            Tick();
            try
            {
                await (conversionTask = Task.Run(() =>
                {
                    pkg.Extract(dir, overwrite, progress, token);
                    TileFolder.Record(dir, pkg);
                }));
                convertedDir = dir;
                if (!adding) newPathBox.Text = dir;
                resultStrap.Show(Status.Ok, $"{progress.Written:N0} tile pronte in {watch.Elapsed.TotalSeconds:0.0} s",
                    dir + (progress.Skipped > 0 ? $"\n{progress.Skipped:N0} tile erano già nel progetto e sono rimaste com'erano." : ""));
                if (adding) addProject = await Task.Run(() => TileFolder.Open(dir));
                if (server == null) await SetLiveProject(dir, quiet: true);
            }
            catch (OperationCanceledException)
            {
                resultStrap.Show(Status.Warning, "Conversione annullata", $"Le {progress.Written:N0} tile già scritte restano in {dir}.");
            }
            catch (Exception e)
            {
                var error = e is AggregateException a ? a.Flatten().InnerExceptions[0] : e;
                resultStrap.Show(Status.Error, "Conversione interrotta", error.Message);
            }
            finally
            {
                conversion.Dispose();
                conversion = null;
                ShowDestination();
                UpdateState();
            }
        }

        /// <summary>Puts the project just converted on air, replacing the one on air if it is another (same port, same URL).</summary>
        async void GoLive()
        {
            SelectTab(1);
            if (liveProject?.Dir != convertedDir)
            {
                if (server != null) ToggleServer(); // off air first: if the switch fails, the tally must not say IN ONDA
                await SetLiveProject(convertedDir, quiet: true);
            }
            if (server == null && liveProject != null && liveProject.Dir == convertedDir) ToggleServer();
        }

        // --- In onda --------------------------------------------------------------------------------------------

        void ChooseLiveProject()
        {
            var picked = FolderPicker.Show(this, "Scegli il progetto da mettere in onda", liveProject?.Dir ?? location);
            if (picked != null) _ = SetLiveProject(picked);
        }

        /// <summary>Quiet = remembered or just converted: a folder that is gone is simply not offered.</summary>
        async Task SetLiveProject(string dir, bool quiet = false)
        {
            ProjectInfo found;
            Image preview = null;
            try
            {
                found = await Task.Run(() => TileFolder.Find(dir));
                if (found != null) preview = await Task.Run(() => LoadPreview(found.Dir));
            }
            catch (Exception e)
            {
                if (!quiet) liveStrap.Show(Status.Error, "Cartella non raggiungibile", e.Message);
                return;
            }
            if (found == null)
            {
                if (!quiet) liveStrap.Show(Status.Error, "Questa cartella non contiene un progetto di tile", dir + "\n" + NotAProject);
                return;
            }
            liveProject = found;
            liveBox.Text = found.Dir;
            localUrl.Preview = preview;
            remoteUrl.Preview = preview == null ? null : new Bitmap(preview);
            var sources = found.Sources.Count == 0 ? "senza storico dei pacchetti"
                : found.Sources.Count == 1 ? "1 pacchetto importato" : $"{found.Sources.Count} pacchetti importati";
            liveStrap.Show(Status.Ok, Path.GetFileName(found.Dir), $"Tile {(found.Format ?? "?").ToUpperInvariant()} · zoom {Zooms(found.Levels)} · {sources}");
            if (remoteBox.Text.Trim().Length == 0 && found.RemoteUrl != null) remoteBox.Text = found.RemoteUrl;
            UpdateState();
        }

        /// <summary>The tile shown in the URL buttons, read into memory so the file is never locked.</summary>
        static Image LoadPreview(string dir)
        {
            var sample = TileFolder.SampleTile(dir);
            if (sample == null) return null;
            try
            {
                using (var image = Image.FromStream(new MemoryStream(File.ReadAllBytes(sample.Value.File))))
                    return new Bitmap(image);
            }
            catch (ArgumentException)
            {
                return null; // not a readable image: the button shows the tile grid instead
            }
        }

        void ToggleServer()
        {
            if (server != null)
            {
                server.Dispose();
                server = null;
                tally.ShowTally(false, "Server fermo", "Metti in onda un progetto per usarlo in GEOlayers.");
            }
            else if (liveProject != null)
            {
                try
                {
                    server = new TileServer(liveProject.Dir, (int)portBox.Value);
                    Tick();
                }
                catch (SocketException)
                {
                    tally.Show(Status.Error, $"La porta {portBox.Value} è occupata",
                        $"Un altro programma la sta usando oppure Windows l'ha riservata. Prova con {portBox.Value + 1}.");
                }
            }
            UpdateState();
        }

        // --- Storage remoto -------------------------------------------------------------------------------------

        async void VerifyRemote()
        {
            if (verifying) return;
            var input = remoteBox.Text.Trim();
            string template;
            try
            {
                template = RemoteSource.Template(input, liveProject?.Format ?? "png");
            }
            catch (FormatException e)
            {
                remoteStrap.Show(Status.Error, "Indirizzo non valido", e.Message);
                return;
            }
            var dir = liveProject?.Dir;
            verifying = true;
            remoteStrap.Show(Status.Info, "Verifica in corso…", input);
            UpdateState();
            (Status Status, string Title, string Detail) result;
            try
            {
                result = await Task.Run(() => RemoteSource.VerifyAsync(template, dir));
            }
            catch (Exception e)
            {
                result = (Status.Error, "Verifica non riuscita", e.Message);
            }
            verifying = false;
            if (remoteBox.Text.Trim() == input)
            {
                remoteStrap.Show(result.Status, result.Title, result.Detail);
                remoteUrl.Url = template;
                remoteVerified = remoteUrlHeading.Visible = remoteUrl.Visible = result.Status != Status.Error;
                if (result.Status != Status.Error && dir != null)
                    _ = Task.Run(() =>
                    {
                        try { TileFolder.SetRemoteUrl(dir, input); }
                        catch (Exception) { } // read-only share: the address is just not remembered
                    });
            }
            UpdateState();
        }

        // --- state ----------------------------------------------------------------------------------------------

        void UpdateState()
        {
            bool busy = conversion != null;
            newRow.Visible = newHint.Visible = newChoice.Checked;
            addRow.Visible = addChoice.Checked;
            addHint.Visible = addChoice.Checked && addProject == null && addError == null;
            var incompatible = addChoice.Checked && addProject != null && package != null ? TileFolder.Incompatibility(addProject, package) : null;
            policy.Visible = addChoice.Checked && addProject != null && package != null && incompatible == null && package.Levels.Intersect(addProject.Levels).Any();
            bool ready = package != null && package.Problems.Count == 0 &&
                         (addChoice.Checked ? addProject != null && incompatible == null : newDir != null && locationError == null);
            convertButton.Text = busy ? "Annulla" : convertedDir != null ? "Metti in onda" : addChoice.Checked ? "Aggiungi al progetto" : "Converti";
            convertButton.Enabled = busy ? !conversion.IsCancellationRequested : convertedDir != null || ready;
            openButton.Visible = convertedDir != null && !busy;
            progressLine.Visible = busy;
            foreach (var control in new Control[] { monitor, newChoice, addChoice, locationButton, projectButton, policy })
                control.Enabled = !busy;

            liveButton.Enabled = server == null;
            liveHint.Visible = liveProject == null;
            serverButton.Text = server != null ? "Ferma" : "Metti in onda";
            serverButton.Primary = server == null;
            serverButton.Enabled = server != null || liveProject != null;
            portBox.Enabled = server == null;
            tabs[1].OnAir = server != null;
            localUrl.Url = $"http://localhost:{portBox.Value}/{{z}}/{{x}}/{{y}}.{liveProject?.Format ?? "png"}";
            localGeo.SetProject(liveProject);
            remoteGeo.SetProject(liveProject);
            localGeo.Visible = liveProject != null;
            remoteGeo.Visible = remoteVerified && liveProject != null; // Visible's getter is false on a hidden tab
            localUrl.Note = server != null ? "Fai clic per copiarlo e incollalo in GEOlayers."
                : "Server fermo: mettilo in onda prima di usare l'URL.";
            localUrl.CopiedNote = server != null ? "Copiato negli appunti: incollalo in GEOlayers."
                : "Copiato. Prima di usarlo in GEOlayers metti in onda il progetto.";
            verifyButton.Enabled = !verifying && remoteBox.Text.Trim().Length > 0;
        }

        void Tick()
        {
            if (conversion != null)
            {
                double done = Math.Min(1, Interlocked.Read(ref progress.Bytes) / (double)progressTotal);
                progressLine.Value = (float)done;
                resultStrap.Show(Status.Info, conversion.IsCancellationRequested ? "Annullamento…" : "Conversione in corso",
                    $"{progress.Written + progress.Skipped:N0} tile, {done:P0}");
            }
            if (server != null)
                tally.ShowTally(true, "GEOlayers può caricare le tile da questo PC",
                    (server.Requests == 1 ? "1 richiesta" : $"{server.Requests:N0} richieste") +
                    (server.NotFound == 0 ? "."
                        : (server.NotFound == 1 ? ", 1 tile non trovata" : $", {server.NotFound:N0} tile non trovate") +
                          ": di solito sono zoom o aree che il progetto non contiene."));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if ((conversion != null && !Confirm("La conversione è in corso. Vuoi interromperla e chiudere?")) ||
                (server != null && !Confirm("Il progetto è in onda: chiudendo, GEOlayers non potrà più caricare le tile. Vuoi chiudere?")))
            {
                e.Cancel = true;
                return;
            }
            conversion?.Cancel();
            try { conversionTask?.Wait(3000); } // let the tile being written finish
            catch (Exception) { }
            server?.Dispose();
            SaveSettings();
            base.OnFormClosing(e);
        }

        bool Confirm(string question) =>
            MessageBox.Show(this, question, "Rai - Package tile manager", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        /// <summary>The last project on air and the port, so the next day the server is one click away.</summary>
        string LoadSettings()
        {
            string project = null;
            try
            {
                foreach (var line in File.ReadAllLines(SettingsFile))
                {
                    var kv = line.Split(new[] { '=' }, 2);
                    if (kv.Length != 2) continue;
                    if (kv[0] == "project" && kv[1].Length > 0) project = kv[1];
                    else if (kv[0] == "port" && int.TryParse(kv[1], out var port) && port >= portBox.Minimum && port <= portBox.Maximum) portBox.Value = port;
                }
            }
            catch (Exception) { } // first run: defaults
            return project;
        }

        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                File.WriteAllLines(SettingsFile, new[] { "project=" + liveProject?.Dir, "port=" + portBox.Value });
            }
            catch (Exception) { } // not worth bothering the user
        }

        // --- helpers --------------------------------------------------------------------------------------------

        void EnableDrop(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += (s, e) =>
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) && conversion == null ? DragDropEffects.Copy : DragDropEffects.None;
            control.DragDrop += (s, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
                    foreach (var path in paths) OpenPath(path); // a package and a project folder can arrive together
            };
            foreach (Control child in control.Controls) EnableDrop(child);
        }

        static string Zooms(int[] levels)
        {
            if (levels == null || levels.Length == 0) return "nessuno";
            var parts = new List<string>();
            for (int i = 0; i < levels.Length; i++)
            {
                int first = levels[i];
                while (i + 1 < levels.Length && levels[i + 1] == levels[i] + 1) i++;
                parts.Add(first == levels[i] ? first.ToString() : $"{first}-{levels[i]}");
            }
            return string.Join(", ", parts);
        }

        static string FileSize(long bytes) =>
            bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.0} GB" : $"{bytes / (double)(1L << 20):0.0} MB";

        static string Date(string iso) =>
            DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("g") : iso;

        [StructLayout(LayoutKind.Sequential)]
        struct Rect { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct NcCalcSizeParams { public Rect Client, Before, Source; public IntPtr Position; }

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
