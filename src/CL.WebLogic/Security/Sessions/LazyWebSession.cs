using System.Text.Json;
using CL.WebLogic.Runtime.Services;

namespace CL.WebLogic.Security.Sessions;

/// <summary>
/// One session object per request. Loads from the store only when a cookie was sent and writes
/// back only when something changed (or the touch interval passed). Created and persisted by
/// <see cref="WebSessionManager"/>.
/// </summary>
public sealed class LazyWebSession : IWebSession
{
    private const int MaxKeyLength = 128;
    private const int MaxTotalChars = 64 * 1024;

    private Dictionary<string, string> _values = new(StringComparer.Ordinal);

    internal string? Token { get; set; }
    internal WebSessionRecord? Loaded { get; set; }
    internal bool Dirty { get; set; }
    internal bool Abandoned { get; set; }
    internal bool IssueCookie { get; set; }
    internal bool ExpireCookie { get; set; }
    internal string? PreviousIdHash { get; set; }
    internal string? CsrfSecret { get; set; }
    internal DateTimeOffset? Created { get; set; }
    internal string? UserName { get; set; }
    internal IReadOnlyList<string> Groups { get; set; } = [];
    internal IReadOnlyList<string> Permissions { get; set; } = [];
    internal IReadOnlyList<string> AuthenticationMethods { get; set; } = [];
    internal string? TenantId { get; set; }
    internal string? SecurityStamp { get; set; }
    internal IReadOnlyDictionary<string, string> Values => _values;

    /// <inheritdoc/>
    public bool IsPersisted => !Abandoned && (Loaded is not null || Dirty);

    /// <inheritdoc/>
    public WebSessionAuthStage AuthStage { get; internal set; }

    /// <inheritdoc/>
    public string? UserId { get; internal set; }

    /// <inheritdoc/>
    public DateTimeOffset? CreatedAt => IsPersisted ? Created : null;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Keys => _values.Keys;

    /// <inheritdoc/>
    public string? GetString(string key) => _values.TryGetValue(key, out var value) ? value : null;

    /// <inheritdoc/>
    public void SetString(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);
        if (key.Length > MaxKeyLength)
            throw new ArgumentException($"Session keys are limited to {MaxKeyLength} characters.", nameof(key));
        var total = _values.Where(kv => kv.Key != key).Sum(kv => kv.Key.Length + kv.Value.Length) + key.Length + value.Length;
        if (total > MaxTotalChars)
            throw new InvalidOperationException($"Session data is limited to {MaxTotalChars} characters; store large data elsewhere.");
        _values[key] = value;
        Touch();
    }

    /// <inheritdoc/>
    public T? GetJson<T>(string key) =>
        GetString(key) is { } json ? JsonSerializer.Deserialize<T>(json, WebJson.Default) : default;

    /// <inheritdoc/>
    public void SetJson<T>(string key, T value) => SetString(key, JsonSerializer.Serialize(value, WebJson.Default));

    /// <inheritdoc/>
    public void Remove(string key)
    {
        if (_values.Remove(key))
            Touch();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (_values.Count == 0)
            return;
        _values = new Dictionary<string, string>(StringComparer.Ordinal);
        Touch();
    }

    internal void ClearValues() => _values = new Dictionary<string, string>(StringComparer.Ordinal);

    internal void LoadFrom(WebSessionRecord record)
    {
        Loaded = record;
        Created = record.CreatedAt;
        AuthStage = record.AuthStage;
        UserId = record.UserId;
        UserName = record.UserName;
        Groups = record.Groups;
        Permissions = record.Permissions;
        AuthenticationMethods = record.AuthenticationMethods;
        TenantId = record.TenantId;
        SecurityStamp = record.SecurityStamp;
        CsrfSecret = record.CsrfSecret;
        _values = new Dictionary<string, string>(record.Values, StringComparer.Ordinal);
    }

    /// <summary>Marks the session changed, creating it (token, secret, cookie) on first write.</summary>
    internal void Touch()
    {
        Dirty = true;
        Abandoned = false;
        if (Token is null || (Loaded is null && !IssueCookie))
        {
            Token = WebSessionTokens.NewToken();
            IssueCookie = true;
        }
        CsrfSecret ??= WebSessionTokens.NewSecret();
    }
}
