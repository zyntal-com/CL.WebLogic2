using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace CL.WebLogic.LoadTest;

/// <summary>Runs closed-loop workers against one URL and reports throughput and latency percentiles.</summary>
internal static class LoadTestRun
{
    public static async Task<string> RunAsync(HttpClient client, Uri url, LoadTestOptions options)
    {
        await RunWorkersAsync(client, url, options.Concurrency, options.Warmup, record: null).ConfigureAwait(false);

        var latencies = new List<double>[options.Concurrency];
        var statuses = new Dictionary<int, int>();
        var errors = 0;
        var watch = Stopwatch.StartNew();
        await RunWorkersAsync(client, url, options.Concurrency, options.Duration, (worker, ms, status) =>
        {
            (latencies[worker] ??= []).Add(ms);
            lock (statuses)
            {
                if (status == 0) errors++;
                else statuses[status] = statuses.GetValueOrDefault(status) + 1;
            }
        }).ConfigureAwait(false);
        var elapsed = watch.Elapsed.TotalSeconds;

        var all = latencies.Where(l => l is not null).SelectMany(l => l).Order().ToArray();
        string P(double q) => all.Length == 0 ? "-" : all[Math.Min(all.Length - 1, (int)(q * all.Length))].ToString("F2", CultureInfo.InvariantCulture);
        var codes = string.Join(" ", statuses.OrderBy(s => s.Key).Select(s => $"{s.Key}x{s.Value}"));
        return string.Create(CultureInfo.InvariantCulture,
            $"{url.PathAndQuery,-32} {all.Length / elapsed,9:F0} req/s  p50 {P(0.50),7} ms  p95 {P(0.95),7} ms  p99 {P(0.99),7} ms  [{codes}{(errors > 0 ? $" errors x{errors}" : "")}]");
    }

    private static Task RunWorkersAsync(HttpClient client, Uri url, int concurrency, TimeSpan duration, Action<int, double, int>? record)
    {
        var until = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
        return Task.WhenAll(Enumerable.Range(0, concurrency).Select(worker => Task.Run(async () =>
        {
            var buffer = new byte[64 * 1024];
            while (Stopwatch.GetTimestamp() < until)
            {
                var started = Stopwatch.GetTimestamp();
                int status;
                try
                {
                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    await using var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    while (await body.ReadAsync(buffer).ConfigureAwait(false) > 0) { }
                    status = (int)response.StatusCode;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                {
                    status = 0;
                }
                record?.Invoke(worker, Stopwatch.GetElapsedTime(started).TotalMilliseconds, status);
            }
        })));
    }

    public static HttpClient CreateClient(int concurrency) => new(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = concurrency,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    { Timeout = TimeSpan.FromSeconds(30) };
}
