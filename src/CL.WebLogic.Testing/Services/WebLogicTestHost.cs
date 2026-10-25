using System.Net;
using CL.WebLogic.Hosting.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CL.WebLogic.Testing.Services;

/// <summary>
/// Runs a real Kestrel server on <c>127.0.0.1</c> with a random port and WebLogic wired over a
/// <see cref="FakeWebLogicContext"/>. Requests go over real HTTP, so the full ASP.NET pipeline
/// (routing, CORS, headers, cookies) behaves exactly as in production.
/// </summary>
public sealed class WebLogicTestHost : IAsyncDisposable
{
    private readonly List<HttpClient> _clients = [];
    private readonly bool _ownsContentRoot;

    private WebLogicTestHost(WebApplication app, FakeWebLogicContext context, Uri baseAddress, string contentRoot, bool ownsContentRoot)
    {
        App = app;
        Context = context;
        BaseAddress = baseAddress;
        ContentRootPath = contentRoot;
        _ownsContentRoot = ownsContentRoot;
        Client = CreateClient();
    }

    /// <summary>The running application.</summary>
    public WebApplication App { get; }

    /// <summary>The fake toolkit context (recording logger, private event bus).</summary>
    public FakeWebLogicContext Context { get; }

    /// <summary>Base address of the server, e.g. <c>http://127.0.0.1:51234/</c>.</summary>
    public Uri BaseAddress { get; }

    /// <summary>The content root the app runs with.</summary>
    public string ContentRootPath { get; }

    /// <summary>A client with its own cookie jar that does not follow redirects.</summary>
    public HttpClient Client { get; }

    /// <summary>Application services.</summary>
    public IServiceProvider Services => App.Services;

    /// <summary>Builds and starts a host.</summary>
    public static async Task<WebLogicTestHost> StartAsync(WebLogicTestHostOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new WebLogicTestHostOptions();
        var ownsContentRoot = options.ContentRootPath is null;
        var contentRoot = options.ContentRootPath ?? Path.Combine(Path.GetTempPath(), "weblogic-host", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        var config = options.Config ?? new Configuration.Models.WebLogicConfig();
        if (options.AllowDevelopmentStores)
            config.Security.AllowDevelopmentStores = true;
        var context = new FakeWebLogicContext(config);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = options.EnvironmentName,
            ContentRootPath = contentRoot
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));

        builder.Services.AddCodeLogicWebLogic(context, options.ConfigureWebLogic);
        options.ConfigureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseCodeLogicWebLogic();
        options.ConfigureApp?.Invoke(app);
        app.MapCodeLogicWebLogic();

        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new WebLogicTestHost(app, context, new Uri(address.TrimEnd('/') + "/"), contentRoot, ownsContentRoot);
    }

    /// <summary>Creates another client with its own cookie jar.</summary>
    public HttpClient CreateClient(bool allowAutoRedirect = false)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };
        var client = new HttpClient(handler) { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(30) };
        _clients.Add(client);
        return client;
    }

    /// <summary>Writes a file under the content root (e.g. a theme asset) and returns its full path.</summary>
    public string WriteContentFile(string relativePath, string content)
    {
        var path = Path.Combine(ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Stops the server and cleans up.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
            client.Dispose();

        await App.StopAsync().ConfigureAwait(false);
        await App.DisposeAsync().ConfigureAwait(false);
        Context.Dispose();

        if (_ownsContentRoot)
        {
            try { Directory.Delete(ContentRootPath, recursive: true); }
            catch (IOException) { /* best effort */ }
        }
    }
}
