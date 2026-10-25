namespace CL.WebLogic.Forms.Validation;

/// <summary>
/// Cross-field and asynchronous validation of <typeparamref name="TModel"/> (e.g. "user name is
/// taken"). Runs after the field rules passed. Register with <c>web.AddFormValidator&lt;TModel, T&gt;()</c>.
/// </summary>
public interface IWebFormValidator<TModel>
{
    /// <summary>Validates; report problems with <see cref="WebFormValidationContext.AddError"/>.</summary>
    ValueTask ValidateAsync(TModel model, WebFormValidationContext context, CancellationToken cancellationToken);
}
