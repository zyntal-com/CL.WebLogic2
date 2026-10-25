namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// A custom template filter (<c>{model:x|myfilter:arg}</c>). Register implementations with
/// <c>web.AddTemplateFilter&lt;T&gt;()</c>. Return a <see cref="Escaping.WebHtml"/> or
/// <see cref="Escaping.EncodedValue"/> only for markup you produced safely.
/// </summary>
public interface ITemplateFilter
{
    /// <summary>Filter name used in templates (case-insensitive).</summary>
    string Name { get; }

    /// <summary>Transforms <paramref name="value"/>.</summary>
    object? Apply(object? value, string? argument, TemplateRenderContext context);
}
