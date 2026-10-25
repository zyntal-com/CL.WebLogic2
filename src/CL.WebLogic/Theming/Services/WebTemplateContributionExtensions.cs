using System.Reflection;
using System.Text;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Templates.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Lets contributors and plugins ship their own templates (pages, partials, widget and form
/// templates). They are used when the active theme has no file at the same path, and removed
/// with the contribution.
/// </summary>
public static class WebTemplateContributionExtensions
{
    /// <summary>
    /// Registers every <c>.html</c> embedded resource of <paramref name="assembly"/> whose name
    /// starts with <paramref name="resourcePrefix"/>; the rest of the name is the template path.
    /// Embed them with a logical name that keeps the folders:
    /// <code>&lt;EmbeddedResource Include="theme\**\*.html" LogicalName="theme/%(RecursiveDir)%(Filename)%(Extension)" /&gt;</code>
    /// </summary>
    public static IReadOnlyList<string> MapTemplates(this WebContributionBuilder builder, Assembly assembly, string resourcePrefix = "theme/")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(resourcePrefix);

        var prefix = resourcePrefix.Replace('\\', '/');
        var templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var logical = name.Replace('\\', '/');
            if (!logical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !logical.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                continue;
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            templates[TemplatePath.Normalize("/" + logical[prefix.Length..])] = reader.ReadToEnd();
        }
        if (templates.Count == 0)
            throw new InvalidOperationException(
                $"{assembly.GetName().Name} has no embedded templates under '{prefix}'. Embed them with " +
                @"<EmbeddedResource Include=""theme\**\*.html"" LogicalName=""theme/%(RecursiveDir)%(Filename)%(Extension)"" />.");
        Register(builder, templates);
        return templates.Keys.ToList();
    }

    /// <summary>Registers one template from source.</summary>
    public static string MapTemplate(this WebContributionBuilder builder, string path, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);
        var normalized = TemplatePath.Normalize("/" + path.TrimStart('/'));
        Register(builder, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [normalized] = source });
        return normalized;
    }

    private static void Register(WebContributionBuilder builder, Dictionary<string, string> templates)
    {
        var registry = builder.Services.GetRequiredService<WebContributedTemplates>();
        var owner = builder.Contributor;
        builder.OnCommit(() => registry.Add(owner, templates));
        builder.OnRemove(() =>
        {
            registry.Remove(owner);
            return ValueTask.CompletedTask;
        });
    }
}
