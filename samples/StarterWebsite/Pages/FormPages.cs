using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Runtime.Models;
using StarterWebsite.Forms;

namespace StarterWebsite.Pages;

/// <summary>The forms gallery and the wizard (both work with and without JavaScript).</summary>
public sealed class FormPages : IWebContributor
{
    private const string GalleryTemplate = "templates/forms/index";
    private const string WizardTemplate = "templates/forms/wizard";

    /// <inheritdoc/>
    public Task ContributeAsync(WebContributionBuilder builder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.MapPage("/forms", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Page(GalleryTemplate, new { Done = r.GetQuery("done") }, PageModels.Meta("Forms gallery"))))
            .AllowAnonymous();
        builder.MapPage("/forms", SubmitGalleryAsync, "POST").AllowAnonymous().WithRateLimit("forms").WithBodyLimit(3 * 1024 * 1024);
        builder.MapPage("/forms/wizard", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Page(WizardTemplate, new { Done = r.GetQuery("done") }, PageModels.Meta("Wizard"))))
            .AllowAnonymous();
        builder.MapPage("/forms/wizard", SubmitWizardAsync, "POST").AllowAnonymous().WithRateLimit("forms");
        return Task.CompletedTask;
    }

    private static async ValueTask<WebResult> SubmitGalleryAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<EventSignupForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsValid)
            return submission.Invalid(fallbackTemplate: GalleryTemplate, fallbackModel: new { Done = (string?)null });

        var model = submission.Model;
        var photo = model.Photo is { } file ? $" Photo: {file.DetectedType} {file.Width}×{file.Height}." : string.Empty;
        var message = $"Thanks {model.Name}! {model.Attendance}, {model.Guests.Count} guest(s), {model.Tracks.Count} track(s).{photo}";
        return FormResult.Success(message, "/forms?done=" + Uri.EscapeDataString(model.Name!), new
        {
            model.Name, model.Country, model.Attendance, model.Tracks, model.Arrival, model.Experience, model.BadgeColor,
            Guests = model.Guests.Select(g => g.Name).ToList(),
            Photo = model.Photo is null ? null : new { model.Photo.FileName, model.Photo.DetectedType, model.Photo.Width, model.Photo.Height, model.Photo.Length }
        });
    }

    private static async ValueTask<WebResult> SubmitWizardAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var submission = await request.Forms.BindAsync<ProjectWizardForm>(cancellationToken).ConfigureAwait(false);
        if (!submission.IsComplete || !submission.IsValid)
            return submission.Continue(WizardTemplate, new { Done = (string?)null });
        return FormResult.Success($"Project '{submission.Model.ProjectName}' launches {submission.Model.LaunchDate:yyyy-MM-dd}.",
            "/forms/wizard?done=" + Uri.EscapeDataString(submission.Model.ProjectName!));
    }
}
