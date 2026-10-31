using System.Linq.Expressions;
using CL.Data.Errors;
using CL.Data.Execution;
using CL.Data.Hosting;
using CL.Data.Query;
using CL.Data.Routing;
using CL.WebLogic.Data.Models;
using CL.WebLogic.Hosting.Services;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Data.Services;

/// <summary>
/// The CL.Data connection the stores use, plus helpers that turn failed results into exceptions
/// (a store that cannot reach its database must fail the request, never pretend that a session or
/// key does not exist).
/// </summary>
/// <remarks>
/// Every store reads and writes the primary: these rows are security state (sessions, lockouts,
/// one-time keys), so a lagging replica or a cached copy could undo a sign-out or a revocation.
/// Key lookups go through <see cref="FindAsync{T}"/>, which never uses the CL.Data entity cache.
/// </remarks>
public sealed class WebDataDatabase
{
    private readonly IWebLibraryResolver _libraries;

    /// <summary>Creates the accessor.</summary>
    public WebDataDatabase(IWebLibraryResolver libraries, WebDataOptions options)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        ArgumentNullException.ThrowIfNull(options);
        ConnectionName = options.ConnectionName;
    }

    /// <summary>The CL.Data connection name (<c>config.data.json</c> → <c>connections</c>).</summary>
    public string ConnectionName { get; }

    /// <summary>The connection, routed to the primary.</summary>
    public Database Db
    {
        get
        {
            var data = _libraries.GetLibrary<DataLibrary>()
                ?? throw new InvalidOperationException("CodeLogic.WebLogic.Data needs CL.Data: load DataLibrary and a provider library before WebLogic.");
            return Value(data.Db(ConnectionName)).ReadFrom(ReadPreference.Primary);
        }
    }

    /// <summary>A repository for <typeparamref name="T"/> on the connection.</summary>
    public Repository<T> Repository<T>() => Db.Repository<T>();

    /// <summary>A query for <typeparamref name="T"/> on the connection.</summary>
    public QueryBuilder<T> Query<T>() => Db.Query<T>();

    /// <summary>The first row matching <paramref name="predicate"/>, or null (never from a cache).</summary>
    public async Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) where T : class =>
        Value(await Query<T>().Where(predicate).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Runs <paramref name="work"/> in a transaction and commits it; a failed commit throws like any
    /// other failure, so a caller never assumes a write that was rolled back.
    /// </summary>
    public async Task<TResult> InTransactionAsync<TResult>(Func<DataTransaction, CancellationToken, Task<TResult>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var transaction = Value(await Db.BeginTransactionAsync(ct: cancellationToken).ConfigureAwait(false));
        var result = await work(transaction, cancellationToken).ConfigureAwait(false);
        Value(await transaction.CommitAsync(cancellationToken).ConfigureAwait(false));
        return result;
    }

    /// <summary>True when <paramref name="error"/> is a unique-key violation (a duplicate row).</summary>
    public static bool IsDuplicate(Error? error) => error?.Code == DataErrors.DuplicateKeyCode;

    /// <summary>The value of <paramref name="result"/>, or an exception with its error.</summary>
    public static T Value<T>(Result<T> result) =>
        result.IsSuccess ? result.Value! : throw new InvalidOperationException("WebLogic data store: " + (result.Error?.Message ?? "unknown error"));

    /// <summary>Throws when <paramref name="result"/> failed.</summary>
    public static void Value(Result result)
    {
        if (result.IsFailure)
            throw new InvalidOperationException("WebLogic data store: " + (result.Error?.Message ?? "unknown error"));
    }

    /// <summary>UTC <see cref="DateTime"/> for a column.</summary>
    public static DateTime Utc(DateTimeOffset value) => value.UtcDateTime;

    /// <summary>A column value back to <see cref="DateTimeOffset"/>.</summary>
    public static DateTimeOffset FromUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
