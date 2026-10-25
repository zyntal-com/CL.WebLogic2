using CL.WebLogic.Templates.Ast;

namespace CL.WebLogic.Templates.Rendering;

/// <summary>
/// Default engine: compiles the AST once into a tree of <see cref="TemplateBlock"/> delegates.
/// Adjacent literals are merged and synchronous steps run without allocating async state
/// machines; an await only happens when a partial, widget or loop body actually suspends.
/// </summary>
public sealed class ClosureTemplateCompiler : ITemplateCompiler
{
    private static readonly TemplateBlock Empty = _ => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public string Name => "closure";

    /// <inheritdoc/>
    public CompiledTemplate Compile(string path, string sourceHash, TemplateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sections = document.Sections.ToDictionary(s => s.Name, s => CompileNodes(s.Body), StringComparer.OrdinalIgnoreCase);
        return new CompiledTemplate(path, sourceHash, document.LayoutPath, CompileNodes(document.Nodes), sections, Name);
    }

    /// <summary>Compiles a node list into one block.</summary>
    public static TemplateBlock CompileNodes(IReadOnlyList<TemplateNode> nodes)
    {
        var steps = new List<TemplateBlock>(nodes.Count);
        var pending = new System.Text.StringBuilder();

        foreach (var node in nodes)
        {
            if (node is LiteralNode or VerbatimNode)
            {
                pending.Append(node is LiteralNode l ? l.Text : ((VerbatimNode)node).Text);
                continue;
            }

            FlushLiteral();
            steps.Add(CompileNode(node));
        }

        FlushLiteral();
        return Sequence(steps.ToArray());

        void FlushLiteral()
        {
            if (pending.Length == 0)
                return;
            var text = pending.ToString();
            pending.Clear();
            steps.Add(ctx =>
            {
                ctx.Output.Append(text);
                return ValueTask.CompletedTask;
            });
        }
    }

    private static TemplateBlock CompileNode(TemplateNode node)
    {
        switch (node)
        {
            case TokenNode token:
                var (selector, filters, encoding, raw) = (token.Selector, token.Filters, token.Context, token.Raw);
                return Sync(ctx => TemplateOps.WriteValue(ctx, TemplateOps.Evaluate(ctx, selector, filters), encoding, raw));
            case KeywordNode keyword:
                var kind = keyword.Keyword;
                return Sync(ctx => TemplateOps.WriteKeyword(ctx, kind));
            case CsrfNode csrf:
                var csrfKind = csrf.Kind;
                return Sync(ctx => TemplateOps.WriteCsrf(ctx, csrfKind));
            case AssetNode asset:
                var (assetPath, assetEncoding) = (asset.Path, asset.Context);
                return Sync(ctx => TemplateOps.WriteAsset(ctx, assetPath, assetEncoding));
            case TextNode text:
                var (key, textEncoding) = (text.Key, text.Context);
                return Sync(ctx => TemplateOps.WriteText(ctx, key, textEncoding));
            case RenderSectionNode section:
                var sectionName = section.Name;
                return Sync(ctx => TemplateOps.WriteSection(ctx, sectionName));
            case IfNode ifNode:
                return CompileIf(ifNode);
            case SwitchNode switchNode:
                return CompileSwitch(switchNode);
            case ForeachNode loop:
                var (source, alias) = (loop.Source, loop.Alias);
                var body = CompileNodes(loop.Body);
                var empty = loop.Empty is null ? null : CompileNodes(loop.Empty);
                return ctx => TemplateOps.ForeachAsync(ctx, source, alias, body, empty);
            case PartialNode partial:
                var (partialPath, arguments) = (partial.Path, partial.Arguments);
                return ctx => TemplateOps.RenderPartialAsync(ctx, partialPath, arguments);
            case WidgetNode widget:
                var widgetName = widget.Name;
                var attributes = widget.Attributes.Select(a => (a.Name, CompileNodes(a.Value))).ToList();
                return ctx => TemplateOps.RenderWidgetAsync(ctx, widgetName, attributes);
            case FormNode form:
                var formName = form.Name;
                var formAttributes = form.Attributes.Select(a => (a.Name, CompileNodes(a.Value))).ToList();
                return ctx => TemplateOps.RenderFormAsync(ctx, formName, formAttributes);
            case WidgetAreaNode area:
                var areaName = area.Area;
                return ctx => TemplateOps.RenderWidgetAreaAsync(ctx, areaName);
            default:
                return Empty;
        }
    }

    private static TemplateBlock CompileIf(IfNode node)
    {
        var branches = node.Branches.Select(b => (b.Condition, Block: CompileNodes(b.Body))).ToArray();
        var otherwise = node.Else is null ? Empty : CompileNodes(node.Else);
        return ctx =>
        {
            foreach (var (condition, block) in branches)
            {
                if (TemplateOps.Test(ctx, condition))
                    return block(ctx);
            }
            return otherwise(ctx);
        };
    }

    private static TemplateBlock CompileSwitch(SwitchNode node)
    {
        var subject = node.Subject;
        var cases = node.Cases.Select(c => (c.Value, Block: CompileNodes(c.Body))).ToArray();
        var fallback = node.Default is null ? Empty : CompileNodes(node.Default);
        return ctx =>
        {
            var value = TemplateValueResolver.Resolve(ctx, subject);
            foreach (var (caseValue, block) in cases)
            {
                if (TemplateOps.Matches(value, caseValue))
                    return block(ctx);
            }
            return fallback(ctx);
        };
    }

    private static TemplateBlock Sync(Action<TemplateRenderContext> action) => ctx =>
    {
        action(ctx);
        return ValueTask.CompletedTask;
    };

    private static TemplateBlock Sequence(TemplateBlock[] steps) => steps.Length switch
    {
        0 => Empty,
        1 => steps[0],
        _ => ctx =>
        {
            for (var i = 0; i < steps.Length; i++)
            {
                var pending = steps[i](ctx);
                if (!pending.IsCompletedSuccessfully)
                    return ContinueAsync(pending, steps, i + 1, ctx);
            }
            return ValueTask.CompletedTask;
        }
    };

    private static async ValueTask ContinueAsync(ValueTask pending, TemplateBlock[] steps, int next, TemplateRenderContext context)
    {
        await pending.ConfigureAwait(false);
        for (var i = next; i < steps.Length; i++)
            await steps[i](context).ConfigureAwait(false);
    }
}
