using CL.Data.Hosting;
using CL.Data.Mapping;
using CL.WebLogic.Hosting.Services;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.Data.Services;

/// <summary>
/// Creates or updates the <c>wl_*</c> tables before the app takes requests (CL.Data schema sync at
/// the connection's sync mode; unchanged tables are skipped). A failure stops startup with the
/// reason; drift (changes the sync mode does not allow) is logged as a warning.
/// </summary>
public sealed class WebDataSchemaService : IHostedService
{
    private readonly IReadOnlyList<WebDataTables> _tables;
    private readonly IWebLibraryResolver _libraries;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the service.</summary>
    public WebDataSchemaService(IEnumerable<WebDataTables> tables, IWebLibraryResolver libraries, IWebLogicContext context)
    {
        _tables = [.. tables ?? []];
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var group in _tables.GroupBy(t => t.ConnectionName, StringComparer.OrdinalIgnoreCase))
        {
            List<EntityMap> maps = [];
            foreach (var map in group.SelectMany(t => t.Maps()))
            {
                if (map.IsFailure)
                    throw new InvalidOperationException($"WebLogic data tables on '{group.Key}': {map.Error?.Message}");
                if (!maps.Contains(map.Value!))
                    maps.Add(map.Value!);
            }
            if (maps.Count == 0)
                continue;

            var data = _libraries.GetLibrary<DataLibrary>()
                ?? throw new InvalidOperationException("CodeLogic.WebLogic.Data needs CL.Data: load DataLibrary and a provider library before WebLogic.");
            var db = WebDataDatabase.Value(data.Db(group.Key));
            var report = WebDataDatabase.Value(await db.Schema.SyncAsync(maps, ct: cancellationToken).ConfigureAwait(false));
            foreach (var table in report.Tables.Where(t => t.Pending.Count > 0))
                _context.Logger.Warning($"WebLogic table {table.Table} on '{group.Key}' differs from the model and was not changed (sync mode): {string.Join("; ", table.Pending)}");
            _context.Logger.Info($"WebLogic data tables ready on '{group.Key}' ({maps.Count} tables, {db.Provider.Name}).");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
