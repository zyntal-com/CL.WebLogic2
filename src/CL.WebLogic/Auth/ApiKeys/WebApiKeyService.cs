using CL.WebLogic.Auth.Events;
using CL.WebLogic.Hosting.Services;

namespace CL.WebLogic.Auth.ApiKeys;

/// <summary>Creates, lists and revokes API keys.</summary>
public sealed class WebApiKeyService
{
    private readonly IWebApiKeyStore _store;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebApiKeyService(IWebApiKeyStore store, IWebLogicContext context, TimeProvider? time = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Creates a key. Return <see cref="WebCreatedApiKey.PlainTextKey"/> to the client once.</summary>
    public async Task<WebCreatedApiKey> CreateAsync(WebApiKeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        foreach (var network in request.AllowedNetworks)
        {
            if (!Security.Services.IpNetworkParser.TryParse(network, out _))
                throw new ArgumentException($"'{network}' is not a valid address or CIDR network.", nameof(request));
        }

        var (plain, prefix) = WebApiKeyFormat.Generate();
        var now = _time.GetUtcNow();
        var key = new WebApiKey
        {
            Id = Guid.NewGuid().ToString("N"),
            Prefix = prefix,
            SecretHash = WebApiKeyFormat.Hash(plain),
            Name = request.Name,
            UserId = request.UserId,
            Scopes = request.Scopes,
            Groups = request.Groups,
            Permissions = request.Permissions,
            TenantId = request.TenantId,
            AllowedNetworks = request.AllowedNetworks,
            RateLimitPolicy = request.RateLimitPolicy,
            CreatedAt = now,
            ExpiresAt = request.Lifetime is { } lifetime ? now.Add(lifetime) : null
        };
        await _store.CreateAsync(key, cancellationToken).ConfigureAwait(false);
        _context.Events.Publish(new ApiKeyChangedEvent(key.Id, key.UserId, false));
        return new WebCreatedApiKey { Key = key, PlainTextKey = plain };
    }

    /// <summary>Revokes a key; false when it does not exist (or belongs to another user when <paramref name="userId"/> is given).</summary>
    public async Task<bool> RevokeAsync(string keyId, string? userId, CancellationToken cancellationToken)
    {
        var key = await _store.FindByIdAsync(keyId, cancellationToken).ConfigureAwait(false);
        if (key is null || (userId is not null && key.UserId != userId))
            return false;
        if (key.RevokedAt is null)
        {
            await _store.UpdateAsync(key with { RevokedAt = _time.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
            _context.Events.Publish(new ApiKeyChangedEvent(key.Id, key.UserId, true));
        }
        return true;
    }

    /// <summary>Keys of a user.</summary>
    public Task<IReadOnlyList<WebApiKey>> ListAsync(string? userId, CancellationToken cancellationToken) => _store.ListAsync(userId, cancellationToken);
}
