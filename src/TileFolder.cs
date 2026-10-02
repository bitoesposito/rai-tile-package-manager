using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RaiTilePackageManager
{
    /// <summary>A project: the {z}/{x}/{y} folder plus its metadata.json (format, remote address, imported packages).</summary>
    [DataContract]
    public sealed class ProjectInfo
    {
        [DataMember(Name = "format", Order = 1)] public string Format;
        [DataMember(Name = "remoteUrl", Order = 2, EmitDefaultValue = false)] public string RemoteUrl;
        [DataMember(Name = "sources", Order = 3)] public List<SourceInfo> Sources;
        /// <summary>Not stored: the project folder and the zoom levels found on disk.</summary>
        public string Dir;
        public int[] Levels;
        /// <summary>Not stored: the union of the packages' extents in degrees (west, south, east, north), or null when unknown.</summary>
        public double[] Bounds;
    }

    /// <summary>One package imported into the project.</summary>
    [DataContract]
    public sealed class SourceInfo
    {
        [DataMember(Name = "file", Order = 1)] public string File;
        [DataMember(Name = "name", Order = 2)] public string Name;
        [DataMember(Name = "levels", Order = 3)] public int[] Levels;
        [DataMember(Name = "bounds", Order = 4, EmitDefaultValue = false)] public double[] Bounds;
        [DataMember(Name = "date", Order = 5)] public string Date;
    }

    /// <summary>Project folders: recognising them, creating dedicated ones, recording what goes in.</summary>
    public static class TileFolder
    {
        public const string MetadataFile = "metadata.json";
        static readonly DataContractJsonSerializer Json = new DataContractJsonSerializer(typeof(ProjectInfo));
        // Windows rules even when the tests run elsewhere: projects live on Windows disks and shares.
        static readonly char[] WindowsNameChars = Path.GetInvalidFileNameChars().Union("<>:\"/\\|?*").ToArray();

        /// <summary>
        /// The project in <paramref name="dir"/>, or null when the folder is not one. A project holds metadata.json,
        /// or nothing but zoom folders (the output of the old Python script): a Desktop never qualifies.
        /// Throws when the drive or the network share is unreachable.
        /// </summary>
        public static ProjectInfo Open(string dir)
        {
            var root = Path.GetPathRoot(Path.GetFullPath(dir));
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"{root} non è raggiungibile: controlla la connessione di rete, l'unità o i permessi.");
            if (!Directory.Exists(dir)) return null;
            var levels = Levels(dir);
            var info = Load(dir);
            if (info == null)
            {
                bool onlyTiles = Directory.EnumerateFileSystemEntries(dir).All(e => IsZoomFolder(e) || IsShellFile(e) || IsMetadata(e));
                if (levels.Length == 0 || !onlyTiles) return null; // a damaged metadata.json still counts
                info = new ProjectInfo();
            }
            info.Dir = dir;
            info.Levels = levels;
            info.Sources = info.Sources ?? new List<SourceInfo>();
            info.Bounds = Union(info.Sources);
            info.Format = info.Format ?? levels
                .Select(z => Directory.EnumerateFiles(Path.Combine(dir, z.ToString()), "*", SearchOption.AllDirectories).FirstOrDefault())
                .Where(f => f != null)
                .Select(f => Path.GetExtension(f).TrimStart('.').ToLowerInvariant())
                .FirstOrDefault();
            return info;
        }

        /// <summary>The project <paramref name="dir"/> is, or sits in as one of its {z} or {z}/{x} folders; null when none.</summary>
        public static ProjectInfo Find(string dir)
        {
            var project = Open(dir);
            var d = new DirectoryInfo(dir).Parent;
            for (int up = 1; project == null && up <= 2 && d != null; up++, d = d.Parent)
            {
                try { project = Open(d.FullName); }
                catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { break; } // parent not readable: not ours to judge
            }
            return project;
        }

        /// <summary>
        /// The dedicated folder a new project gets inside <paramref name="location"/>: its name, or "name (2)", "name (3)"…
        /// when that is taken. Tiles never land loose in the chosen folder.
        /// </summary>
        public static string NewProjectPath(string location, string name)
        {
            name = string.Concat(name.Split(WindowsNameChars)).Trim().TrimEnd('.');
            if (name.Length == 0) name = "tile";
            for (int n = 1; ; n++)
            {
                var dir = Path.Combine(location, n == 1 ? name : $"{name} ({n})");
                if (File.Exists(dir)) continue;
                if (!Directory.Exists(dir) || !Directory.EnumerateFileSystemEntries(dir).Any()) return dir;
            }
        }

        /// <summary>Why <paramref name="pkg"/> cannot go into <paramref name="project"/>, or null when it can.</summary>
        public static string Incompatibility(ProjectInfo project, TilePackage pkg) =>
            project.Format == null || project.Format == pkg.Extension ? null
                : $"Il progetto contiene tile {project.Format.ToUpperInvariant()} e il pacchetto {pkg.Extension.ToUpperInvariant()}. " +
                  "GEOlayers usa un solo URL con una sola estensione, quindi il formato deve essere lo stesso: " +
                  $"riesporta il pacchetto in {project.Format.ToUpperInvariant()} oppure crea un nuovo progetto.";

        /// <summary>Creates the folder and proves it is writable (network shares can be read-only or gone).</summary>
        public static void CheckWritable(string dir)
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".prova-scrittura-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(probe, new byte[0]);
            File.Delete(probe);
        }

        /// <summary>Adds the imported package to metadata.json.</summary>
        public static void Record(string dir, TilePackage pkg)
        {
            var info = Load(dir) ?? new ProjectInfo();
            info.Format = pkg.Extension;
            info.Sources = info.Sources ?? new List<SourceInfo>();
            info.Sources.Add(new SourceInfo
            {
                File = Path.GetFileName(pkg.FilePath),
                Name = pkg.Name,
                Levels = pkg.Levels,
                Bounds = pkg.Bounds,
                Date = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            });
            Save(dir, info);
        }

        /// <summary>Remembers the verified remote storage of the project.</summary>
        public static void SetRemoteUrl(string dir, string url)
        {
            var info = Open(dir);
            if (info == null) return;
            info.RemoteUrl = url;
            Save(dir, info);
        }

        /// <summary>A tile of the project (lowest zoom first), used to test a remote copy of it.</summary>
        public static (int Z, int X, int Y, string File)? SampleTile(string dir)
        {
            foreach (var z in Levels(dir))
            {
                var file = Directory.EnumerateFiles(Path.Combine(dir, z.ToString()), "*", SearchOption.AllDirectories).FirstOrDefault();
                if (file != null
                    && int.TryParse(Path.GetFileName(Path.GetDirectoryName(file)), out var x)
                    && int.TryParse(Path.GetFileNameWithoutExtension(file), out var y))
                    return (z, x, y, file);
            }
            return null;
        }

        /// <summary>
        /// The union of the packages' extents; a re-import of the same file replaces its older entry. Null as soon as one
        /// package has no extent recorded (imported by the old script or by an earlier build): with partial bounds GEOlayers
        /// would skip that package's tiles.
        /// </summary>
        static double[] Union(List<SourceInfo> sources)
        {
            var latest = sources.GroupBy(s => s.File, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
            if (latest.Count == 0 || latest.Any(s => s.Bounds == null || s.Bounds.Length != 4)) return null;
            return new[] { latest.Min(s => s.Bounds[0]), latest.Min(s => s.Bounds[1]), latest.Max(s => s.Bounds[2]), latest.Max(s => s.Bounds[3]) };
        }

        static int[] Levels(string dir) => Directory.EnumerateDirectories(dir)
            .Where(IsZoomFolder)
            .Select(d => int.Parse(Path.GetFileName(d), CultureInfo.InvariantCulture))
            .OrderBy(z => z)
            .ToArray();

        static bool IsZoomFolder(string path) =>
            int.TryParse(Path.GetFileName(path), NumberStyles.None, CultureInfo.InvariantCulture, out var z) && z <= 30 && Directory.Exists(path);

        static bool IsMetadata(string path) => Path.GetFileName(path).Equals(MetadataFile, StringComparison.OrdinalIgnoreCase);

        /// <summary>Files Windows drops into any folder on its own.</summary>
        static bool IsShellFile(string path)
        {
            var name = Path.GetFileName(path);
            return name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase);
        }

        static ProjectInfo Load(string dir)
        {
            var file = Path.Combine(dir, MetadataFile);
            if (!File.Exists(file)) return null;
            try
            {
                using (var s = File.OpenRead(file))
                    return (ProjectInfo)Json.ReadObject(s);
            }
            catch (SerializationException)
            {
                return null; // damaged or hand-edited: the folder is judged by what it contains
            }
        }

        static void Save(string dir, ProjectInfo info)
        {
            using (var s = File.Create(Path.Combine(dir, MetadataFile)))
            using (var w = JsonReaderWriterFactory.CreateJsonWriter(s, Encoding.UTF8, false, true, "  "))
                Json.WriteObject(w, info);
        }
    }
}
