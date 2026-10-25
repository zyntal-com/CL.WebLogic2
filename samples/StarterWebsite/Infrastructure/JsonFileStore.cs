using System.Text.Json;

namespace StarterWebsite.Infrastructure;

/// <summary>
/// A keyed collection kept in memory and persisted as one JSON file. Reads are served from
/// memory; every change rewrites the file atomically (temp file + rename). Good for a
/// single-instance site with modest data — the point is that persistence is the app's choice,
/// not the toolkit's. Swap in a database store when that stops being true.
/// </summary>
public sealed class JsonFileStore<TValue> : IDisposable
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, TValue>? _items;

    /// <summary>A store backed by <paramref name="path"/>.</summary>
    public JsonFileStore(string path) => _path = path;

    /// <summary>The value for <paramref name="key"/>, or default.</summary>
    public async Task<TValue?> GetAsync(string key, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Load().GetValueOrDefault(key);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Every value matching <paramref name="predicate"/>.</summary>
    public async Task<List<TValue>> WhereAsync(Func<TValue, bool> predicate, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return [.. Load().Values.Where(predicate)];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Runs <paramref name="change"/> on the collection under the store's lock and saves when it
    /// returns <c>Changed = true</c>.
    /// </summary>
    public async Task<TResult> UpdateAsync<TResult>(Func<Dictionary<string, TValue>, (bool Changed, TResult Result)> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = Load();
            var (changed, result) = change(items);
            if (changed)
                await SaveAsync(items, cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Sets <paramref name="key"/> to <paramref name="value"/>.</summary>
    public Task SetAsync(string key, TValue value, CancellationToken cancellationToken) =>
        UpdateAsync(items =>
        {
            items[key] = value;
            return (true, true);
        }, cancellationToken);

    /// <summary>Removes the entries matching <paramref name="predicate"/>; returns how many.</summary>
    public Task<int> RemoveWhereAsync(Func<string, TValue, bool> predicate, CancellationToken cancellationToken) =>
        UpdateAsync(items =>
        {
            var keys = items.Where(p => predicate(p.Key, p.Value)).Select(p => p.Key).ToList();
            foreach (var key in keys)
                items.Remove(key);
            return (keys.Count > 0, keys.Count);
        }, cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    private Dictionary<string, TValue> Load()
    {
        if (_items is not null)
            return _items;
        _items = File.Exists(_path)
            ? JsonSerializer.Deserialize<Dictionary<string, TValue>>(File.ReadAllText(_path), Options) ?? []
            : [];
        return _items;
    }

    private async Task SaveAsync(Dictionary<string, TValue> items, CancellationToken cancellationToken)
    {
        var temp = _path + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, items, Options, cancellationToken).ConfigureAwait(false);
        File.Move(temp, _path, overwrite: true);
    }
}
