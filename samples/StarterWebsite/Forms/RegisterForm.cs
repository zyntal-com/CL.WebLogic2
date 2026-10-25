using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>Account registration (honeypot + timing protected).</summary>
[WebForm(Id = "register", SubmitLabel = "Create account")]
public sealed class RegisterForm
{
    /// <summary>Public user name.</summary>
    [Required, StringLength(32, MinimumLength = 3), RegularExpression("^[A-Za-z0-9_.-]+$", ErrorMessage = "Use letters, digits, '.', '_' or '-'.")]
    [WebField(Label = "User name", Autocomplete = "username", Remote = true, Help = "3–32 characters; letters, digits, . _ -")]
    public string? UserName { get; set; }

    /// <summary>E-mail for verification and password resets.</summary>
    [Required, EmailAddress, StringLength(254)]
    [WebField(WebFieldType.Email, Label = "E-mail", Autocomplete = "email")]
    public string? Email { get; set; }

    /// <summary>Password (policy enforced by the toolkit).</summary>
    [Required, StringLength(512, MinimumLength = 10)]
    [WebField(WebFieldType.Password, Label = "Password", Autocomplete = "new-password", Help = "At least 10 characters.")]
    public string? Password { get; set; }

    /// <summary>Repeat.</summary>
    [Required, Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [WebField(WebFieldType.Password, Label = "Repeat password", Autocomplete = "new-password")]
    public string? ConfirmPassword { get; set; }
}
