using CL.MySQL2;
using CL.WebLogic.Hosting.Services;
using Microsoft.Extensions.Hosting;

namespace CL.WebLogic.MySql.Services;

/// <summary>
/// Creates or updates the <c>wl_*</c> tables before the app takes requests (CL.MySQL2 schema
/// sync: additive, unchanged tables are skipped). A failure stops startup with the reason.
/// </summary>
public sealed class WebMySqlSchemaService : IHostedService
{
    private readonly IReadOnlyList<WebMySqlTables> _tables;
    private readonly IWebLibraryResolver _libraries;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the service.</summary>
    public WebMySqlSchemaService(IEnumerable<WebMySqlTables> tables, IWebLibraryResolver libraries, IWebLogicContext context)
    {
        _tables = [.. tables ?? []];
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var group in _tables.GroupBy(t => t.ConnectionId, StringComparer.Ordinal))
        {
            var types = group.SelectMany(t => t.EntityTypes).Distinct().ToArray();
            if (types.Length == 0)
                continue;
            var mysql = _libraries.GetLibrary<MySQL2Library>()
                ?? throw new InvalidOperationException("CodeLogic.WebLogic.MySql needs CL.MySQL2: load MySQL2Library before WebLogic.");
            var result = await mysql.SyncSchemaAsync(types, false, group.Key, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
                throw new InvalidOperationException($"WebLogic MySQL schema sync on '{group.Key}' failed: {result.Error?.Message}");
            _context.Logger.Info($"WebLogic MySQL tables ready on '{group.Key}' ({types.Length} tables).");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
