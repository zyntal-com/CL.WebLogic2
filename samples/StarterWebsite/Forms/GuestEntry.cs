using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>One guest in <see cref="EventSignupForm.Guests"/>.</summary>
public sealed class GuestEntry
{
    /// <summary>Guest name.</summary>
    [Required, StringLength(80)]
    [WebField(Label = "Guest name")]
    public string? Name { get; set; }

    /// <summary>Dietary needs.</summary>
    [StringLength(80)]
    [WebField(Label = "Dietary needs")]
    public string? Diet { get; set; }
}
