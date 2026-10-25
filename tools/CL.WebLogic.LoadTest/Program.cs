using CL.WebLogic.LoadTest;

LoadTestOptions options;
try
{
    options = LoadTestOptions.Parse(args);
}
catch (Exception ex) when (ex is ArgumentException or FormatException or IndexOutOfRangeException)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("usage: CL.WebLogic.LoadTest [--duration 10] [--warmup 2] [--concurrency 32] [--header 'Name: value'] url...");
    return 2;
}

using var client = LoadTestRun.CreateClient(options.Concurrency);
foreach (var (name, value) in options.Headers)
    client.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
Console.WriteLine($"{options.Concurrency} workers, {options.Duration.TotalSeconds:F0} s per URL (after {options.Warmup.TotalSeconds:F0} s warm-up)");
foreach (var url in options.Urls)
    Console.WriteLine(await LoadTestRun.RunAsync(client, url, options).ConfigureAwait(false));
return 0;
