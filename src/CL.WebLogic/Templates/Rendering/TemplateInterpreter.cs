using CL.WebLogic.Templates.Ast;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// The reference engine: walks the AST on every render. Slower than
/// <see cref="ClosureTemplateCompiler"/> but the simplest statement of the semantics.
/// </summary>
public sealed class TemplateInterpreter : ITemplateCompiler
{
    /// <inheritdoc/>
    public string Name => "interpreter";

    /// <inheritdoc/>
    public CompiledTemplate Compile(string path, string sourceHash, TemplateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sections = document.Sections.ToDictionary(
            s => s.Name,
            s => (TemplateBlock)(ctx => RenderAsync(ctx, s.Body)),
            StringComparer.OrdinalIgnoreCase);
        return new CompiledTemplate(path, sourceHash, document.LayoutPath, ctx => RenderAsync(ctx, document.Nodes), sections, Name);
    }

    /// <summary>Renders <paramref name="nodes"/> into the context's output.</summary>
    public static async ValueTask RenderAsync(TemplateRenderContext context, IReadOnlyList<TemplateNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LiteralNode literal:
                    context.Output.Append(literal.Text);
                    break;
                case VerbatimNode verbatim:
                    context.Output.Append(verbatim.Text);
                    break;
                case TokenNode token:
                    TemplateOps.WriteValue(context, TemplateOps.Evaluate(context, token.Selector, token.Filters), token.Context, token.Raw);
                    break;
                case KeywordNode keyword:
                    TemplateOps.WriteKeyword(context, keyword.Keyword);
                    break;
                case CsrfNode csrf:
                    TemplateOps.WriteCsrf(context, csrf.Kind);
                    break;
                case AssetNode asset:
                    TemplateOps.WriteAsset(context, asset.Path, asset.Context);
                    break;
                case TextNode text:
                    TemplateOps.WriteText(context, text.Key, text.Context);
                    break;
                case RenderSectionNode section:
                    TemplateOps.WriteSection(context, section.Name);
                    break;
                case IfNode ifNode:
                    await RenderIfAsync(context, ifNode).ConfigureAwait(false);
                    break;
                case SwitchNode switchNode:
                    await RenderSwitchAsync(context, switchNode).ConfigureAwait(false);
                    break;
                case ForeachNode loop:
                    await TemplateOps.ForeachAsync(
                        context, loop.Source, loop.Alias,
                        ctx => RenderAsync(ctx, loop.Body),
                        loop.Empty is null ? null : ctx => RenderAsync(ctx, loop.Empty)).ConfigureAwait(false);
                    break;
                case PartialNode partial:
                    await TemplateOps.RenderPartialAsync(context, partial.Path, partial.Arguments).ConfigureAwait(false);
                    break;
                case WidgetNode widget:
                    await TemplateOps.RenderWidgetAsync(
                        context, widget.Name,
                        widget.Attributes.Select(a => (a.Name, (TemplateBlock)(ctx => RenderAsync(ctx, a.Value)))).ToList()).ConfigureAwait(false);
                    break;
                case FormNode form:
                    await TemplateOps.RenderFormAsync(
                        context, form.Name,
                        form.Attributes.Select(a => (a.Name, (TemplateBlock)(ctx => RenderAsync(ctx, a.Value)))).ToList()).ConfigureAwait(false);
                    break;
                case WidgetAreaNode area:
                    await TemplateOps.RenderWidgetAreaAsync(context, area.Area).ConfigureAwait(false);
                    break;
            }
        }
    }

    private static ValueTask RenderIfAsync(TemplateRenderContext context, IfNode node)
    {
        foreach (var branch in node.Branches)
        {
            if (TemplateOps.Test(context, branch.Condition))
                return RenderAsync(context, branch.Body);
        }
        return node.Else is null ? ValueTask.CompletedTask : RenderAsync(context, node.Else);
    }

    private static ValueTask RenderSwitchAsync(TemplateRenderContext context, SwitchNode node)
    {
        var subject = TemplateValueResolver.Resolve(context, node.Subject);
        foreach (var @case in node.Cases)
        {
            if (TemplateOps.Matches(subject, @case.Value))
                return RenderAsync(context, @case.Body);
        }
        return node.Default is null ? ValueTask.CompletedTask : RenderAsync(context, node.Default);
    }
}
