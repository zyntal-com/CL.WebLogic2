using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Security e-mails (reset links, 2FA changes, …).</summary>
public sealed class NotificationsConfig
{
    /// <summary>Sender address for security e-mails.</summary>
    [ConfigField(Label = "From", Group = "Notifications", Order = 70)]
    public string FromAddress { get; set; } = "no-reply@localhost";

    /// <summary>Sender display name (defaults to the site name).</summary>
    [ConfigField(Label = "From Name", Group = "Notifications", Order = 71)]
    public string FromName { get; set; } = "";

    /// <summary>Folder (under the WebLogic data directory) the development outbox writes .eml files to.</summary>
    [ConfigField(Label = "Outbox Folder", Group = "Notifications", Order = 72, Collapsed = true)]
    public string OutboxFolder { get; set; } = "outbox";

    /// <summary>CL.Mail template id prefix (<c>{prefix}.{kind}</c>); built-in texts are used when a template is missing.</summary>
    [ConfigField(Label = "Mail Template Prefix", Group = "Notifications", Order = 73, Collapsed = true)]
    public string MailTemplatePrefix { get; set; } = "weblogic";

    /// <summary>E-mail the user when they sign in from a browser none of their other sessions use. Needs <c>IWebSessionListStore</c>.</summary>
    [ConfigField(Label = "Notify New Device", Description = "E-mail on sign-ins from a browser none of the user's other sessions use.", Group = "Notifications", Order = 74)]
    public bool NotifyNewDevice { get; set; }
}
