using CL.Mail;
using CL.Mail.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;

namespace CL.WebLogic.Auth.Notifications;

/// <summary>
/// Sends security e-mails through CL.Mail. Uses the mail template
/// <c>{MailTemplatePrefix}.{kind}</c> (e.g. <c>weblogic.password-reset</c>) when it exists, with
/// the variables <c>userName</c>, <c>link</c>, <c>siteName</c> and the notification values;
/// otherwise the built-in text.
/// </summary>
public sealed class MailSecurityNotifier : IWebSecurityNotifier
{
    private readonly IWebLibraryResolver _libraries;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the notifier.</summary>
    public MailSecurityNotifier(IWebLibraryResolver libraries, WebLogicConfigHolder config, IWebLogicContext context)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task SendAsync(WebSecurityNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var mail = _libraries.GetLibrary<MailLibrary>();
        if (mail is null)
        {
            _context.Logger.Error($"CL.Mail is not loaded; the {notification.Kind} e-mail for user {notification.UserId} was not sent.");
            return;
        }

        var config = _config.Current;
        var settings = config.Auth.Notifications;
        var fromName = settings.FromName.Length > 0 ? settings.FromName : config.SiteName;
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["userName"] = notification.UserName,
            ["link"] = notification.Link,
            ["siteName"] = config.SiteName
        };
        foreach (var (key, value) in notification.Values)
            variables[key] = value;

        var templateId = settings.MailTemplatePrefix + "." + KebabCase(notification.Kind.ToString());
        var result = await mail.SendTemplatedAsync(templateId, variables,
            b => b.From(settings.FromAddress, fromName).To(notification.ToAddress), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess && result.Error == MailError.TemplateNotFound)
        {
            var (subject, text, html) = SecurityNotificationText.Render(notification, config.SiteName);
            var message = mail.CreateMessage().From(settings.FromAddress, fromName).To(notification.ToAddress).Subject(subject).Body(text, html).Build();
            result = await mail.Smtp.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }

        if (!result.IsSuccess)
            _context.Logger.Error($"Sending the {notification.Kind} e-mail for user {notification.UserId} failed: {result.Error} {result.ErrorMessage}");
    }

    private static string KebabCase(string value) =>
        string.Concat(value.Select((c, i) => char.IsUpper(c) && i > 0 ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
