namespace CL.WebLogic.Security.Webhooks;

/// <summary>
/// How <see cref="WebWebhookVerifier"/> checks one sender's HMAC-SHA256 signature.
/// <para>
/// Two header shapes are supported. By default the signature header holds just the MAC (with an
/// optional <see cref="SignaturePrefix"/> such as <c>sha256=</c>) and an optional
/// <see cref="TimestampHeader"/> carries the send time. When <see cref="TimestampKey"/> is set the
/// signature header is a list of <c>key=value</c> pairs instead (<c>t=1700000000,v1=…,v1=…</c>).
/// </para>
/// <para>
/// When a timestamp is used it is signed too: the MAC covers
/// <see cref="SignedTimestampFormat"/> (with <c>{timestamp}</c> replaced) followed by the raw body,
/// and requests older or newer than <see cref="Tolerance"/> are refused.
/// </para>
/// This is a class, not a record, so the secrets never appear in a generated <c>ToString()</c>.
/// Never log it.
/// </summary>
public sealed class WebWebhookOptions
{
    /// <summary>
    /// Active shared secrets (used as UTF-8 key bytes). A signature made with any of them is
    /// accepted, so a new secret can be added before the sender switches and the old one removed after.
    /// </summary>
    public IReadOnlyList<string> Secrets { get; init; } = [];

    /// <summary>Request header that carries the signature.</summary>
    public string SignatureHeader { get; init; } = "X-Signature";

    /// <summary>Prefix stripped from the signature when present (for example <c>sha256=</c>), or null.</summary>
    public string? SignaturePrefix { get; init; }

    /// <summary>How the MAC is written.</summary>
    public WebWebhookSignatureEncoding Encoding { get; init; } = WebWebhookSignatureEncoding.Hex;

    /// <summary>Separate header with the send time in Unix seconds, or null (no timestamp check unless <see cref="TimestampKey"/> is set).</summary>
    public string? TimestampHeader { get; init; }

    /// <summary>
    /// Key of the timestamp inside a <c>key=value,…</c> signature header (<c>t</c> for Stripe), or null when the
    /// signature header holds only the MAC.
    /// </summary>
    public string? TimestampKey { get; init; }

    /// <summary>Key of the signatures inside a <c>key=value,…</c> signature header (may repeat).</summary>
    public string SignatureKey { get; init; } = "v1";

    /// <summary>What is signed in front of the body when a timestamp is used; <c>{timestamp}</c> is replaced with the header value.</summary>
    public string SignedTimestampFormat { get; init; } = "{timestamp}.";

    /// <summary>How far the timestamp may be from the server clock, either way (replay window).</summary>
    public TimeSpan Tolerance { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Largest body read and verified, in bytes. The route's own limit applies too, whichever is smaller.</summary>
    public long MaxBodyBytes { get; init; } = 1024 * 1024;

    /// <summary>True when the signature covers a timestamp.</summary>
    public bool UsesTimestamp => TimestampKey is not null || TimestampHeader is not null;

    /// <summary>GitHub: <c>X-Hub-Signature-256: sha256=&lt;hex&gt;</c>, no timestamp.</summary>
    public static WebWebhookOptions GitHub(params string[] secrets) => new()
    {
        Secrets = secrets,
        SignatureHeader = "X-Hub-Signature-256",
        SignaturePrefix = "sha256="
    };

    /// <summary>Stripe: <c>Stripe-Signature: t=&lt;unix&gt;,v1=&lt;hex&gt;</c>, signed payload <c>{t}.{body}</c>.</summary>
    public static WebWebhookOptions Stripe(params string[] secrets) => new()
    {
        Secrets = secrets,
        SignatureHeader = "Stripe-Signature",
        TimestampKey = "t",
        SignatureKey = "v1"
    };

    /// <summary>Throws when the options cannot verify anything (programmer error).</summary>
    internal void EnsureValid()
    {
        if (Secrets.Count == 0 || Secrets.Any(string.IsNullOrEmpty))
            throw new InvalidOperationException("WebWebhookOptions.Secrets needs at least one non-empty secret.");
        if (string.IsNullOrWhiteSpace(SignatureHeader))
            throw new InvalidOperationException("WebWebhookOptions.SignatureHeader is required.");
        if (TimestampKey is not null && string.IsNullOrWhiteSpace(SignatureKey))
            throw new InvalidOperationException("WebWebhookOptions.SignatureKey is required with TimestampKey.");
        if (Tolerance <= TimeSpan.Zero)
            throw new InvalidOperationException("WebWebhookOptions.Tolerance must be positive.");
        if (MaxBodyBytes <= 0)
            throw new InvalidOperationException("WebWebhookOptions.MaxBodyBytes must be positive.");
    }
}
