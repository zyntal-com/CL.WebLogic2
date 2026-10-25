using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

namespace StarterWebsite.Forms;

/// <summary>A three-step wizard; progress travels in a signed token, so no server state is needed.</summary>
[WebForm(Id = "project-wizard", Title = "Start a project", SubmitLabel = "Next")]
public sealed class ProjectWizardForm
{
    /// <summary>Step 1.</summary>
    [WebStep("basics", Title = "Basics", Order = 1), Required, StringLength(60, MinimumLength = 3)]
    [WebField(Label = "Project name")]
    public string? ProjectName { get; set; }

    /// <summary>Step 1.</summary>
    [WebStep("basics", Order = 1), Required, WebOptions("website", "api", "intranet")]
    [WebField(WebFieldType.Radio, Label = "Kind")]
    public string? Kind { get; set; }

    /// <summary>Step 2.</summary>
    [WebStep("team", Title = "Team", Order = 2), Range(1, 50)]
    [WebField(WebFieldType.Number, Label = "Team size")]
    public int TeamSize { get; set; } = 3;

    /// <summary>Step 2.</summary>
    [WebStep("team", Order = 2), EmailAddress]
    [WebField(WebFieldType.Email, Label = "Lead e-mail")]
    public string? LeadEmail { get; set; }

    /// <summary>Step 3.</summary>
    [WebStep("launch", Title = "Launch", Order = 3), Required]
    [WebField(WebFieldType.Date, Label = "Launch date")]
    public DateOnly? LaunchDate { get; set; }
}
