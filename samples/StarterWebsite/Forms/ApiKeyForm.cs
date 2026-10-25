using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>Create a personal API key.</summary>
[WebForm(Id = "api-key", SubmitLabel = "Create key", AntiBot = false)]
public sealed class ApiKeyForm
{
    /// <summary>A label to recognise the key by.</summary>
    [Required, StringLength(60, MinimumLength = 2)]
    [WebField(Label = "Name", Placeholder = "My script")]
    public string? Name { get; set; }

    /// <summary>What the key may do.</summary>
    [Required, WebOptions(Provider = "api-scopes")]
    [WebField(WebFieldType.CheckboxList, Label = "Scopes")]
    public List<string> Scopes { get; set; } = [];

    /// <summary>Lifetime in days (0 = no expiry).</summary>
    [Range(0, 365)]
    [WebField(WebFieldType.Number, Label = "Expires after (days)", Help = "0 means never.")]
    public int ExpiresInDays { get; set; } = 30;
}
