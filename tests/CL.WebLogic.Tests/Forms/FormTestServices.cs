using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Content.Services;
using CL.WebLogic.Forms.AntiBot;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Rendering;
using CL.WebLogic.Forms.Services;
using CL.WebLogic.Forms.Validation;
using CL.WebLogic.Localization.Services;
using CL.WebLogic.Security.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace CL.WebLogic.Tests.Forms;

internal static class FormTestServices
{
    public static WebFormMessages Messages { get; } = new(new PassthroughTextLocalizer());

    public static WebFormBinder Binder { get; } = new(Messages, new WebHtmlSanitizer());

    public static WebFieldValidator Validator { get; } = new(Messages);

    public static WebLogicConfigHolder Config(Action<WebLogicConfig>? configure = null)
    {
        var config = new WebLogicConfig();
        configure?.Invoke(config);
        return new WebLogicConfigHolder(config);
    }

    public static Dictionary<string, StringValues> Values(params (string Key, string Value)[] pairs)
    {
        var values = new Dictionary<string, StringValues>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
            values[key] = values.TryGetValue(key, out var existing) ? StringValues.Concat(existing, value) : new StringValues(value);
        return values;
    }

    /// <summary>Binds and validates like the form service does (without HTTP).</summary>
    public static (T Model, WebFormErrors Errors) BindAndValidate<T>(params (string Key, string Value)[] pairs)
    {
        var definition = WebFormDefinitionBuilder.Build(typeof(T));
        var values = Values(pairs);
        var visible = WebFormVisibility.For(definition, values);
        var errors = new WebFormErrors();
        var model = (T)Binder.Bind(definition, definition.Fields, values, visible, errors);
        Validator.Validate(definition, definition.Fields, model!, visible, errors);
        return (model, errors);
    }

    public static WebFormRendererServices RendererServices(IDataProtectionProvider? protection = null) =>
        new(new FixedCsrf(), new WebFormAntiBot(protection ?? new EphemeralDataProtectionProvider(), Config()), Messages, Config(), new ServiceCollection().BuildServiceProvider());

    private sealed class FixedCsrf : IWebCsrfTokens
    {
        public string GetToken(HttpContext httpContext) => "csrf-token";

        public bool Validate(HttpContext httpContext, string? token) => token == "csrf-token";
    }
}
