using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Uploads;

namespace StarterWebsite.Forms;

/// <summary>The forms gallery: most field types, conditional fields, a repeater and an upload.</summary>
[WebForm(Id = "event-signup", Title = "Conference sign-up", SubmitLabel = "Sign me up")]
public sealed class EventSignupForm
{
    /// <summary>Text with remote validation.</summary>
    [Required, StringLength(80, MinimumLength = 2)]
    [WebField(Label = "Full name", Autocomplete = "name")]
    public string? Name { get; set; }

    /// <summary>E-mail.</summary>
    [Required, EmailAddress]
    [WebField(WebFieldType.Email, Label = "E-mail", Autocomplete = "email")]
    public string? Email { get; set; }

    /// <summary>Select from a provider.</summary>
    [Required, WebOptions(Provider = "countries")]
    [WebField(Label = "Country")]
    public string? Country { get; set; }

    /// <summary>Shown only when the country is "other".</summary>
    [WebShowWhen(nameof(Country), "other"), StringLength(60)]
    [WebField(Label = "Which country?")]
    public string? OtherCountry { get; set; }

    /// <summary>Radio buttons from inline options.</summary>
    [Required, WebOptions("in-person", "online")]
    [WebField(WebFieldType.Radio, Label = "Attending")]
    public string? Attendance { get; set; }

    /// <summary>Several choices.</summary>
    [WebOptions(Provider = "tracks")]
    [WebField(WebFieldType.CheckboxList, Label = "Interesting tracks")]
    public List<string> Tracks { get; set; } = [];

    /// <summary>Date.</summary>
    [Required]
    [WebField(WebFieldType.Date, Label = "Arrival")]
    public DateOnly? Arrival { get; set; }

    /// <summary>Range slider.</summary>
    [Range(1, 5)]
    [WebField(WebFieldType.Range, Label = "Experience (1–5)")]
    public int Experience { get; set; } = 3;

    /// <summary>Colour picker.</summary>
    [WebField(WebFieldType.Color, Label = "Badge colour")]
    public string? BadgeColor { get; set; } = "#3b82f6";

    /// <summary>A repeater of nested items.</summary>
    [WebRepeater(Min = 0, Max = 3)]
    [WebField(Label = "Guests")]
    public List<GuestEntry> Guests { get; set; } = [];

    /// <summary>An image upload, sniffed and measured on the server.</summary>
    [WebFile(Extensions = ".png,.jpg,.jpeg,.gif,.webp", MaxBytes = 2 * 1024 * 1024, ImagesOnly = true, MaxWidth = 4096, MaxHeight = 4096)]
    [WebField(Label = "Badge photo (optional)")]
    public WebUploadedFile? Photo { get; set; }

    /// <summary>Free text.</summary>
    [StringLength(1000)]
    [WebField(WebFieldType.TextArea, Label = "Anything else?")]
    public string? Notes { get; set; }

    /// <summary>A real checkbox.</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the code of conduct.")]
    [WebField(WebFieldType.Checkbox, Label = "I accept the code of conduct")]
    public bool AcceptConduct { get; set; }
}
