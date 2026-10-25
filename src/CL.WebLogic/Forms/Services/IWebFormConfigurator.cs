namespace CL.WebLogic.Forms.Services;

/// <summary>Configures the form of <typeparamref name="TModel"/> in code (applied after the attributes). Register with <c>web.ConfigureForm&lt;TModel, TConfigurator&gt;()</c>.</summary>
public interface IWebFormConfigurator<TModel>
{
    /// <summary>Configures the form.</summary>
    void Configure(WebFormBuilder<TModel> form);
}
