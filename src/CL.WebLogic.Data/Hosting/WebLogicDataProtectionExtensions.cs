using CL.WebLogic.Data.Entities;
using CL.WebLogic.Data.Models;
using CL.WebLogic.Data.Services;
using CL.WebLogic.Data.Stores;
using CL.WebLogic.Hosting.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CL.WebLogic.Data.Hosting;

/// <summary>DataProtection key ring in the database.</summary>
public static class WebLogicDataProtectionExtensions
{
    /// <summary>
    /// Stores the DataProtection key ring in <c>wl_dataprotection_keys</c> on the CL.Data connection
    /// <paramref name="connectionName"/> (created at startup), so every node shares it. Combine with
    /// <c>SetApplicationName</c> and <c>ProtectKeysWithSecret</c>.
    /// </summary>
    public static IDataProtectionBuilder PersistKeysToDatabase(this IDataProtectionBuilder builder, string connectionName = "Default")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
        {
            var db = new Lazy<WebDataDatabase>(() => new WebDataDatabase(services.GetRequiredService<IWebLibraryResolver>(), new WebDataOptions { ConnectionName = connectionName }));
            return new ConfigureOptions<KeyManagementOptions>(options => options.XmlRepository = new DataXmlRepository(() => db.Value));
        });
        WebLogicDataBuilderExtensions.AddTables(builder.Services, new WebDataTables(connectionName, () => [WebDataTables.Map<DataProtectionKeyRow>()]));
        return builder;
    }
}
