using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using RaiTilePackageManager;

// Builds synthetic tile packages, converts them and checks the output, the folder rules, the merge and the server.
static class Tests
{
    static int failures;

    static int Main()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "rtpm-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            Conversion(tmp);
            Folders(tmp);
            Merge(tmp);
            Rejections(tmp);
            Server(tmp);
            Geometry();
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
        Console.WriteLine(failures == 0 ? "\nTutti i controlli superati." : $"\n{failures} controlli FALLITI.");
        return failures == 0 ? 0 : 1;
    }

    static void Conversion(string tmp)
    {
        // z17 bundle names need 5 hex digits (R1ff80C0ff00): the old script read only 4.
        var pkg = TilePackage.Open(Package(tmp, "a.tpkx", "PNG32", 102100, 256, new[] { 0.0, 0, 10, 10 },
            T(0, 0, 0, "A0"), T(1, 1, 0, "A1"), T(1, 0, 1, "A2"), T(17, 0x1ff82, 0x0ff03, "A3")));
        Check(Near(pkg.Bounds, 0, 0, 10, 10), "extent di root.json convertita in gradi");
        Check(pkg.Problems.Count == 0 && pkg.Warnings.Count == 0, "pacchetto valido senza avvisi");
        Check(pkg.Extension == "png" && pkg.Name == "Test", "formato e nome da root.json");
        Check(pkg.Levels.SequenceEqual(new[] { 0, 1, 17 }), "livelli 0, 1, 17");

        var dir = Path.Combine(tmp, "out");
        var progress = Extract(pkg, dir, false);
        Check(progress.Written == 4 && progress.Skipped == 0, "4 tile scritte");
        Check(progress.Bytes == pkg.TotalBytes, "avanzamento al 100% dei byte");
        Check(Tile(dir, 0, 0, 0) == "A0" && Tile(dir, 1, 1, 0) == "A1" && Tile(dir, 1, 0, 1) == "A2", "z/x/y: x = colonna, y = riga");
        Check(Tile(dir, 17, 0x1ff82, 0x0ff03) == "A3", "bundle con 5 cifre esadecimali");

        TileFolder.Record(dir, pkg);
        var project = TileFolder.Open(dir);
        Check(project.Format == "png" && project.Levels.SequenceEqual(new[] { 0, 1, 17 }), "metadata.json letto");
        Check(project.Sources.Count == 1 && project.Sources[0].File == "a.tpkx", "storico dei pacchetti");
        Check(Near(project.Bounds, 0, 0, 10, 10), "bounds del progetto");
    }

    static void Folders(string tmp)
    {
        // A Desktop: files and unrelated folders. It is never a project and tiles never land loose in it.
        var desktop = Path.Combine(tmp, "Desktop");
        Directory.CreateDirectory(Path.Combine(desktop, "Lavori"));
        Directory.CreateDirectory(Path.Combine(desktop, "2"));
        File.WriteAllText(Path.Combine(desktop, "appunti.txt"), "x");
        Check(TileFolder.Open(desktop) == null && TileFolder.Find(desktop) == null, "il Desktop non è un progetto");
        Check(TileFolder.NewProjectPath(desktop, "mappa") == Path.Combine(desktop, "mappa"), "nuovo progetto in una cartella dedicata");

        Directory.CreateDirectory(Path.Combine(desktop, "mappa", "0"));
        Check(TileFolder.NewProjectPath(desktop, "mappa") == Path.Combine(desktop, "mappa (2)"), "nome già usato: mappa (2)");
        Directory.CreateDirectory(Path.Combine(desktop, "vuota"));
        Check(TileFolder.NewProjectPath(desktop, "vuota") == Path.Combine(desktop, "vuota"), "cartella vuota con lo stesso nome: riusata");
        Check(TileFolder.NewProjectPath(desktop, "a:b?") == Path.Combine(desktop, "ab"), "caratteri non validi tolti dal nome");

        var dir = Path.Combine(tmp, "out");
        Check(TileFolder.Find(Path.Combine(dir, "17", (0x1ff82).ToString())).Dir == dir, "cartella {z}/{x} dentro un progetto: trova il progetto");
        Check(TileFolder.Open(Path.Combine(tmp, "nuova")) == null, "cartella inesistente: nessun progetto");

        // Folders made by the old explode.py have no metadata.json: format and levels come from the files.
        var legacy = Path.Combine(tmp, "legacy");
        Directory.CreateDirectory(Path.Combine(legacy, "5", "17"));
        File.WriteAllText(Path.Combine(legacy, "5", "17", "11.jpg"), "x");
        File.WriteAllText(Path.Combine(legacy, "Thumbs.db"), "x");
        var info = TileFolder.Open(legacy);
        Check(info != null && info.Format == "jpg" && info.Levels.SequenceEqual(new[] { 5 }) && info.Sources.Count == 0, "cartella del vecchio script riconosciuta");
        Check(info.Bounds == null, "cartella del vecchio script: bounds sconosciuti");
        Check(TileFolder.SampleTile(legacy).Value.X == 17, "tile campione z/x/y");
    }

    static void Merge(string tmp)
    {
        var dir = Path.Combine(tmp, "out");
        // An ArcGIS Pro package of a small area at higher zoom also carries its own z0 tile: it must not replace ours.
        var detail = TilePackage.Open(Package(tmp, "b.tpkx", "PNG", 3857, 256, new[] { 5.0, -5, 20, 8 }, T(0, 0, 0, "B0"), T(2, 3, 3, "B2")));
        Check(TileFolder.Incompatibility(TileFolder.Open(dir), detail) == null, "stesso formato: si può aggiungere");

        var keep = Extract(detail, dir, false);
        Check(keep.Written == 1 && keep.Skipped == 1, "aggiunta: 1 nuova, 1 tenuta");
        Check(Tile(dir, 0, 0, 0) == "A0" && Tile(dir, 2, 3, 3) == "B2", "tile esistente tenuta, nuova aggiunta");
        TileFolder.Record(dir, detail);
        Check(Near(TileFolder.Open(dir).Bounds, 0, -5, 20, 10), "bounds: unione dei pacchetti");

        var replace = Extract(detail, dir, true);
        Check(replace.Written == 2 && Tile(dir, 0, 0, 0) == "B0", "sostituzione");

        Check(TileFolder.Incompatibility(TileFolder.Open(Path.Combine(tmp, "legacy")), detail) != null, "png in un progetto jpg: bloccato");

        // A package recorded without an extent (old script or earlier build) leaves the bounds unknown until it is imported again.
        var old = Path.Combine(tmp, "senza-extent");
        var noExtent = TilePackage.Open(Package(tmp, "z.tpkx", "PNG", 3857, T(1, 0, 0, "Z")));
        Extract(noExtent, old, false);
        TileFolder.Record(old, noExtent);
        Check(noExtent.Bounds == null && TileFolder.Open(old).Bounds == null, "pacchetto senza extent: bounds sconosciuti");
        Directory.CreateDirectory(Path.Combine(tmp, "riesportato"));
        var again = TilePackage.Open(Package(Path.Combine(tmp, "riesportato"), "z.tpkx", "PNG", 3857, 256, new[] { 1.0, 2, 3, 4 }, T(1, 0, 0, "Z")));
        Extract(again, old, false);
        TileFolder.Record(old, again);
        Check(Near(TileFolder.Open(old).Bounds, 1, 2, 3, 4), "reimportato con l'extent: bounds noti");
    }

    static void Rejections(string tmp)
    {
        var gaussBoaga = TilePackage.Open(Package(tmp, "c.tpkx", "PNG", 3003, T(0, 0, 0, "C")));
        Check(gaussBoaga.Problems.Count == 1 && gaussBoaga.Problems[0].Contains("EPSG:3003"), "sistema di riferimento non Web Mercator: bloccato");

        var jpeg = TilePackage.Open(Package(tmp, "d.tpkx", "JPEG", 102100, T(0, 0, 0, "D")));
        Check(jpeg.Problems.Count == 0 && jpeg.Extension == "jpg", "JPEG: estensione jpg");

        var lerc = TilePackage.Open(Package(tmp, "e.tpkx", "LERC", 102100, T(0, 0, 0, "E")));
        Check(lerc.Problems.Count == 1, "LERC (quote): bloccato");

        var big = TilePackage.Open(Package(tmp, "h.tpkx", "PNG", 102100, 512, T(0, 0, 0, "H")));
        Check(big.Problems.Count == 1 && big.Problems[0].Contains("512"), "tile da 512 px: bloccate");

        var tpk = Path.Combine(tmp, "f.tpk");
        using (var zip = ZipFile.Open(tpk, ZipArchiveMode.Create))
        {
            Add(zip, "v101/Layers/conf.xml", Encoding.UTF8.GetBytes(
                "<CacheInfo><TileCacheInfo><SpatialReference><WKID>102100</WKID><LatestWKID>3857</LatestWKID></SpatialReference>" +
                "<TileOrigin><X>-20037508.342787</X><Y>20037508.342787</Y></TileOrigin><TileCols>256</TileCols><TileRows>256</TileRows>" +
                "<LODInfos><LODInfo><LevelID>0</LevelID><Resolution>156543.033928</Resolution></LODInfo>" +
                "<LODInfo><LevelID>1</LevelID><Resolution>99999</Resolution></LODInfo></LODInfos></TileCacheInfo>" +
                "<TileImageInfo><CacheTileFormat>PNG</CacheTileFormat></TileImageInfo></CacheInfo>"));
            Add(zip, "v101/Layers/_alllayers/L01/R0000C0000.bundle", BundleBytes(new[] { T(1, 0, 0, "F") }));
            var (west, south) = Merc(10, 40);
            var (east, north) = Merc(20, 45);
            Add(zip, "v101/Layers/conf.cdi", Encoding.UTF8.GetBytes(FormattableString.Invariant(
                $"<EnvelopeN><XMin>{west:R}</XMin><YMin>{south:R}</YMin><XMax>{east:R}</XMax><YMax>{north:R}</YMax>") +
                "<SpatialReference><WKID>102100</WKID><LatestWKID>3857</LatestWKID></SpatialReference></EnvelopeN>"));
        }
        var custom = TilePackage.Open(tpk);
        Check(custom.Problems.Count == 1 && custom.Problems[0].Contains("livello 1"), ".tpk con scale personalizzate: bloccato");
        Check(Near(custom.Bounds, 10, 40, 20, 45), ".tpk: extent da conf.cdi");

        var v1 = Path.Combine(tmp, "g.tpk");
        using (var zip = ZipFile.Open(v1, ZipArchiveMode.Create))
            Add(zip, "v101/Layers/_alllayers/L00/R0000C0000.bundlx", new byte[16]);
        Check(TilePackage.Open(v1).Problems.Count == 2, "Compact Cache V1: bloccato");
    }

    static void Server(string tmp)
    {
        var dir = Path.Combine(tmp, "out");
        using (var server = new TileServer(dir, 0))
        using (var http = new HttpClient())
        {
            var url = $"http://127.0.0.1:{server.Port}/";
            var ok = http.GetAsync(url + "0/0/0.png").Result;
            Check((int)ok.StatusCode == 200 && ok.Content.ReadAsStringAsync().Result == "B0", "server: tile servita");
            Check((int)http.GetAsync(url + "9/9/9.png").Result.StatusCode == 404, "server: tile mancante 404");
            Check((int)http.GetAsync(url + "..%2F..%2Fetc%2Fpasswd").Result.StatusCode == 404, "server: niente path traversal");
            Check((int)http.GetAsync($"http://localhost:{server.Port}/1/1/0.png").Result.StatusCode == 200, "server: risponde anche su localhost");
            Check(server.Requests == 4 && server.NotFound == 2, "server: contatori richieste");

            var verified = RemoteSource.VerifyAsync(RemoteSource.Template(url, "png"), dir).Result;
            Check(verified.Status == Status.Ok, $"storage remoto: tile identica ({verified.Title})");
            var stale = RemoteSource.VerifyAsync(RemoteSource.Template(url, "png"), Path.Combine(tmp, "legacy")).Result;
            Check(stale.Status == Status.Warning, $"storage remoto: tile mancante ({stale.Title})");
        }
        var down = RemoteSource.VerifyAsync("http://127.0.0.1:9/{z}/{x}/{y}.png", null).Result;
        Check(down.Status == Status.Error, $"storage remoto spento ({down.Title}: {down.Detail})");
        Check(RemoteSource.Template("https://tiles.example.it/mappa/", "jpg") == "https://tiles.example.it/mappa/{z}/{x}/{y}.jpg", "template da indirizzo base");
        Check(Throws(() => RemoteSource.Template(@"\\nas\tiles", "png")) && Throws(() => RemoteSource.Template("ftp://x/{z}/{x}/{y}", "png")), "indirizzi non http rifiutati");
    }

    // --- synthetic packages -------------------------------------------------------------------------

    static (int Z, int X, int Y, string Data) T(int z, int x, int y, string data) => (z, x, y, data);

    static string Package(string tmp, string name, string format, int wkid, params (int Z, int X, int Y, string Data)[] tiles) =>
        Package(tmp, name, format, wkid, 256, null, tiles);

    static string Package(string tmp, string name, string format, int wkid, int tileSize, params (int Z, int X, int Y, string Data)[] tiles) =>
        Package(tmp, name, format, wkid, tileSize, null, tiles);

    /// <summary>A .tpkx; <paramref name="extent"/> (west, south, east, north in degrees) goes into root.json in Web Mercator metres.</summary>
    static string Package(string tmp, string name, string format, int wkid, int tileSize, double[] extent, params (int Z, int X, int Y, string Data)[] tiles)
    {
        var path = Path.Combine(tmp, name);
        var lods = string.Join(",", Enumerable.Range(0, 24).Select(l =>
            $"{{\"level\":{l},\"resolution\":{(156543.03392804097 / Math.Pow(2, l)).ToString("R", CultureInfo.InvariantCulture)}}}"));
        var root = "{\"name\":\"Test\",\"tileImageInfo\":{\"format\":\"" + format + "\"},\"tileInfo\":{\"rows\":" + tileSize + ",\"cols\":" + tileSize + "," +
                   "\"spatialReference\":{\"wkid\":" + wkid + "},\"origin\":{\"x\":-20037508.342787001,\"y\":20037508.342787001},\"lods\":[" + lods + "]}";
        if (extent != null)
        {
            var (xmin, ymin) = Merc(extent[0], extent[1]);
            var (xmax, ymax) = Merc(extent[2], extent[3]);
            root += FormattableString.Invariant(
                $",\"fullExtent\":{{\"xmin\":{xmin:R},\"ymin\":{ymin:R},\"xmax\":{xmax:R},\"ymax\":{ymax:R},\"spatialReference\":{{\"wkid\":102100,\"latestWkid\":3857}}}}");
        }
        root += "}";
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Add(zip, "root.json", Encoding.UTF8.GetBytes(root));
            foreach (var group in tiles.GroupBy(t => (t.Z, Row: t.Y / 128 * 128, Col: t.X / 128 * 128)))
                Add(zip, $"tile/L{group.Key.Z:00}/R{group.Key.Row:x4}C{group.Key.Col:x4}.bundle", BundleBytes(group));
        }
        return path;
    }

    /// <summary>Compact Cache V2 bundle: header, 128×128 index (offset | size &lt;&lt; 40), then size-prefixed tiles.</summary>
    static byte[] BundleBytes(IEnumerable<(int Z, int X, int Y, string Data)> tiles)
    {
        var index = new ulong[128 * 128];
        var data = new MemoryStream();
        const int start = 64 + 128 * 128 * 8;
        foreach (var t in tiles)
        {
            var bytes = Encoding.ASCII.GetBytes(t.Data);
            data.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
            index[(t.Y % 128) * 128 + t.X % 128] = (ulong)(start + data.Length) | ((ulong)bytes.Length << 40);
            data.Write(bytes, 0, bytes.Length);
        }
        var bundle = new MemoryStream();
        bundle.Write(BitConverter.GetBytes(3), 0, 4);
        bundle.Write(new byte[60], 0, 60);
        foreach (var rec in index) bundle.Write(BitConverter.GetBytes(rec), 0, 8);
        data.WriteTo(bundle);
        return bundle.ToArray();
    }

    static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        using (var s = zip.CreateEntry(name, CompressionLevel.NoCompression).Open()) s.Write(bytes, 0, bytes.Length);
    }

    static void Geometry()
    {
        // The map preview's pixel maths: Rome there and back, Italy fitted in a window, the Full HD frame halved to fit.
        double lon = 12.49637, lat = 41.90235;
        Check(Math.Abs(WebMercator.Lon(WebMercator.X(lon, 12), 12) - lon) < 1e-9 && Math.Abs(WebMercator.Lat(WebMercator.Y(lat, 12), 12) - lat) < 1e-9,
            "pixel e gradi, andata e ritorno");
        Check(WebMercator.X(0, 0) == 128 && Math.Abs(WebMercator.Y(0, 0) - 128) < 1e-9 && Math.Abs(WebMercator.MetresPerPixel(0, 0) - 156543.03392804097) < 1e-6,
            "zoom 0: centro del mondo e metri per pixel");
        var italy = new[] { 6.6, 36.6, 18.5, 47.1 };
        Check(WebMercator.FitZoom(italy, 1100, 700, 0, 16) == 6 && WebMercator.FitZoom(italy, 990, 630, 0, 16) == 5, "l'Italia entra nella finestra");
        Check(WebMercator.FitZoom(italy, 100000, 100000, 0, 8) == 8 && WebMercator.FitZoom(italy, 10, 10, 3, 8) == 3, "zoom dentro quelli del progetto");
        Check(WebMercator.FrameSteps(1920, 1080, 2000, 1200) == 0 && WebMercator.FrameSteps(1920, 1080, 1068, 574) == 1 &&
              WebMercator.FrameSteps(1920, 1080, 400, 300) == 3, "riquadro Full HD dimezzato finché entra");
        Check(WebMercator.FrameSteps(1920, 1080, 0, -5) <= 16, "finestra senza area: il riquadro non cicla");
    }

    // --- helpers ------------------------------------------------------------------------------------

    /// <summary>Degrees to Web Mercator metres.</summary>
    static (double X, double Y) Merc(double lon, double lat) =>
        (lon * Math.PI / 180 * 6378137, 6378137 * Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360)));

    static bool Near(double[] actual, params double[] expected) =>
        actual != null && actual.Length == expected.Length && actual.Zip(expected, (a, e) => Math.Abs(a - e) < 1e-6).All(ok => ok);

    static ExtractProgress Extract(TilePackage pkg, string dir, bool overwrite)
    {
        var progress = new ExtractProgress();
        pkg.Extract(dir, overwrite, progress, CancellationToken.None);
        return progress;
    }

    static string Tile(string dir, int z, int x, int y)
    {
        var file = Path.Combine(dir, z.ToString(), x.ToString(), y + ".png");
        return File.Exists(file) ? File.ReadAllText(file) : null;
    }

    static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (FormatException) { return true; }
    }

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok    " : "FAIL  ") + what);
        if (!ok) failures++;
    }
}
