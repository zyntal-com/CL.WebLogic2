using CL.WebLogic.Templates.Parsing;
using CL.WebLogic.Templates.Rendering;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.Tests.Templates;

public sealed class FormDirectiveTests
{
    private static TemplateTestRenderer Renderer(string engine, ITemplateFormRenderer forms) =>
        new(engine == "closure" ? new ClosureTemplateCompiler() : new TemplateInterpreter()) { Forms = forms };

    [Theory]
    [InlineData("closure")]
    [InlineData("interpreter")]
    public async Task Form_directive_passes_resolved_attributes(string engine)
    {
        var forms = new RecordingFormRenderer();
        var html = await Renderer(engine, forms).RenderSourceAsync("<main>{form:contact submit=\"Send {model:who}\" class=\"wide\"}</main>", new { who = "<now>" });

        Assert.Equal("<main><form contact></main>", html);
        Assert.Equal("contact", forms.Name);
        Assert.Equal("Send <now>", forms.Attributes!["submit"]); // plain text: the renderer encodes
        Assert.Equal("wide", forms.Attributes["class"]);
    }

    [Theory]
    [InlineData("closure")]
    [InlineData("interpreter")]
    public async Task Without_a_form_renderer_a_comment_is_written(string engine)
    {
        var html = await Renderer(engine, NullTemplateFormRenderer.Instance).RenderSourceAsync("{form:a<b}");
        Assert.Equal("<!-- form not available: a&lt;b -->", html);
    }

    private sealed class RecordingFormRenderer : ITemplateFormRenderer
    {
        public string? Name { get; private set; }

        public IReadOnlyDictionary<string, string>? Attributes { get; private set; }

        public Task RenderFormAsync(TemplateRenderContext context, string name, IReadOnlyDictionary<string, string> attributes)
        {
            Name = name;
            Attributes = attributes;
            context.Output.Append("<form ").Append(name).Append('>');
            return Task.CompletedTask;
        }
    }
}
