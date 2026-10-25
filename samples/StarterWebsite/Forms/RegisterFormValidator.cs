using CL.WebLogic.Auth.Users;
using CL.WebLogic.Forms.Validation;

namespace StarterWebsite.Forms;

/// <summary>Async rule: the user name must be free (also used by remote validation while typing).</summary>
public sealed class RegisterFormValidator : IWebFormValidator<RegisterForm>
{
    private readonly IWebUserStore _users;

    /// <summary>Creates the validator.</summary>
    public RegisterFormValidator(IWebUserStore users) => _users = users;

    /// <inheritdoc/>
    public async ValueTask ValidateAsync(RegisterForm model, WebFormValidationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);
        if (!string.IsNullOrWhiteSpace(model.UserName) && await _users.FindByLoginAsync(model.UserName, cancellationToken).ConfigureAwait(false) is not null)
            context.AddError(nameof(RegisterForm.UserName), "That user name is taken.");
    }
}
