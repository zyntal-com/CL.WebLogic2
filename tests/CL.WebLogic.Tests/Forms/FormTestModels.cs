using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Uploads;

namespace CL.WebLogic.Tests.Forms;

public enum Topic
{
    General,
    [Display(Name = "Billing question")]
    Billing,
    Support
}

[WebForm(Title = "Contact us", SubmitLabel = "Send")]
public sealed class ContactForm
{
    [Required, StringLength(50, MinimumLength = 2)]
    public string? Name { get; set; }

    [Required, EmailAddress]
    [WebField(Autocomplete = "email")]
    public string? Email { get; set; }

    public Topic Topic { get; set; }

    [WebShowWhen(nameof(Topic), "Billing")]
    [Required]
    public string? InvoiceNumber { get; set; }

    [Range(1, 10)]
    public int? Priority { get; set; }

    [DataType(DataType.MultilineText), StringLength(500)]
    public string? Message { get; set; }

    public bool Subscribe { get; set; }

    [WebOptions("red|Red", "green|Green", "blue|Blue")]
    public List<string> Colors { get; set; } = [];

    [WebIgnore]
    public bool IsAdmin { get; set; }

    [RegularExpression("[A-Z]{3}-[0-9]{3}")]
    public string? Code { get; set; }
}

public sealed class SignupForm
{
    [Required, DataType(DataType.Password), MinLength(8)]
    public string? Password { get; set; }

    [Compare(nameof(Password)), DataType(DataType.Password)]
    [WebField(Label = "Repeat password")]
    public string? Confirm { get; set; }
}

public sealed class OrderLine
{
    [Required]
    public string? Product { get; set; }

    [Range(1, 99)]
    public int Quantity { get; set; } = 1;
}

public sealed class OrderForm
{
    [Required]
    public string? Customer { get; set; }

    [WebRepeater(Min = 1, Max = 3)]
    public List<OrderLine> Lines { get; set; } = [];
}

public sealed class WizardForm
{
    [WebStep("account", Title = "Account", Order = 1), Required]
    public string? Email { get; set; }

    [WebStep("profile", Title = "Profile", Order = 2), Required]
    public string? DisplayName { get; set; }

    [WebStep("profile")]
    public string? Bio { get; set; }
}

public sealed class UploadForm
{
    [WebFile(Extensions = ".png,.jpg", MaxBytes = 1024, ImagesOnly = true, MaxWidth = 100, MaxHeight = 100)]
    public WebUploadedFile? Avatar { get; set; }

    [WebFile(Extensions = ".txt,.pdf", MaxFiles = 2)]
    public List<WebUploadedFile> Attachments { get; set; } = [];
}

public sealed class CrossFieldForm : IValidatableObject
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From > To)
            yield return new ValidationResult("To must be after From.", [nameof(To)]);
    }
}

public sealed class XssForm
{
    [WebField(Label = "<script>alert(1)</script>", Help = "\"><img src=x onerror=alert(1)>")]
    public string? Value { get; set; }

    [WebField(WebFieldType.RichText)]
    public string? Body { get; set; }
}
