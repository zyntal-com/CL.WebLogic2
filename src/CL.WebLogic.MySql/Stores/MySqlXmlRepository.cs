using System.Xml.Linq;
using CL.MySQL2.Models;
using CL.WebLogic.MySql.Entities;
using CL.WebLogic.MySql.Services;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace CL.WebLogic.MySql.Stores;

/// <summary>
/// The DataProtection key ring in <c>wl_dataprotection_keys</c>, shared by every node. Keys are
/// written once and never overwritten. Register with
/// <c>services.AddDataProtection().PersistKeysToMySql()</c>; combine with
/// <c>ProtectKeysWithSecret</c> to encrypt them at rest.
/// </summary>
/// <remarks>
/// <see cref="IXmlRepository"/> is synchronous, so the calls block; DataProtection caches the ring
/// and reloads it rarely. Database errors throw instead of returning an empty ring (which would
/// mint a new key and make existing cookies unreadable).
/// </remarks>
public sealed class MySqlXmlRepository : IXmlRepository
{
    private readonly Func<WebMySqlDatabase> _db;

    /// <summary>Creates the repository; <paramref name="db"/> is resolved on each use.</summary>
    public MySqlXmlRepository(Func<WebMySqlDatabase> db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public IReadOnlyCollection<XElement> GetAllElements() =>
        Task.Run(async () =>
        {
            var rows = WebMySqlDatabase.Value(await _db().Repository<DataProtectionKeyRow>().GetAllAsync().ConfigureAwait(false));
            return (IReadOnlyCollection<XElement>)[.. rows.OrderBy(r => r.CreatedUtc).Select(r => XElement.Parse(r.Xml))];
        }).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        var name = new string((friendlyName ?? "").Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (name.Length is 0 or > 128)
            name = Guid.NewGuid().ToString("D");
        var outcome = Task.Run(async () => WebMySqlDatabase.Value(await _db().Repository<DataProtectionKeyRow>().InsertIgnoreAsync(new DataProtectionKeyRow
        {
            Name = name,
            Xml = element.ToString(SaveOptions.DisableFormatting),
            CreatedUtc = DateTime.UtcNow
        }).ConfigureAwait(false))).GetAwaiter().GetResult();
        if (outcome != InsertOutcome.Inserted)
            throw new InvalidOperationException($"DataProtection key '{name}' already exists in wl_dataprotection_keys.");
    }
}
