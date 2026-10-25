using CL.WebLogic.Runtime.Services;
using CL.WebLogic.Templates.Ast;
using CL.WebLogic.Templates.Escaping;
using System.Text.Encodings.Web;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// Execution semantics shared by every engine (interpreter, closure compiler, generated code):
/// value evaluation, encoding, conditions and the built-in directives. Engines differ only in
/// how they dispatch to these operations, so their output is identical by construction.
/// </summary>
public static class TemplateOps
{
    /// <summary>Maximum nesting of partials and layouts.</summary>
    public const int MaxDepth = 32;

    /// <summary>Resolves <paramref name="selector"/> and applies <paramref name="filters"/>.</summary>
    public static object? Evaluate(TemplateRenderContext context, TemplateSelector selector, IReadOnlyList<TemplateFilter> filters)
    {
        var value = TemplateValueResolver.Resolve(context, selector);
        for (var i = 0; i < filters.Count; i++)
            value = context.Services.Filters.Apply(filters[i].Name, value, filters[i].Arg, context);
        return value;
    }

    /// <summary>Writes <paramref name="value"/> encoded for <paramref name="encoding"/> (or as-is when <paramref name="raw"/>).</summary>
    public static void WriteValue(TemplateRenderContext context, object? value, TemplateEncodingContext encoding, bool raw)
    {
        var output = context.Output;
        if (raw)
        {
            output.Append(TemplateValues.ToText(value));
            return;
        }

        switch (value)
        {
            case null:
                return;
            case WebHtml html when encoding == TemplateEncodingContext.Html:
                output.Append(html.Html);
                return;
            case EncodedValue encoded when encoded.Context == encoding:
                output.Append(encoded.Text);
                return;
            default:
                output.Append(TemplateEncoder.Encode(TemplateValues.ToText(value), encoding));
                return;
        }
    }

    /// <summary>Evaluates a condition.</summary>
    public static bool Test(TemplateRenderContext context, TemplateCondition condition)
    {
        var user = context.HttpContext?.User;
        return condition switch
        {
            NotCondition not => !Test(context, not.Inner),
            AuthCondition => user?.Identity?.IsAuthenticated == true,
            GroupCondition group => context.Services.Authorizer.IsInAnyGroup(context.HttpContext, group.Groups),
            PermissionCondition permission => context.Services.Authorizer.HasPermission(context.HttpContext, permission.Permission),
            TruthyCondition truthy => TemplateValues.Truthy(TemplateValueResolver.Resolve(context, truthy.Selector)),
            CompareCondition compare => TemplateValues.Compare(
                TemplateValueResolver.Resolve(context, compare.Left),
                compare.Op,
                compare.Right.IsLiteral ? compare.Right.Literal : TemplateValueResolver.Resolve(context, compare.Right.Selector!)),
            _ => false
        };
    }

