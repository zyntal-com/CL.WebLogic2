using System.Globalization;
using CL.WebLogic.Localization.Services;

namespace CL.WebLogic.Forms.Validation;

/// <summary>
/// Validation messages. Keys are looked up as <c>forms.{key}</c> in the text localizer; the
/// built-in English text is used when the localizer has none. <c>{0}</c> is the label.
/// </summary>
public sealed class WebFormMessages
{
    /// <summary>Built-in English messages.</summary>
    public static IReadOnlyDictionary<string, string> Defaults { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["required"] = "{0} is required.",
        ["minLength"] = "{0} must be at least {1} characters.",
        ["maxLength"] = "{0} must be at most {1} characters.",
        ["min"] = "{0} must be at least {1}.",
        ["max"] = "{0} must be at most {1}.",
        ["number"] = "{0} must be a number.",
        ["pattern"] = "{0} is not in the expected format.",
        ["email"] = "{0} must be a valid e-mail address.",
        ["url"] = "{0} must be a valid URL.",
        ["tel"] = "{0} must be a valid phone number.",
        ["equalTo"] = "{0} must match {1}.",
        ["invalid"] = "{0} is not valid.",
        ["choice"] = "{0} contains a choice that is not available.",
        ["repeaterMin"] = "{0}: add at least {1}.",
        ["repeaterMax"] = "{0}: add at most {1}.",
        ["fileCount"] = "{0}: upload at most {1} files.",
        ["fileTooLarge"] = "{0}: {1} is larger than {2}.",
        ["fileType"] = "{0}: {1} is not an allowed file type.",
        ["fileContent"] = "{0}: the content of {1} does not match its type.",
        ["imageOnly"] = "{0}: {1} is not an image.",
        ["imageSize"] = "{0}: {1} is larger than {2}×{3} pixels.",
        ["timeout"] = "{0} could not be checked. Try a shorter value.",
        ["rejected"] = "The form could not be submitted. Reload the page and try again.",
        ["summary"] = "Please correct the highlighted fields."
    };

    private readonly IWebTextLocalizer _localizer;

    /// <summary>Creates the messages.</summary>
    public WebFormMessages(IWebTextLocalizer localizer) => _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

    /// <summary>The template for <paramref name="key"/>.</summary>
    public string Template(string key)
    {
        var localized = _localizer.GetText("forms." + key);
        return localized != "forms." + key && !string.IsNullOrEmpty(localized) ? localized : Defaults.GetValueOrDefault(key, "{0} is not valid.");
    }

    /// <summary>The formatted message.</summary>
    public string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Template(key), args);
}
