namespace CL.WebLogic.Forms.Rendering;

/// <summary>Renders a form to HTML. Built in: <see cref="Bootstrap5FormRenderer"/> and <see cref="PlainFormRenderer"/>.</summary>
public interface IWebFormRenderer
{
    /// <summary>Renderer name (<c>bootstrap5</c>, <c>plain</c>, …).</summary>
    string Name { get; }

    /// <summary>Renders the form.</summary>
    ValueTask<string> RenderAsync(WebFormRenderRequest request, CancellationToken cancellationToken);
}
