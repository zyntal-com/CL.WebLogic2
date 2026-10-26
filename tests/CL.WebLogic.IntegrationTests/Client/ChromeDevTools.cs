using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace CL.WebLogic.IntegrationTests.Client;

/// <summary>
/// Headless Chrome driven over the DevTools protocol (shared framework only: a process and a
/// ClientWebSocket). Enough for virtual WebAuthn authenticators, navigation and evaluation.
/// </summary>
internal sealed class ChromeDevTools : IAsyncDisposable
{
    private readonly Process _process;
    private readonly ClientWebSocket _socket;
    private readonly string _profile;
    private int _nextId;

    private ChromeDevTools(Process process, ClientWebSocket socket, string profile)
    {
        _process = process;
        _socket = socket;
        _profile = profile;
    }

    /// <summary>Starts <paramref name="browser"/> headless and connects to it.</summary>
    public static async Task<ChromeDevTools> LaunchAsync(string browser)
    {
        var profile = Path.Combine(Path.GetTempPath(), "wl-cdp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        var start = new ProcessStartInfo(browser) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in (string[])["--headless=new", "--disable-gpu", "--no-sandbox", "--no-first-run", "--no-default-browser-check",
                     "--remote-debugging-port=0", "--user-data-dir=" + profile, "about:blank"])
            start.ArgumentList.Add(argument);
        var process = Process.Start(start)!;
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        // Chrome writes "port\n/devtools/browser/<id>" once it listens.
        var portFile = Path.Combine(profile, "DevToolsActivePort");
        string[]? lines = null;
        for (var i = 0; i < 150 && lines is not { Length: >= 2 }; i++)
        {
            await Task.Delay(100);
            try
            {
                lines = File.Exists(portFile) ? await File.ReadAllLinesAsync(portFile) : null;
            }
            catch (IOException)
            {
                lines = null; // still being written
            }
        }
        if (lines is not { Length: >= 2 })
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("Chrome did not open a DevTools port.");
        }

        var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{lines[0].Trim()}{lines[1].Trim()}"), CancellationToken.None);
        return new ChromeDevTools(process, socket, profile);
    }

    /// <summary>Sends a command (to a page when <paramref name="sessionId"/> is set) and returns its result.</summary>
    public async Task<JsonElement> SendAsync(string method, object? parameters = null, string? sessionId = null)
    {
        var id = Interlocked.Increment(ref _nextId);
        var message = new Dictionary<string, object?> { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new { } };
        if (sessionId is not null)
            message["sessionId"] = sessionId;
        await _socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, true, CancellationToken.None);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            using var reply = JsonDocument.Parse(await ReceiveAsync(timeout.Token));
            var root = reply.RootElement;
            if (!root.TryGetProperty("id", out var replyId) || replyId.GetInt32() != id)
                continue; // an event or another reply
            if (root.TryGetProperty("error", out var error))
                throw new InvalidOperationException($"{method}: {error.GetRawText()}");
            return root.GetProperty("result").Clone();
        }
    }

    /// <summary>Evaluates <paramref name="expression"/> in the page and returns the value.</summary>
    public async Task<JsonElement> EvaluateAsync(string sessionId, string expression)
    {
        var result = await SendAsync("Runtime.evaluate", new { expression, returnByValue = true }, sessionId);
        return result.GetProperty("result").TryGetProperty("value", out var value) ? value.Clone() : default;
    }

    private async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        WebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(chunk, cancellationToken);
            buffer.Write(chunk, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return buffer.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await SendAsync("Browser.close");
        }
        catch (Exception ex) when (ex is InvalidOperationException or WebSocketException or OperationCanceledException)
        {
            // already gone
        }
        _socket.Dispose();
        if (!_process.WaitForExit(5000))
            _process.Kill(entireProcessTree: true);
        _process.Dispose();
        for (var i = 0; i < 10; i++)
        {
            try
            {
                Directory.Delete(_profile, recursive: true);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(200); // the browser may still hold files for a moment
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(200);
            }
        }
    }
}
