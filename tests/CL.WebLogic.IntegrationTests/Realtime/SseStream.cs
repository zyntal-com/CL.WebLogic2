using System.Net.Http.Headers;
using System.Text.Json;

namespace CL.WebLogic.IntegrationTests.Realtime;

/// <summary>Reads a server-sent event stream in tests.</summary>
internal sealed class SseStream : IAsyncDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly StreamReader _reader;

    private SseStream(HttpResponseMessage response, StreamReader reader)
    {
        _response = response;
        _reader = reader;
    }

    public HttpResponseMessage Response => _response;

    public static async Task<SseStream> OpenAsync(HttpClient client, string url, string? lastEventId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (lastEventId is not null)
            request.Headers.Add("Last-Event-ID", lastEventId);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        return new SseStream(response, reader);
    }

    /// <summary>The next event (id and data), or null when the stream ended or nothing came in time.</summary>
    public async Task<(string? Id, JsonElement Data)?> NextAsync(TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        string? id = null;
        string? data = null;
        try
        {
            while (true)
            {
                var line = await _reader.ReadLineAsync(cts.Token);
                if (line is null)
                    return null;
                if (line.Length == 0)
                {
                    if (data is not null)
                        return (id, JsonDocument.Parse(data).RootElement.Clone());
                    continue;
                }
                if (line.StartsWith(':'))
                    continue;
                if (line.StartsWith("id: ", StringComparison.Ordinal))
                    id = line[4..];
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                    data = line[6..];
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Types of the events until (and including) <paramref name="marker"/>.</summary>
    public async Task<List<string>> TypesUntilAsync(string marker)
    {
        var types = new List<string>();
        while (await NextAsync() is { } next)
        {
            var type = next.Data.GetProperty("type").GetString()!;
            types.Add(type);
            if (type == marker)
                break;
        }
        return types;
    }

    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        _response.Dispose();
        return ValueTask.CompletedTask;
    }
}
