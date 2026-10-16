using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace StampMitra.Tests;

/// <summary>
/// Starts the spec repo's Node mock server (scripts/mock-server.mjs). The spec directory comes from STAMPMITRA_SPEC_DIR
/// (default /home/user/stampmitra-developer-openapi); Node from STAMPMITRA_NODE, PATH or /opt/node22/bin. When either is
/// missing the conformance tests are skipped, unless CONFORMANCE_REQUIRED=1 (set in CI), where they fail instead.
/// </summary>
public sealed class MockServerFixture : IDisposable
{
    private readonly Process? _process;
    private readonly HttpClient _control = new();

    public MockServerFixture()
    {
        var required = Environment.GetEnvironmentVariable("CONFORMANCE_REQUIRED") == "1";
        var specDir = Environment.GetEnvironmentVariable("STAMPMITRA_SPEC_DIR");
        if (string.IsNullOrEmpty(specDir))
        {
            specDir = "/home/user/stampmitra-developer-openapi";
        }

        var script = Path.Combine(specDir, "scripts", "mock-server.mjs");
        var node = FindNode();
        if (!File.Exists(script) || node is null)
        {
            SkipReason = $"mock server unavailable (script: {script}, node: {node ?? "not found"})";
            if (required)
            {
                throw new InvalidOperationException("CONFORMANCE_REQUIRED=1 but " + SkipReason);
            }

            return;
        }

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        BaseUrl = $"http://127.0.0.1:{port}";

        var psi = new ProcessStartInfo(node, $"\"{script}\" --port {port} --host 127.0.0.1")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        _process = Process.Start(psi);
        _process!.OutputDataReceived += (_, _) => { };
        _process.ErrorDataReceived += (_, _) => { };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        for (var i = 0; i < 100; i++)
        {
            try
            {
                using var r = _control.GetAsync(new Uri(BaseUrl + "/__mock/health")).GetAwaiter().GetResult();
                if (r.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // not up yet
            }

            Thread.Sleep(100);
        }

        Dispose();
        SkipReason = "mock server did not start";
        if (required)
        {
            throw new InvalidOperationException(SkipReason);
        }
    }

    public string? SkipReason { get; private set; }

    public string BaseUrl { get; } = string.Empty;

    public void Reset()
    {
        using var r = _control.PostAsync(new Uri(BaseUrl + "/__mock/reset"), new StringContent(string.Empty)).GetAwaiter().GetResult();
        r.EnsureSuccessStatusCode();
    }

    public List<JsonElement> Recorded()
    {
        var text = _control.GetStringAsync(new Uri(BaseUrl + "/__mock/requests")).GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    public void Dispose()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
            // already gone
        }

        _process?.Dispose();
        _control.Dispose();
    }

    private static string? FindNode()
    {
        var env = Environment.GetEnvironmentVariable("STAMPMITRA_NODE");
        if (!string.IsNullOrEmpty(env) && File.Exists(env))
        {
            return env;
        }

        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator).Append("/opt/node22/bin");
        return dirs.Select(d => Path.Combine(d, "node")).FirstOrDefault(File.Exists);
    }
}

[CollectionDefinition("mock")]
public sealed class MockServerCollectionDefinition : ICollectionFixture<MockServerFixture>
{
}

/// <summary>A tiny real HTTP server for bodies the Node mock cannot produce (HTML errors, extra fields, pagination).</summary>
internal sealed class TinyServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<HttpListenerRequest, int, (int Status, string Body, string ContentType, (string, string)[] Headers)> _handler;
    private int _count;

    public TinyServer(Func<HttpListenerRequest, int, (int, string, string, (string, string)[])> handler)
    {
        _handler = handler;
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _ = Task.Run(Loop);
    }

    public string BaseUrl { get; }

    public int Count => _count;

    private async Task Loop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                return;
            }

            var n = Interlocked.Increment(ref _count);
            var (status, body, type, headers) = _handler(ctx.Request, n);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = type;
            foreach (var (k, v) in headers)
            {
                ctx.Response.AddHeader(k, v);
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }
    }

    public void Dispose() => _listener.Close();
}
