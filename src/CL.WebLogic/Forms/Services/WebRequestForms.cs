using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Rendering;
using CL.WebLogic.Templates.Escaping;

namespace CL.WebLogic.Forms.Services;

/// <summary>Forms for the current request (<c>request.Forms</c>).</summary>
public sealed class WebRequestForms
{
    private readonly Runtime.Models.WebRequestContext _request;
    private readonly WebFormService _service;

    internal WebRequestForms(Runtime.Models.WebRequestContext request, WebFormService service)
    {
        _request = request;
        _service = service;
    }

    /// <summary>Binds and validates the posted form.</summary>
    public Task<WebFormSubmission<TModel>> BindAsync<TModel>(CancellationToken cancellationToken = default) =>
        _service.BindAsync<TModel>(_request, Token(cancellationToken));

    /// <summary>The form definition of <typeparamref name="TModel"/>.</summary>
    public WebFormDefinition Definition<TModel>() => _service.Definitions.Get<TModel>();

    /// <summary>Copies values between form models and entities (<see cref="WebFormMapper"/>).</summary>
    public WebFormMapper Mapper => _request.GetRequiredService<WebFormMapper>();

    /// <summary>Shows <paramref name="model"/>'s values when the form renders in this request (edit forms).</summary>
    public void Prefill<TModel>(TModel model) => _service.Prefill(_request.HttpContext, model);

    /// <summary>Renders the form (for page models; templates can use <c>{form:id}</c> instead).</summary>
    public async Task<WebHtml> RenderAsync<TModel>(WebFormRenderOptions? options = null, CancellationToken cancellationToken = default) =>
        new(await _service.RenderAsync(_request, _service.Definitions.Get<TModel>(), null, options, Token(cancellationToken)).ConfigureAwait(false));

    private CancellationToken Token(CancellationToken cancellationToken) =>
        cancellationToken.CanBeCanceled ? cancellationToken : _request.HttpContext.RequestAborted;
}