    /// <summary>True when the switch subject equals <paramref name="caseValue"/> (case-insensitive text comparison).</summary>
    public static bool Matches(object? subject, string caseValue) =>
        string.Equals(TemplateValues.ToText(subject), caseValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs <paramref name="body"/> once per item of <paramref name="source"/>, or <paramref name="empty"/> when there are none.</summary>
    public static async ValueTask ForeachAsync(TemplateRenderContext context, TemplateSelector source, string? alias, TemplateBlock body, TemplateBlock? empty)
    {
        var items = TemplateValues.AsItems(TemplateValueResolver.Resolve(context, source));
        if (items.Count == 0)
        {
            if (empty is not null)
                await empty(context).ConfigureAwait(false);
            return;
        }

        var savedItem = context.CurrentItem;
        var savedLoop = context.Loop;
        var savedAliases = context.Aliases;
        Dictionary<string, object?>? aliases = null;
        if (alias is not null)
        {
            aliases = savedAliases is null
                ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object?>(savedAliases, StringComparer.OrdinalIgnoreCase);
            context.Aliases = aliases;
        }

        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                context.CurrentItem = items[i];
                context.Loop = new TemplateLoopState(i, items.Count);
                if (aliases is not null)
                    aliases[alias!] = items[i];
                await body(context).ConfigureAwait(false);
            }
        }
        finally
        {
            context.CurrentItem = savedItem;
            context.Loop = savedLoop;
            context.Aliases = savedAliases;
        }
    }

    /// <summary>Renders a partial with its arguments as <c>{param:…}</c> values. A partial's own layout is ignored.</summary>
    public static async ValueTask RenderPartialAsync(TemplateRenderContext context, string path, IReadOnlyList<PartialArgument> arguments)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in arguments)
            parameters[argument.Name] = argument.IsLiteral ? argument.Literal : TemplateValueResolver.Resolve(context, argument.Selector!);

        var normalized = TemplatePath.Normalize(path, context.TemplatePath);
        var partial = await context.Services.Loader.LoadAsync(normalized, context.CancellationToken).ConfigureAwait(false);

        var savedPath = context.TemplatePath;
        var savedParams = context.Params;
        Enter(context, normalized);
        try
        {
            context.TemplatePath = normalized;
            context.Params = parameters;
            await partial.Body(context).ConfigureAwait(false);
        }
        finally
        {
            Leave(context, normalized);
            context.TemplatePath = savedPath;
            context.Params = savedParams;
        }
    }

    /// <summary>Renders a widget; attribute values are rendered as plain text first.</summary>
    public static async ValueTask RenderWidgetAsync(TemplateRenderContext context, string name, IReadOnlyList<(string Name, TemplateBlock Value)> attributes)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (attributeName, value) in attributes)
            resolved[attributeName] = await RenderToStringAsync(context, value).ConfigureAwait(false);

        await context.Services.Widgets.RenderWidgetAsync(context, name, resolved).ConfigureAwait(false);
    }

    /// <summary>Renders a form; attribute values are rendered as plain text first.</summary>
    public static async ValueTask RenderFormAsync(TemplateRenderContext context, string name, IReadOnlyList<(string Name, TemplateBlock Value)> attributes)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (attributeName, value) in attributes)
            resolved[attributeName] = await RenderToStringAsync(context, value).ConfigureAwait(false);

        await context.Services.Forms.RenderFormAsync(context, name, resolved).ConfigureAwait(false);
    }

    /// <summary>Renders a widget area.</summary>
    public static ValueTask RenderWidgetAreaAsync(TemplateRenderContext context, string area) =>
        new(context.Services.Widgets.RenderAreaAsync(context, area));

    /// <summary>Renders <paramref name="block"/> into a string instead of the current output.</summary>
    public static async ValueTask<string> RenderToStringAsync(TemplateRenderContext context, TemplateBlock block)
    {
        var saved = context.Output;
        context.Output = new System.Text.StringBuilder();
        try
        {
            await block(context).ConfigureAwait(false);
            return context.Output.ToString();
        }
        finally
        {
            context.Output = saved;
        }
    }

    /// <summary>Writes an engine keyword.</summary>
    public static void WriteKeyword(TemplateRenderContext context, TemplateKeyword keyword)
    {
        var output = context.Output;
        switch (keyword)
        {
            case TemplateKeyword.RenderBody:
                output.Append(context.RenderBody);
                break;
            case TemplateKeyword.RenderHead:
                output.Append(WebPageHeadRenderer.Render(
                    context.Meta,
                    TemplateValues.ToText(TemplatePathWalker.Resolve(context.Model, "page_title")),
                    TemplateValues.ToText(TemplatePathWalker.Resolve(context.Model, "page_description")),
                    context.Nonce));
                break;
            case TemplateKeyword.Nonce:
                output.Append(TemplateEncoder.Html.Encode(context.Nonce));
                break;
            case TemplateKeyword.NonceAttribute:
                var nonce = context.Nonce;
                if (nonce.Length > 0)
                    output.Append(" nonce=\"").Append(TemplateEncoder.Html.Encode(nonce)).Append('"');
                break;
            case TemplateKeyword.WebLogicScripts:
                output.Append(context.Services.Scripts.GetScriptTags(context.HttpContext, context.Nonce));
                break;
        }
    }

    /// <summary>Writes CSRF markup.</summary>
    public static void WriteCsrf(TemplateRenderContext context, CsrfKind kind)
    {
        var token = context.HttpContext is null ? string.Empty : context.Services.Csrf.GetToken(context.HttpContext);
        var encoded = TemplateEncoder.Html.Encode(token);
        switch (kind)
        {
            case CsrfKind.Field:
                if (token.Length > 0)
                    context.Output.Append("<input type=\"hidden\" name=\"").Append(TemplateEncoder.Html.Encode(context.Services.Csrf.FieldName))
                        .Append("\" value=\"").Append(encoded).Append("\">");
                break;
            case CsrfKind.Meta:
                if (token.Length > 0)
                    context.Output.Append("<meta name=\"csrf-token\" content=\"").Append(encoded).Append("\">");
                break;
            default:
                context.Output.Append(encoded);
                break;
        }
    }

    /// <summary>Writes the URL of a theme asset.</summary>
    public static void WriteAsset(TemplateRenderContext context, string path, TemplateEncodingContext encoding)
    {
        var url = context.Services.Assets.GetUrl(path);
        if (url.StartsWith('/') && context.HttpContext?.Request.PathBase.HasValue == true)
            url = context.HttpContext.Request.PathBase.Value + url;
        context.Output.Append(TemplateEncoder.Encode(url, encoding));
    }

    /// <summary>Writes a localized text.</summary>
    public static void WriteText(TemplateRenderContext context, string key, TemplateEncodingContext encoding) =>
        context.Output.Append(TemplateEncoder.Encode(context.Services.Localizer.GetText(key), encoding));

    /// <summary>Writes a section captured from the child template.</summary>
    public static void WriteSection(TemplateRenderContext context, string name)
    {
        if (context.Sections.TryGetValue(name, out var html))
            context.Output.Append(html);
    }

    /// <summary>Pushes <paramref name="path"/> on the render stack; throws on recursion or excessive depth.</summary>
    public static void Enter(TemplateRenderContext context, string path)
    {
        if (context.Stack.Count >= MaxDepth)
            throw new TemplateRenderException($"Template nesting exceeds {MaxDepth} levels.", path);
        if (!context.Stack.Add(path))
            throw new TemplateRenderException("Template includes itself recursively.", path);
    }

    /// <summary>Pops <paramref name="path"/> from the render stack.</summary>
    public static void Leave(TemplateRenderContext context, string path) => context.Stack.Remove(path);
}
