namespace Samples.Hosting;

/// <summary>
/// The environment variables every sample understands, written into the CodeLogic config files
/// before startup. Unset variables leave the files alone, so the files stay the source of truth.
/// </summary>
public static class SampleConfig
{
    /// <summary>
    /// WebLogic: <c>DEMO_SITE_NAME</c>, <c>DEMO_PUBLIC_URL</c>, <c>DEMO_ALLOWED_HOSTS</c>,
    /// <c>DEMO_HTTPS</c> (redirect + HSTS), <c>DEMO_TRUSTED_PROXIES</c> (turns forwarded headers on),
    /// <c>DEMO_EXPLORER</c> (explorer in production, permission still required),
    /// <c>DEMO_MAIL_FROM</c> and <c>DEMO_PASSWORD_PEPPER</c>. Without a pepper one is generated into the
    /// config file on the data volume — keep that volume with the user database, or hashes stop verifying.
    /// </summary>
    public static ConfigFilePatch WebLogic(SamplePaths paths)
    {
        var proxies = DemoEnvironment.List("DEMO_TRUSTED_PROXIES");
        return ConfigFilePatch.Open(paths.LibraryConfig("CL.WebLogic", "weblogic"))
            .Set("siteName", DemoEnvironment.Text("DEMO_SITE_NAME"))
            .Set("publicBaseUrl", DemoEnvironment.Text("DEMO_PUBLIC_URL"))
            .Set("security.hosts.allowedHosts", DemoEnvironment.List("DEMO_ALLOWED_HOSTS"))
            .Set("security.https.enforce", DemoEnvironment.Flag("DEMO_HTTPS"))
            .Set("security.forwardedHeaders.enabled", proxies is null ? null : proxies.Count > 0)
            .Set("security.forwardedHeaders.trustedProxies", proxies)
            .Set("explorer.inProduction", DemoEnvironment.Flag("DEMO_EXPLORER"))
            .Set("auth.notifications.fromAddress", DemoEnvironment.Text("DEMO_MAIL_FROM"))
            .Set("auth.passwords.pepper", DemoEnvironment.Text("DEMO_PASSWORD_PEPPER"))
            // Secrets generated once and kept in the config file on the data volume.
            .Default("security.audit.ipHashKey", NewSecret())
            .Default("auth.passwords.pepper", NewSecret());
    }

    private static string NewSecret() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// CL.Mail SMTP: <c>SMTP_HOST</c>, <c>SMTP_PORT</c>, <c>SMTP_USER</c>, <c>SMTP_PASSWORD</c>,
    /// <c>SMTP_SECURITY</c> (none, startTls, sslTls).
    /// </summary>
    public static ConfigFilePatch Mail(SamplePaths paths) =>
        ConfigFilePatch.Open(paths.LibraryConfig("CL.Mail", "mail"))
            .Set("enabled", DemoEnvironment.Text("SMTP_HOST") is null ? null : true)
            .Set("smtp.host", DemoEnvironment.Text("SMTP_HOST"))
            .Set("smtp.port", DemoEnvironment.Number("SMTP_PORT"))
            .Set("smtp.username", DemoEnvironment.Text("SMTP_USER"))
            .Set("smtp.password", DemoEnvironment.Text("SMTP_PASSWORD"))
            .Set("smtp.securityMode", DemoEnvironment.Text("SMTP_SECURITY"))
            .Set("defaultFromEmail", DemoEnvironment.Text("DEMO_MAIL_FROM"));

    /// <summary>True when an SMTP server is configured (otherwise mails go to the dev outbox).</summary>
    public static bool HasSmtp => DemoEnvironment.Text("SMTP_HOST") is not null;

    /// <summary>Turns off a toolkit library the sample does not use, unless its file says otherwise.</summary>
    public static void DisableUnlessConfigured(SamplePaths paths, string libraryId, string section) =>
        ConfigFilePatch.Open(paths.LibraryConfig(libraryId, section)).Default("enabled", false).Save();
}
