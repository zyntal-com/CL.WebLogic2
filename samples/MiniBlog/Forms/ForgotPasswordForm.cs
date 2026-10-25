using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace MiniBlog.Forms;

/// <summary>Request a reset link.</summary>
[WebForm(Id = "forgot-password", SubmitLabel = "Send reset link")]
public sealed class ForgotPasswordForm
{
    /// <summary>The account's e-mail.</summary>
    [Required, EmailAddress, StringLength(254)]
    [WebField(WebFieldType.Email, Label = "E-mail", Autocomplete = "email")]
    public string? Email { get; set; }
}
