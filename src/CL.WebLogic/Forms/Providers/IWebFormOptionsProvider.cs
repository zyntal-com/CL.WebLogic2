using CL.WebLogic.Forms.Models;

namespace CL.WebLogic.Forms.Providers;

/// <summary>
/// Supplies options for select/radio/checkbox fields and suggestions for autocomplete fields,
/// by name. The options endpoint (<c>/_weblogic/forms/options/{name}</c>) requires a signed-in
/// caller unless <see cref="AllowAnonymous"/>, and the <see cref="Policy"/> when set. Bound values
/// of select-like fields are checked with <see cref="IsValidAsync"/>.
/// </summary>
public interface IWebFormOptionsProvider
{
    /// <summary>Provider name.</summary>
    string Name { get; }

    /// <summary>Anonymous callers may read the options.</summary>
    bool AllowAnonymous => false;

    /// <summary>Authorization policy required to read the options.</summary>
    string? Policy => null;

    /// <summary>All options (for selects).</summary>
    ValueTask<IReadOnlyList<WebFieldOption>> GetOptionsAsync(WebFormOptionsRequest request, CancellationToken cancellationToken);

    /// <summary>Options matching <paramref name="term"/> (autocomplete; at most 50).</summary>
    ValueTask<IReadOnlyList<WebFieldOption>> SearchAsync(string term, WebFormOptionsRequest request, CancellationToken cancellationToken);

    /// <summary>True when <paramref name="value"/> is an allowed choice.</summary>
    ValueTask<bool> IsValidAsync(string value, WebFormOptionsRequest request, CancellationToken cancellationToken);
}
