using CL.WebLogic.Forms.Models;

namespace CL.WebLogic.Forms.Providers;

/// <summary>Base class: implement <see cref="GetOptionsAsync"/>; search and validity are derived from it.</summary>
public abstract class WebFormOptionsProvider : IWebFormOptionsProvider
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public virtual bool AllowAnonymous => false;

    /// <inheritdoc/>
    public virtual string? Policy => null;

    /// <inheritdoc/>
    public abstract ValueTask<IReadOnlyList<WebFieldOption>> GetOptionsAsync(WebFormOptionsRequest request, CancellationToken cancellationToken);

    /// <inheritdoc/>
    public virtual async ValueTask<IReadOnlyList<WebFieldOption>> SearchAsync(string term, WebFormOptionsRequest request, CancellationToken cancellationToken)
    {
        var all = await GetOptionsAsync(request, cancellationToken).ConfigureAwait(false);
        return [.. all.Where(o => o.Label.Contains(term, StringComparison.CurrentCultureIgnoreCase) || o.Value.Contains(term, StringComparison.OrdinalIgnoreCase)).Take(50)];
    }

    /// <inheritdoc/>
    public virtual async ValueTask<bool> IsValidAsync(string value, WebFormOptionsRequest request, CancellationToken cancellationToken)
    {
        var all = await GetOptionsAsync(request, cancellationToken).ConfigureAwait(false);
        return all.Any(o => !o.Disabled && string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));
    }
}
