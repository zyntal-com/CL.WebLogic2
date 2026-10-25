namespace Samples.Hosting;

/// <summary>
/// <c>dotnet App.dll --probe [url]</c>: a container health check without curl in the image.
/// Exits 0 when the URL answers 2xx within five seconds.
/// </summary>
public static class HealthProbe
{
    /// <summary>The liveness endpoint of the app inside the container.</summary>
    public const string DefaultUrl = "http://127.0.0.1:8080/_weblogic/health/live";

    /// <summary>True when the process was started as a probe.</summary>
    public static bool IsProbe(string[] args) => args.Length > 0 && args[0] == "--probe";

    /// <summary>Runs the probe; returns the exit code.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        var url = args.Length > 1 ? args[1] : DefaultUrl;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync(new Uri(url)).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await Console.Error.WriteLineAsync($"probe failed: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }
}
