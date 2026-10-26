using CL.MySQL2;
using CL.MySQL2.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.MySql.Models;
using CodeLogic.Core.Results;

namespace CL.WebLogic.MySql.Services;

/// <summary>
/// The CL.MySQL2 library and connection the stores use, plus small helpers that turn failed
/// results into exceptions (a store that cannot reach its database must fail the request, never
/// pretend that a session or key does not exist).
/// </summary>
public sealed class WebMySqlDatabase
{
    private readonly IWebLibraryResolver _libraries;

    /// <summary>Creates the accessor.</summary>
    public WebMySqlDatabase(IWebLibraryResolver libraries, WebMySqlOptions options)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        ArgumentNullException.ThrowIfNull(options);
        ConnectionId = options.ConnectionId;
    }

    /// <summary>The CL.MySQL2 connection id.</summary>
    public string ConnectionId { get; }

    /// <summary>The loaded CL.MySQL2 library.</summary>
    public MySQL2Library MySql => _libraries.GetLibrary<MySQL2Library>()
        ?? throw new InvalidOperationException("CodeLogic.WebLogic.MySql needs CL.MySQL2: load MySQL2Library before WebLogic.");

    /// <summary>A repository for <typeparamref name="T"/> on the connection.</summary>
    public Repository<T> Repository<T>() where T : class, new() => MySql.GetRepository<T>(ConnectionId);

    /// <summary>A query for <typeparamref name="T"/> on the connection.</summary>
    public QueryBuilder<T> Query<T>() where T : class, new() => MySql.Query<T>(ConnectionId);

    /// <summary>Runs a parameterized statement and returns the affected rows.</summary>
    public async Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken) =>
        Value(await MySql.ExecuteSqlAsync(sql, parameters, ConnectionId, cancellationToken).ConfigureAwait(false));

    /// <summary>Runs a parameterized query returning one value.</summary>
    public async Task<T?> ScalarAsync<T>(string sql, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken) =>
        Value(await MySql.SqlScalarAsync<T>(sql, parameters, ConnectionId, cancellationToken).ConfigureAwait(false));

    /// <summary>The value of <paramref name="result"/>, or an exception with its error.</summary>
    public static T Value<T>(Result<T> result) =>
        result.IsSuccess ? result.Value! : throw new InvalidOperationException("MySQL store: " + (result.Error?.Message ?? "unknown error"));

    /// <summary>UTC <see cref="DateTime"/> for a column.</summary>
    public static DateTime Utc(DateTimeOffset value) => value.UtcDateTime;

    /// <summary>A column value back to <see cref="DateTimeOffset"/>.</summary>
    public static DateTimeOffset FromUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
