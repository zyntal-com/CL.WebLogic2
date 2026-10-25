using CL.WebLogic.Realtime.Models;
using CL.WebLogic.Realtime.Services;
using CodeLogic.Core.Results;
using StarterWebsite.Configuration;

namespace StarterWebsite.Widgets;

/// <summary>The live wall's recent messages (memory only) and their realtime broadcast.</summary>
public sealed class ShoutWall
{
    private const int Keep = 50;
    private readonly LinkedList<Shout> _shouts = new();
    private readonly Lock _lock = new();
    private readonly WebRealtimeHub _hub;
    private readonly StarterConfig _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the wall.</summary>
    public ShoutWall(WebRealtimeHub hub, StarterConfig config, TimeProvider time)
    {
        _hub = hub;
        _config = config;
        _time = time;
    }

    /// <summary>Longest accepted message.</summary>
    public int MaxLength => _config.MaxShoutLength;

    /// <summary>Newest first.</summary>
    public IReadOnlyList<Shout> Recent()
    {
        lock (_lock)
            return [.. _shouts];
    }

    /// <summary>Validates, keeps and broadcasts a message to everyone (Public audience).</summary>
    public async Task<Result> PostAsync(string author, string text, CancellationToken cancellationToken)
    {
        text = text.Trim();
        if (text.Length == 0 || text.Length > MaxLength)
            return Result.Failure(Error.Validation("starter.wall.length", $"Write 1–{MaxLength} characters."));
        var shout = new Shout(author, text, _time.GetUtcNow());
        lock (_lock)
        {
            _shouts.AddFirst(shout);
            while (_shouts.Count > Keep)
                _shouts.RemoveLast();
        }
        await _hub.PublishAsync("starter.shout", new { author = shout.Author, text = shout.Text, at = shout.At }, WebRealtimeAudience.Public, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>Removes every message (moderators).</summary>
    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
            _shouts.Clear();
        await _hub.PublishAsync("starter.wall-cleared", null, WebRealtimeAudience.Public, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
