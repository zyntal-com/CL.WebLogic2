using CL.WebLogic.Forms.Models;

namespace CL.WebLogic.Forms.Providers;

/// <summary>A provider over a fixed list (e.g. countries).</summary>
public sealed class StaticOptionsProvider(string name, IReadOnlyList<WebFieldOption> options, bool allowAnonymous = true) : WebFormOptionsProvider
{
    /// <inheritdoc/>
    public override string Name => name;

    /// <inheritdoc/>
    public override bool AllowAnonymous => allowAnonymous;

    /// <inheritdoc/>
    public override ValueTask<IReadOnlyList<WebFieldOption>> GetOptionsAsync(WebFormOptionsRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(options);
}
