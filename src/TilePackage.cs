using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RaiTilePackageManager
{
    /// <summary>One .bundle file of the package: zoom level and top-left tile of its 128×128 block.</summary>
    public sealed class Bundle
    {
        public string Entry;
        public int Level, Row, Col;
        public long Length;
    }

    /// <summary>Counters shared by the extraction workers and the UI.</summary>
    public sealed class ExtractProgress
    {
        public long Bytes;
        public int Written, Skipped;
    }

    /// <summary>
    /// ArcGIS Pro tile package (.tpkx or .tpk): a zip of Compact Cache V2 bundles plus metadata
    /// (root.json in a .tpkx, conf.xml in a .tpk), converted into an XYZ folder {z}/{x}/{y}.{ext}.
    /// </summary>
    public sealed class TilePackage
    {
        public string FilePath { get; private set; }
        public string Name { get; private set; }
        public string Format { get; private set; } = "PNG";
        public string Extension { get; private set; } = "png";
        public List<Bundle> Bundles { get; } = new List<Bundle>();
        public int[] Levels { get; private set; }
        public long TotalBytes => Bundles.Sum(b => b.Length);
        /// <summary>The extent set in ArcGIS Pro, in degrees: west, south, east, north. Null when the package declares none.</summary>
        public double[] Bounds { get; private set; }
        /// <summary>Reasons the tiles would not line up as XYZ Web Mercator tiles: conversion is blocked.</summary>
        public List<string> Problems { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        // Compact Cache V2: 128×128 tiles per bundle; a 64-byte header, then a row-major index of
        // 8-byte records (low 40 bits = offset of the tile data, high 24 bits = its size, 0 = no tile).
        const int Side = 128;
        const int IndexEnd = 64 + Side * Side * 8;
        static readonly Regex BundleName = new Regex(@"(?:^|/)L(\d+)/R([0-9a-f]+)C([0-9a-f]+)\.bundle$", RegexOptions.IgnoreCase);

        // "ArcGIS Online / Bing Maps / Google Maps" tiling scheme: the grid XYZ clients like GEOlayers use.
        static readonly int[] WebMercatorWkids = { 3857, 102100, 102113, 900913 };
        const double OriginShift = 20037508.342787;
        const double Level0Resolution = 156543.03392804097; // metres per pixel at zoom 0, 256 px tiles
        const string Reexport = "In ArcGIS Pro riesporta il pacchetto con lo schema di tassellatura \"ArcGIS Online / Bing Maps / Google Maps\".";

        public static TilePackage Open(string path)
        {
            var p = new TilePackage { FilePath = path, Name = Path.GetFileNameWithoutExtension(path) };
            using (var zip = ZipFile.OpenRead(path))
            {
                ZipArchiveEntry root = null, conf = null, cdi = null;
                bool v1 = false;
                foreach (var e in zip.Entries)
                {
                    var name = e.FullName.Replace('\\', '/');
                    var file = name.Substring(name.LastIndexOf('/') + 1);
                    var m = BundleName.Match(name);
                    if (m.Success)
                        p.Bundles.Add(new Bundle
                        {
                            Entry = e.FullName,
                            Level = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                            Row = int.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                            Col = int.Parse(m.Groups[3].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                            Length = e.Length,
                        });
                    else if (file.EndsWith(".bundlx", StringComparison.OrdinalIgnoreCase))
                        v1 = true;
                    else if (file.Equals("root.json", StringComparison.OrdinalIgnoreCase) && (root == null || name.Length < root.FullName.Length))
                        root = e;
                    else if (file.Equals("conf.xml", StringComparison.OrdinalIgnoreCase) && conf == null)
                        conf = e;
                    else if (file.Equals("conf.cdi", StringComparison.OrdinalIgnoreCase) && cdi == null)
                        cdi = e; // .tpk: the extent of the cache
                }
                p.Levels = p.Bundles.Select(b => b.Level).Distinct().OrderBy(l => l).ToArray();
                if (p.Bundles.Count == 0)
                    p.Problems.Add("Il file non contiene tile: non sembra un tile package di ArcGIS Pro.");
                if (v1)
                    p.Problems.Add("Il pacchetto usa il vecchio formato Compact Cache V1 (ArcMap), non supportato: riesportalo con ArcGIS Pro.");

                if (root != null)
                    using (var s = root.Open()) p.ReadRootJson(s);
                else if (conf != null)
                {
                    using (var s = conf.Open()) p.ReadConfXml(s);
                    if (cdi != null)
                        using (var s = cdi.Open()) p.ReadConfCdi(s);
                }
                else
                    p.Warnings.Add("Il pacchetto non contiene metadati: l'app assume lo schema Web Mercator standard.");
            }
            return p;
        }

        void ReadRootJson(Stream s)
        {
            var json = new MemoryStream();
            s.CopyTo(json);
            var root = (RootJson)new DataContractJsonSerializer(typeof(RootJson)).ReadObject(new MemoryStream(json.ToArray()));
            if (!string.IsNullOrEmpty(root.name)) Name = root.name;
            try
            {
                // Read on its own: an odd extent must not stop the conversion, it only leaves the bounds unknown.
                var extents = (ExtentsJson)new DataContractJsonSerializer(typeof(ExtentsJson)).ReadObject(new MemoryStream(json.ToArray()));
                var e = extents.fullExtent ?? extents.initialExtent;
                if (e?.xmin != null && e.ymin != null && e.xmax != null && e.ymax != null)
                    Bounds = Degrees(e.xmin.Value, e.ymin.Value, e.xmax.Value, e.ymax.Value, Wkid(e.spatialReference) ?? Wkid(root.tileInfo?.spatialReference));
            }
            catch (SerializationException) { }
            var ti = root.tileInfo;
            if (ti == null)
            {
                Warnings.Add("root.json non descrive la griglia di tile: l'app assume lo schema Web Mercator standard.");
                return;
            }
            var resolutions = new Dictionary<int, double>();
            foreach (var lod in ti.lods ?? new LodJson[0]) resolutions[lod.level] = lod.resolution;
            Check(Wkid(ti.spatialReference), ti.origin?.x, ti.origin?.y, ti.rows, ti.cols, resolutions, root.tileImageInfo?.format ?? ti.format);
        }

        static int? Wkid(SpatialReferenceJson sr) => sr == null ? (int?)null : sr.latestWkid > 0 ? sr.latestWkid : sr.wkid;

        static XElement El(XContainer c, string name) => c?.Descendants().FirstOrDefault(e => e.Name.LocalName == name);

        static double? Num(XContainer c, string name) =>
            double.TryParse(El(c, name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;

        void ReadConfXml(Stream s)
        {
            var x = XDocument.Load(s);
            var origin = El(x, "TileOrigin");
            var resolutions = new Dictionary<int, double>();
            foreach (var lod in x.Descendants().Where(e => e.Name.LocalName == "LODInfo"))
                if (Num(lod, "LevelID") is double id && Num(lod, "Resolution") is double r)
                    resolutions[(int)id] = r;
            Check((int?)(Num(x, "LatestWKID") ?? Num(x, "WKID")), Num(origin, "X"), Num(origin, "Y"),
                (int)(Num(x, "TileRows") ?? 0), (int)(Num(x, "TileCols") ?? 0), resolutions, El(x, "CacheTileFormat")?.Value);
        }

        void ReadConfCdi(Stream s)
        {
            var x = XDocument.Load(s);
            if (Num(x, "XMin") is double xmin && Num(x, "YMin") is double ymin && Num(x, "XMax") is double xmax && Num(x, "YMax") is double ymax)
                Bounds = Degrees(xmin, ymin, xmax, ymax, (int?)(Num(x, "LatestWKID") ?? Num(x, "WKID")));
        }

        /// <summary>
        /// An extent as west, south, east, north in degrees, the order GEOlayers uses. Web Mercator metres are converted;
        /// WGS 84 (4326) is already in degrees; anything else, or an empty extent, gives null.
        /// </summary>
        static double[] Degrees(double xmin, double ymin, double xmax, double ymax, int? wkid)
        {
            if (!(xmin < xmax && ymin < ymax)) return null; // also rejects NaN
            double Clamp(double v, double limit) => Math.Max(-limit, Math.Min(limit, v));
            if (wkid == 4326) return new[] { Clamp(xmin, 180), Clamp(ymin, 85.0511287798), Clamp(xmax, 180), Clamp(ymax, 85.0511287798) };
            if (wkid.HasValue && Array.IndexOf(WebMercatorWkids, wkid.Value) < 0) return null;
            double Lon(double x) => Clamp(x, OriginShift) / WebMercator.EarthRadius * 180 / Math.PI;
            double Lat(double y) => (2 * Math.Atan(Math.Exp(Clamp(y, OriginShift) / WebMercator.EarthRadius)) - Math.PI / 2) * 180 / Math.PI;
            return new[] { Lon(xmin), Lat(ymin), Lon(xmax), Lat(ymax) };
        }

        void Check(int? wkid, double? originX, double? originY, int rows, int cols, Dictionary<int, double> resolutions, string format)
        {
            if (wkid.HasValue && Array.IndexOf(WebMercatorWkids, wkid.Value) < 0)
                Problems.Add($"Sistema di riferimento {(wkid == 0 ? "personalizzato" : "EPSG:" + wkid)}: GEOlayers usa Web Mercator (EPSG:3857). {Reexport}");
            else if (originX.HasValue && originY.HasValue && (Math.Abs(originX.Value + OriginShift) > 1 || Math.Abs(originY.Value - OriginShift) > 1))
                Problems.Add($"La griglia di tile ha un'origine non standard e le tile non sarebbero allineate. {Reexport}");

            if ((rows != 0 || cols != 0) && (rows != 256 || cols != 256)) // 0 = not declared: ArcGIS default
                Problems.Add($"Tile da {cols}×{rows} px: GEOlayers usa tile da 256 px. {Reexport}");

            if (Problems.Count == 0)
                foreach (var level in Levels)
                {
                    double expected = Level0Resolution / Math.Pow(2, level);
                    if (resolutions.TryGetValue(level, out var r) && Math.Abs(r - expected) / expected > 1e-3)
                    {
                        Problems.Add($"Il livello {level} usa una scala personalizzata e non corrisponde allo zoom {level} di Web Mercator. {Reexport}");
                        break;
                    }
                }

            if (!string.IsNullOrEmpty(format)) Format = format;
            var f = Format.ToUpperInvariant();
            if (f.StartsWith("JPG") || f.StartsWith("JPEG"))
                Extension = "jpg";
            else if (f.StartsWith("PNG"))
                Extension = "png";
            else if (f == "MIXED")
                Warnings.Add("Formato MIXED: anche le tile JPEG vengono salvate con estensione .png. Se GEOlayers non le mostra, riesporta il pacchetto in PNG o in JPEG.");
            else
                Problems.Add($"Formato tile {Format} non supportato: servono immagini PNG o JPEG.");
        }

        /// <summary>Writes every tile to {outDir}/{z}/{x}/{y}.{ext}; with overwrite = false tiles already there are kept.</summary>
        public void Extract(string outDir, bool overwrite, ExtractProgress progress, CancellationToken ct)
        {
            var options = new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) };
            Parallel.ForEach(Bundles, options,
                () => ZipFile.OpenRead(FilePath), // ZipArchive is not thread-safe: one per worker
                (bundle, _, zip) => { ExtractBundle(zip, bundle, outDir, overwrite, progress, ct); return zip; },
                zip => zip.Dispose());
        }

        void ExtractBundle(ZipArchive zip, Bundle b, string outDir, bool overwrite, ExtractProgress progress, CancellationToken ct)
        {
            using (var s = zip.GetEntry(b.Entry).Open())
            {
                var head = new byte[IndexEnd];
                ReadFully(s, head, head.Length);
                if (BitConverter.ToInt32(head, 0) != 3)
                    throw new InvalidDataException($"{b.Entry}: bundle non in formato Compact Cache V2.");
                Interlocked.Add(ref progress.Bytes, IndexEnd);
                var tiles = new List<(long Offset, int Size, int Index)>();
                for (int i = 0; i < Side * Side; i++)
                {
                    ulong rec = BitConverter.ToUInt64(head, 64 + i * 8);
                    if (rec >> 40 != 0) tiles.Add(((long)(rec & 0xFFFFFFFFFF), (int)(rec >> 40), i));
                }
                // Zip entry streams are forward-only: read the tiles in file order, one at a time,
                // so memory stays flat whatever the bundle size.
                tiles.Sort((x, y) => x.Offset.CompareTo(y.Offset));

                long pos = IndexEnd;
                var buf = new byte[0];
                var dirs = new HashSet<string>();
                foreach (var t in tiles)
                {
                    ct.ThrowIfCancellationRequested();
                    if (t.Offset < pos) throw new InvalidDataException($"{b.Entry}: indice delle tile non valido.");
                    Skip(s, t.Offset - pos);
                    if (buf.Length < t.Size) buf = new byte[t.Size];
                    ReadFully(s, buf, t.Size);
                    Interlocked.Add(ref progress.Bytes, t.Offset + t.Size - pos);
                    pos = t.Offset + t.Size;
                    int x = b.Col + t.Index % Side, y = b.Row + t.Index / Side;
                    var dir = Path.Combine(outDir, b.Level.ToString(), x.ToString());
                    if (dirs.Add(dir)) Directory.CreateDirectory(dir);
                    if (WriteTile(Path.Combine(dir, y + "." + Extension), buf, t.Size, overwrite))
                        Interlocked.Increment(ref progress.Written);
                    else
                        Interlocked.Increment(ref progress.Skipped);
                }
                Interlocked.Add(ref progress.Bytes, b.Length - pos);
            }
        }

        static bool WriteTile(string file, byte[] data, int size, bool overwrite)
        {
            try
            {
                using (var f = new FileStream(file, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write))
                    f.Write(data, 0, size);
                return true;
            }
            catch (IOException) when (!overwrite && File.Exists(file))
            {
                return false; // already in the project: keep it
            }
        }

        static void ReadFully(Stream s, byte[] buf, int count)
        {
            for (int n = 0, r; n < count; n += r)
                if ((r = s.Read(buf, n, count - n)) == 0) throw new EndOfStreamException("Bundle troncato.");
        }

        static void Skip(Stream s, long count)
        {
            var scratch = new byte[Math.Min(count, 81920)];
            for (int r; count > 0; count -= r)
                if ((r = s.Read(scratch, 0, (int)Math.Min(count, scratch.Length))) == 0) throw new EndOfStreamException("Bundle troncato.");
        }

        // root.json fields we need (the file also lists layers, legends...); filled by the serializer.
#pragma warning disable CS0649
        [DataContract] sealed class RootJson
        {
            [DataMember] public string name;
            [DataMember] public TileInfoJson tileInfo;
            [DataMember] public FormatJson tileImageInfo;
        }
        [DataContract] sealed class TileInfoJson
        {
            [DataMember] public SpatialReferenceJson spatialReference;
            [DataMember] public PointJson origin;
            [DataMember] public int rows, cols;
            [DataMember] public string format;
            [DataMember] public LodJson[] lods;
        }
        [DataContract] sealed class SpatialReferenceJson { [DataMember] public int wkid, latestWkid; }
        [DataContract] sealed class PointJson { [DataMember] public double x, y; }
        [DataContract] sealed class LodJson { [DataMember] public int level; [DataMember] public double resolution; }
        [DataContract] sealed class FormatJson { [DataMember] public string format; }
        [DataContract] sealed class ExtentsJson { [DataMember] public ExtentJson fullExtent, initialExtent; }
        [DataContract] sealed class ExtentJson
        {
            [DataMember] public double? xmin, ymin, xmax, ymax;
            [DataMember] public SpatialReferenceJson spatialReference;
        }
#pragma warning restore CS0649
    }

    /// <summary>
    /// Pixel geometry of the XYZ grid: at zoom z the world is 256 × 2^z pixels square, x growing east from 180° W and y
    /// growing south from 85.05° N. The map preview draws and measures with it.
    /// </summary>
    public static class WebMercator
    {
        public const int TileSize = 256;
        public const double EarthRadius = 6378137;

        public static double WorldSize(int z) => TileSize * Math.Pow(2, z);
        public static double X(double lon, int z) => (lon + 180) / 360 * WorldSize(z);
        public static double Y(double lat, int z)
        {
            double phi = lat * Math.PI / 180;
            return (1 - Math.Log(Math.Tan(phi) + 1 / Math.Cos(phi)) / Math.PI) / 2 * WorldSize(z);
        }
        public static double Lon(double x, int z) => x / WorldSize(z) * 360 - 180;
        public static double Lat(double y, int z) => Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / WorldSize(z)))) * 180 / Math.PI;

        /// <summary>The ground width of one pixel at zoom <paramref name="z"/> and latitude <paramref name="lat"/>, in metres.</summary>
        public static double MetresPerPixel(double lat, int z) => 2 * Math.PI * EarthRadius * Math.Cos(lat * Math.PI / 180) / WorldSize(z);

        /// <summary>The deepest zoom in [min, max] at which <paramref name="bounds"/> (west, south, east, north) fit in width × height pixels.</summary>
        public static int FitZoom(double[] bounds, int width, int height, int min, int max)
        {
            for (int z = max; z > min; z--)
                if (X(bounds[2], z) - X(bounds[0], z) <= width && Y(bounds[1], z) - Y(bounds[3], z) <= height) return z;
            return min;
        }

        /// <summary>
        /// How many times a comp of compWidth × compHeight pixels must be halved to fit in width × height: the frame that
        /// fits shows what the comp takes in at that many zoom levels deeper than the view.
        /// </summary>
        public static int FrameSteps(int compWidth, int compHeight, int width, int height)
        {
            int steps = 0;
            while (steps < 16 && ((compWidth >> steps) > width || (compHeight >> steps) > height)) steps++;
            return steps;
        }
    }
}
