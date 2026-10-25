using CL.WebLogic.Forms.Validation;

namespace StarterWebsite.Forms;

/// <summary>A cross-field rule: online attendees cannot bring guests.</summary>
public sealed class EventSignupValidator : IWebFormValidator<EventSignupForm>
{
    /// <inheritdoc/>
    public ValueTask ValidateAsync(EventSignupForm model, WebFormValidationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);
        if (model.Attendance == "online" && model.Guests.Count > 0)
            context.AddError(nameof(EventSignupForm.Guests), "Guests are for in-person attendance only.");
        if (model.Country == "other" && string.IsNullOrWhiteSpace(model.OtherCountry))
            context.AddError(nameof(EventSignupForm.OtherCountry), "Tell us which country.");
        return ValueTask.CompletedTask;
    }
}
