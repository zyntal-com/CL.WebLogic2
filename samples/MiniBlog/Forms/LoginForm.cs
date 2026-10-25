using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace MiniBlog.Forms;

/// <summary>Editor sign-in.</summary>
[WebForm(Id = "login", SubmitLabel = "Sign in", AntiBot = false)]
public sealed class LoginForm
{
    /// <summary>User name or e-mail.</summary>
    [Required, StringLength(254)]
    [WebField(Label = "User name or e-mail", Autocomplete = "username")]
    public string? Login { get; set; }

    /// <summary>Password.</summary>
    [Required, StringLength(512)]
    [WebField(WebFieldType.Password, Label = "Password", Autocomplete = "current-password")]
    public string? Password { get; set; }

    /// <summary>Where to go afterwards.</summary>
    [WebField(WebFieldType.Hidden)]
    public string? ReturnUrl { get; set; }
}
