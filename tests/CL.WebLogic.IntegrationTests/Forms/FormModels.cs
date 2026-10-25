using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Uploads;
using CL.WebLogic.Forms.Validation;

namespace CL.WebLogic.IntegrationTests.Forms;

public sealed class ContactForm
{
    [Required, StringLength(40, MinimumLength = 2)]
    [WebField(Remote = true)]
    public string? Name { get; set; }

    [Required, EmailAddress]
    public string? Email { get; set; }

    [WebOptions(Provider = "countries")]
    public string? Country { get; set; }

    [WebIgnore]
    public bool IsAdmin { get; set; }
}

public sealed class TakenNameValidator : IWebFormValidator<ContactForm>
{
    public ValueTask ValidateAsync(ContactForm model, WebFormValidationContext context, CancellationToken cancellationToken)
    {
        if (string.Equals(model.Name, "taken", StringComparison.OrdinalIgnoreCase))
            context.AddError(nameof(ContactForm.Name), "That name is taken.");
        if (string.Equals(model.Email, "blocked@example.com", StringComparison.OrdinalIgnoreCase))
            context.AddError(nameof(ContactForm.Email), "That address is blocked.");
        return ValueTask.CompletedTask;
    }
}

public sealed class JoinForm
{
    [WebStep("account", Order = 1), Required, EmailAddress]
    public string? Email { get; set; }

    [WebStep("profile", Order = 2), Required]
    public string? DisplayName { get; set; }
}

public sealed class AvatarForm
{
    [Required, WebFile(Extensions = ".png", MaxBytes = 2048, ImagesOnly = true, MaxWidth = 64, MaxHeight = 64)]
    public WebUploadedFile? Avatar { get; set; }

    public string? Caption { get; set; }
}
