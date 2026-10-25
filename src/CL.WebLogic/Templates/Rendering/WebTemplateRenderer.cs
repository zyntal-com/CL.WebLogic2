using System.Text;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>Default <see cref="IWebTemplateRenderer"/>: resolves layouts, renders sections and the body chain.</summary>
public sealed class WebTemplateRenderer : IWebTemplateRenderer
{
    private readonly TemplateRenderServices _services;
    private readonly IEnumerable<ITemplateGlobalsProvider> _globals;
    private readonly IEnumerable<ITemplateRenderPreparer> _preparers;

    /// <summary>Creates the renderer.</summary>
    public WebTemplateRenderer(TemplateRenderServices services, IEnumerable<ITemplateGlobalsProvider> globals, IEnumerable<ITemplateRenderPreparer>? preparers = null)
    {
        _preparers = preparers ?? [];
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _globals = globals ?? [];
    }

    /// <inheritdoc/>
    public bool Exists(string templatePath) => _services.Loader.Exists(TemplatePath.Normalize(templatePath));

    /// <inheritdoc/>
    public async Task<string> RenderAsync(TemplateRenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = TemplatePath.Normalize(request.TemplatePath);
        var template = await _services.Loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);

        var globals = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in _globals)
            provider.Contribute(globals, request.HttpContext);

        if (request.HttpContext is { } httpContext)
        {
            foreach (var preparer in _preparers)
                await preparer.PrepareAsync(httpContext, cancellationToken).ConfigureAwait(false);
        }

        var context = new TemplateRenderContext
        {
            TemplatePath = path,
            Model = request.Model,
            Globals = globals,
            HttpContext = request.HttpContext,
            Meta = request.Meta,
            Output = new StringBuilder(4096),
            Services = _services,
            CancellationToken = cancellationToken
        };

        if (request.IgnoreLayout)
        {
            TemplateOps.Enter(context, path);
            await template.Body(context).ConfigureAwait(false);
        }
        else
        {
            await RenderWithLayoutsAsync(context, template).ConfigureAwait(false);
        }
        return context.Output.ToString();
    }

    /// <summary>
    /// Renders the template; with a layout, its body and sections are rendered first and handed
    /// to the layout (<c>{renderbody}</c> / <c>{rendersection}</c>), repeating for nested layouts.
    /// Sections defined closer to the page win over sections of the same name further out.
    /// </summary>
    internal static async ValueTask RenderWithLayoutsAsync(TemplateRenderContext context, CompiledTemplate template)
    {
        var current = template;
        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var visited = new List<string>();

        try
        {
            while (current.LayoutPath is not null)
            {
                TemplateOps.Enter(context, current.Path);
                visited.Add(current.Path);
                context.TemplatePath = current.Path;

                var body = await TemplateOps.RenderToStringAsync(context, current.Body).ConfigureAwait(false);
                foreach (var (name, block) in current.Sections)
                {
                    if (!sections.ContainsKey(name))
                        sections[name] = await TemplateOps.RenderToStringAsync(context, block).ConfigureAwait(false);
                }

                context.RenderBody = body;
                context.Sections = sections;
                var layoutPath = TemplatePath.Normalize(current.LayoutPath, current.Path);
                current = await context.Services.Loader.LoadAsync(layoutPath, context.CancellationToken).ConfigureAwait(false);
            }

            TemplateOps.Enter(context, current.Path);
            visited.Add(current.Path);
            context.TemplatePath = current.Path;
            await current.Body(context).ConfigureAwait(false);
        }
        finally
        {
            foreach (var path in visited)
                TemplateOps.Leave(context, path);
        }
    }
}
