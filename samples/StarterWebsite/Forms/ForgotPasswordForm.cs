using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>Request a reset link (the answer never reveals whether the address exists).</summary>
[WebForm(Id = "forgot-password", SubmitLabel = "Send reset link")]
public sealed class ForgotPasswordForm
{
    /// <summary>The account's e-mail.</summary>
    [Required, EmailAddress, StringLength(254)]
    [WebField(WebFieldType.Email, Label = "E-mail", Autocomplete = "email")]
    public string? Email { get; set; }
}
