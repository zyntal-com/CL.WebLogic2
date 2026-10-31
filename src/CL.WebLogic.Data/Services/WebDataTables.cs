using CL.Data.Mapping;
using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Models;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Data.Services;

/// <summary>Tables to create or update at startup on one connection (collected by <see cref="WebDataSchemaService"/>).</summary>
/// <param name="ConnectionName">The CL.Data connection name.</param>
/// <param name="Maps">Resolves the entity maps of the row types (the CL.Data generator registers them).</param>
public sealed record WebDataTables(string ConnectionName, Func<IReadOnlyList<Result<EntityMap>>> Maps)
{
    /// <summary>The map of row type <typeparamref name="T"/>.</summary>
    public static Result<EntityMap> Map<T>()
    {
        var accessor = EntityAccessorRegistry.Get<T>();
        return accessor.IsSuccess ? Result<EntityMap>.Success(accessor.Value!.Map) : Result<EntityMap>.Failure(accessor.Error!);
    }

    /// <summary>
    /// The tables of the stores in <paramref name="stores"/> (as passed to <c>UseDataStores</c>), plus the DataProtection key
    /// ring table with <paramref name="keyRing"/> (<c>PersistKeysToDatabase</c>). Startup sync uses the same lists; migration
    /// tools use them to script exactly the tables an application runs with
    /// (<c>db.Schema.ScriptAsync(maps)</c>).
    /// </summary>
    public static IReadOnlyList<Result<EntityMap>> For(WebDataStores stores, bool keyRing = false)
    {
        List<Result<EntityMap>> maps = [];
        foreach (var (flag, rows) in StoreTables)
            if (stores.HasFlag(flag))
                maps.AddRange(rows.Select(row => row()));
        if (keyRing)
            maps.Add(Map<DataProtectionKeyRow>());
        return maps;
    }

    /// <summary>Each store's row types, in registration order.</summary>
    internal static readonly IReadOnlyList<(WebDataStores Flag, Func<Result<EntityMap>>[] Rows)> StoreTables =
    [
        (WebDataStores.Users, [Map<UserRow>, Map<UserLoginRow>]),
        (WebDataStores.Sessions, [Map<SessionRow>]),
        (WebDataStores.ApiKeys, [Map<ApiKeyRow>]),
        (WebDataStores.RecoveryCodes, [Map<RecoveryCodeRow>]),
        (WebDataStores.OneTimeUse, [Map<OneTimeUseRow>]),
        (WebDataStores.LoginThrottle, [Map<LoginThrottleRow>]),
        (WebDataStores.RateLimits, [Map<RateLimitRow>]),
        (WebDataStores.Audit, [Map<AuditRow>]),
        (WebDataStores.WidgetSettings, [Map<WidgetSettingsRow>]),
        (WebDataStores.Dashboards, [Map<DashboardRow>]),
        (WebDataStores.Passkeys, [Map<PasskeyRow>]),
    ];
}
