namespace CL.WebLogic.Security.Sessions;

/// <summary>
/// The request's session. Nothing is stored (and no cookie is sent) until something is written,
/// so anonymous visitors cost nothing. Values are per visitor — never shared between requests of
/// different visitors.
/// </summary>
public interface IWebSession
{
    /// <summary>True when the session exists in the store or will be saved with this response.</summary>
    bool IsPersisted { get; }

    /// <summary>Authentication progress.</summary>
    WebSessionAuthStage AuthStage { get; }

    /// <summary>Signed-in (or pending second factor) user id.</summary>
    string? UserId { get; }

    /// <summary>Creation time, when persisted.</summary>
    DateTimeOffset? CreatedAt { get; }

    /// <summary>Stored keys.</summary>
    IReadOnlyCollection<string> Keys { get; }

    /// <summary>Reads a string value.</summary>
    string? GetString(string key);

    /// <summary>Writes a string value.</summary>
    void SetString(string key, string value);

    /// <summary>Reads a JSON value.</summary>
    T? GetJson<T>(string key);

    /// <summary>Writes a JSON value.</summary>
    void SetJson<T>(string key, T value);

    /// <summary>Removes a value.</summary>
    void Remove(string key);

    /// <summary>Removes all app values (sign-in state is kept).</summary>
    void Clear();
}
