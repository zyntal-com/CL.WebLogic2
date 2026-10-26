using CL.WebLogic.Hosting.Services;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Models;
using CL.WebLogic.MySql.Services;
using CL.WebLogic.MySql.Stores;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CL.WebLogic.MySql.Hosting;

/// <summary>DataProtection key ring in MySQL.</summary>
public static class WebLogicMySqlDataProtectionExtensions
{
    /// <summary>
    /// Stores the DataProtection key ring in <c>wl_dataprotection_keys</c> on the CL.MySQL2
    /// connection <paramref name="connectionId"/> (created at startup), so every node shares it.
    /// Combine with <c>SetApplicationName</c> and <c>ProtectKeysWithSecret</c>.
    /// </summary>
    public static IDataProtectionBuilder PersistKeysToMySql(this IDataProtectionBuilder builder, string connectionId = "Default")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
        {
            var db = new Lazy<WebMySqlDatabase>(() => new WebMySqlDatabase(services.GetRequiredService<IWebLibraryResolver>(), new WebMySqlOptions { ConnectionId = connectionId }));
            return new ConfigureOptions<KeyManagementOptions>(options => options.XmlRepository = new MySqlXmlRepository(() => db.Value));
        });
        WebLogicMySqlBuilderExtensions.AddTables(builder.Services, new WebMySqlTables(connectionId, [typeof(DataProtectionKeyRow)]));
        return builder;
    }
}
