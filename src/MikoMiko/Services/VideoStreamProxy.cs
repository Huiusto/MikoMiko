using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace MikoMiko.Services;

/// <summary>
/// 本机 HTTP Range 代理：MediaElement 只认 http(s) 且对米游社签名直链的跳转支持很差，
/// 这里把远程视频包成 127.0.0.1 的普通 HTTP 资源，并把播放器的 Range 请求原样转发，
/// 从而做到边缓冲边播放、可拖动进度、按需请求，而不是先整段下载再播。
/// </summary>
public sealed class VideoStreamProxy
{
    private static readonly Lazy<VideoStreamProxy> Lazy = new(() => new VideoStreamProxy());
    public static VideoStreamProxy Instance => Lazy.Value;

    private readonly ConcurrentDictionary<string, string> _urls = new();
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.None,
        AllowAutoRedirect = true,
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private TcpListener? _listener;
    private int _port;

    private VideoStreamProxy()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://www.miyoushe.com/");
        Start();
    }

    public string? GetProxyUrl(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl)) return null;
        var token = Guid.NewGuid().ToString("N");
        _urls[token] = remoteUrl;
        return $"http://127.0.0.1:{_port}/v/{token}";
    }

    public void Release(string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl)) return;
        var index = proxyUrl.LastIndexOf('/');
        if (index < 0) return;
        _urls.TryRemove(proxyUrl[(index + 1)..], out _);
    }

    private void Start()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            var thread = new Thread(AcceptLoop) { IsBackground = true, Name = "MikoMiko-VideoProxy" };
            thread.Start();
        }
        catch
        {
            _listener = null;
        }
    }

    private async void AcceptLoop()
    {
        while (true)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = HandleAsync(client);
            }
            catch
            {
                return;
            }
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var headerText = await ReadHeadersAsync(stream).ConfigureAwait(false);
                if (headerText.Length == 0) return;

                var lines = headerText.Split("\r\n");
                var first = lines[0].Split(' ');
                if (first.Length < 2) return;
                var method = first[0];
                var path = first[1];
                if (!path.StartsWith("/v/")) return;

                var token = path[3..];
                if (!_urls.TryGetValue(token, out var remote)) return;

                var rangeLine = lines.FirstOrDefault(l =>
                    l.StartsWith("Range:", StringComparison.OrdinalIgnoreCase));
                var range = rangeLine is null ? null : rangeLine[6..].Trim();

                using var request = new HttpRequestMessage(
                    method.Equals("HEAD", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Head : HttpMethod.Get, remote);
                request.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");
                if (!string.IsNullOrWhiteSpace(range))
                {
                    request.Headers.TryAddWithoutValidation("Range", range);
                }

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                var status = response.StatusCode == HttpStatusCode.PartialContent ? "206 Partial Content" : "200 OK";
                var builder = new StringBuilder();
                builder.Append($"HTTP/1.1 {status}\r\n");
                builder.Append("Connection: close\r\n");
                builder.Append("Accept-Ranges: bytes\r\n");
                AppendHeader(builder, response, "Content-Type");
                AppendHeader(builder, response, "Content-Length");
                AppendHeader(builder, response, "Content-Range");
                builder.Append("\r\n");
                await stream.WriteAsync(Encoding.UTF8.GetBytes(builder.ToString())).ConfigureAwait(false);

                if (method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) return;
                using var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await body.CopyToAsync(stream).ConfigureAwait(false);
            }
            catch
            {
                // 播放器会自行重试
            }
        }
    }

    private static void AppendHeader(StringBuilder builder, HttpResponseMessage response, string name)
    {
        if (response.Content.Headers.TryGetValues(name, out var values))
        {
            builder.Append(name).Append(": ").Append(string.Join(",", values)).Append("\r\n");
        }
    }

    private static async Task<string> ReadHeadersAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        var text = new StringBuilder();
        while (text.Length < 16 * 1024)
        {
            var read = await stream.ReadAsync(buffer).ConfigureAwait(false);
            if (read <= 0) break;
            text.Append(Encoding.ASCII.GetString(buffer, 0, read));
            if (text.ToString().Contains("\r\n\r\n")) break;
        }

        var raw = text.ToString();
        var end = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        return end >= 0 ? raw[..end] : raw;
    }
}
