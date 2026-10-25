using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Stops the CodeLogic runtime when the ASP.NET host shuts down (opt-in through
/// <c>web.StopCodeLogicOnShutdown()</c>). Use it instead of calling <c>CodeLogic.StopAsync()</c>
/// after <c>app.Run()</c> — never both.
/// </summary>
public sealed class CodeLogicLifetimeBridge : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => CodeLogic.CodeLogic.StopAsync();
}
