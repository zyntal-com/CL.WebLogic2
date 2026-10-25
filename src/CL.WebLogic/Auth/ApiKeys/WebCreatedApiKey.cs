namespace CL.WebLogic.Auth.ApiKeys;

/// <summary>A newly created key. <see cref="PlainTextKey"/> is shown once and never stored.</summary>
public sealed class WebCreatedApiKey
{
    /// <summary>The stored record.</summary>
    public required WebApiKey Key { get; init; }

    /// <summary>The full key to hand to the client.</summary>
    public required string PlainTextKey { get; init; }
}
