using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RaiTilePackageManager
{
    public enum Status { Info, Ok, Warning, Error }

    /// <summary>
    /// Static file server for a project folder on 127.0.0.1 and ::1 only, so localhost and 127.0.0.1 both work.
    /// Plain sockets: HttpListener would need a URL ACL or admin rights and would trigger the firewall prompt.
    /// </summary>
    public sealed class TileServer : IDisposable
    {
        readonly string root;
        readonly List<TcpListener> listeners = new List<TcpListener>();
        volatile bool stopped;
        int requests, notFound;

        public int Port { get; }
        public int Requests => Volatile.Read(ref requests);
        public int NotFound => Volatile.Read(ref notFound);

        /// <summary>Starts listening; throws SocketException when the port is taken. Port 0 picks a free one.</summary>
        public TileServer(string root, int port)
        {
            this.root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            try
            {
                foreach (var ip in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
                {
                    var listener = new TcpListener(ip, port);
                    try
                    {
                        listener.Start();
                    }
                    catch (SocketException e) when (ip.AddressFamily == AddressFamily.InterNetworkV6 && e.SocketErrorCode != SocketError.AddressAlreadyInUse)
                    {
                        continue; // IPv6 disabled on this PC: IPv4 is enough
                    }
                    listeners.Add(listener);
                    port = ((IPEndPoint)listener.LocalEndpoint).Port; // same port for IPv6 when 0 was asked
                }
            }
            catch
            {
                Dispose();
                throw;
            }
            Port = port;
            foreach (var listener in listeners) _ = AcceptLoop(listener);
        }

        public void Dispose()
        {
            stopped = true;
            foreach (var listener in listeners) listener.Stop();
        }

        async Task AcceptLoop(TcpListener listener)
        {
            while (!stopped)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception) when (stopped)
                {
                    return;
                }
                catch (SocketException)
                {
                    continue; // a client that went away before being accepted
                }
                _ = Serve(client);
            }
        }

        async Task Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true);
                    var request = await reader.ReadLineAsync().ConfigureAwait(false);
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync().ConfigureAwait(false))) { } // headers: not needed
                    if (request == null || stopped) return;

                    Interlocked.Increment(ref requests);
                    var parts = request.Split(' ');
                    if (parts.Length != 3 || parts[0] != "GET")
                    {
                        await Send(stream, "405 Method Not Allowed", null).ConfigureAwait(false);
                        return;
                    }
                    var file = Resolve(parts[1]);
                    if (file == null || !File.Exists(file))
                    {
                        Interlocked.Increment(ref notFound);
                        await Send(stream, "404 Not Found", null).ConfigureAwait(false);
                        return;
                    }
                    await Send(stream, "200 OK", File.ReadAllBytes(file)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // client gone, server stopped or file unreadable: nothing more to answer
                }
            }
        }

        /// <summary>Maps /z/x/y.png to a file inside the project, refusing anything that escapes it.</summary>
        string Resolve(string target)
        {
            int cut = target.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0) target = target.Substring(0, cut);
            var parts = Uri.UnescapeDataString(target).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Any(p => p == "." || p == ".." || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                return null;
            var full = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
            return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
        }

        static async Task Send(Stream stream, string status, byte[] body)
        {
            var type = body == null ? "text/plain" : !IsImage(body) ? "application/octet-stream" : body[0] == 0x89 ? "image/png" : "image/jpeg";
            body = body ?? Encoding.ASCII.GetBytes(status);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\n" +
                "Access-Control-Allow-Origin: *\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
            await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
        }

        /// <summary>PNG or JPEG signature (content type comes from the bytes: MIXED packages put JPEG data in .png files).</summary>
        public static bool IsImage(byte[] b) =>
            b.Length > 3 && ((b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G') || (b[0] == 0xFF && b[1] == 0xD8));
    }

    /// <summary>A web server publishing the project, used by GEOlayers instead of the local server.</summary>
    public static class RemoteSource
    {
        /// <summary>Turns a base address or a full {z}/{x}/{y} URL into a URL template; FormatException with a hint otherwise.</summary>
        public static string Template(string input, string extension)
        {
            var url = input.Trim();
            if (url.StartsWith(@"\\") || (url.Length > 1 && url[1] == ':'))
                throw new FormatException("È il percorso di una cartella: per usarla, sceglila come progetto nella scheda In onda.");
            if (url.IndexOf("{z}", StringComparison.Ordinal) < 0)
                url = url.TrimEnd('/') + "/{z}/{x}/{y}." + extension;
            bool placeholders = url.IndexOf("{x}", StringComparison.Ordinal) >= 0 && url.IndexOf("{y}", StringComparison.Ordinal) >= 0;
            if (!placeholders || !Uri.TryCreate(Fill(url, 0, 0, 0), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new FormatException("Serve un indirizzo http:// o https://, per esempio https://tiles.azienda.it/mappa.");
            return url;
        }

        public static string Fill(string template, int z, int x, int y) =>
            template.Replace("{z}", z.ToString()).Replace("{x}", x.ToString()).Replace("{y}", y.ToString());

        /// <summary>
        /// Downloads one tile through the template. With a local project the remote tile must match the local one
        /// byte for byte; without it the check falls back to tile 0/0/0.
        /// </summary>
        public static async Task<(Status Status, string Title, string Detail)> VerifyAsync(string template, string projectDir)
        {
            var sample = projectDir == null ? null : TileFolder.SampleTile(projectDir);
            int z = sample?.Z ?? 0, x = sample?.X ?? 0, y = sample?.Y ?? 0;
            var tile = $"{z}/{x}/{y}";
            var handler = new HttpClientHandler { DefaultProxyCredentials = CredentialCache.DefaultCredentials };
            using (var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) })
            {
                HttpResponseMessage response;
                try
                {
                    response = await http.GetAsync(Fill(template, z, x, y)).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return (Status.Error, "Nessuna risposta", "Il server non ha risposto entro 15 secondi.");
                }
                catch (HttpRequestException e)
                {
                    return (Status.Error, "Storage non raggiungibile", e.InnerException?.Message ?? e.Message);
                }
                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return sample != null
                            ? (Status.Warning, $"La tile {tile} non c'è", "Il server risponde con 404. Controlla che le tile del progetto siano state caricate in quel percorso.")
                            : (Status.Warning, "La tile 0/0/0 non c'è", "Il server risponde con 404. Succede se le tile non partono dallo zoom 0: per un controllo completo scegli il progetto nella scheda In onda.");
                    if (!response.IsSuccessStatusCode)
                        return (Status.Error, $"Errore HTTP {(int)response.StatusCode}", response.ReasonPhrase);
                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (sample != null)
                        return body.SequenceEqual(File.ReadAllBytes(sample.Value.File))
                            ? (Status.Ok, "Storage raggiungibile", $"La tile {tile} coincide con quella del progetto.")
                            : (Status.Warning, "Storage non aggiornato", $"La tile {tile} è diversa da quella del progetto.");
                    return TileServer.IsImage(body)
                        ? (Status.Ok, "Storage raggiungibile", "La tile 0/0/0 è un'immagine valida.")
                        : (Status.Warning, "Risposta inattesa", "Il server non restituisce un'immagine: forse una pagina di accesso o di errore.");
                }
            }
        }
    }
}
