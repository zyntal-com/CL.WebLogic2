using CL.WebLogic.Caching.Models;
using CL.WebLogic.Caching.Services;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Tests.Caching;

public sealed class CachingPrimitivesTests
{
    private static MemoryFragmentCache Cache() => new(new WebLogicConfigHolder(new WebLogicConfig()));

    [Fact]
    public async Task Concurrent_misses_run_the_factory_once()
    {
        using var cache = Cache();
        var calls = 0;
        var gate = new TaskCompletionSource();
        async ValueTask<string> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return "value";
        }

        var tasks = Enumerable.Range(0, 20).Select(_ => cache.GetOrAddAsync("k", Factory, null, CancellationToken.None).AsTask()).ToList();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Equal("value", r));
        Assert.Equal("value", await cache.GetOrAddAsync<string>("k", _ => throw new InvalidOperationException("cached"), null, CancellationToken.None));
    }

    [Fact]
    public async Task Failures_are_not_cached()
    {
        using var cache = Cache();
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrAddAsync<int>("k", _ => throw new InvalidOperationException(), null, CancellationToken.None).AsTask());
        Assert.Equal(7, await cache.GetOrAddAsync("k", _ => ValueTask.FromResult(7), null, CancellationToken.None));
    }

    [Fact]
    public async Task Tags_evict_their_entries()
    {
        using var cache = Cache();
        await cache.GetOrAddAsync("post:1", _ => ValueTask.FromResult("a"), new WebFragmentCacheOptions { Tags = ["posts", "post:1"] }, CancellationToken.None);
        await cache.GetOrAddAsync("post:2", _ => ValueTask.FromResult("b"), new WebFragmentCacheOptions { Tags = ["posts"] }, CancellationToken.None);
        await cache.GetOrAddAsync("menu", _ => ValueTask.FromResult("c"), new WebFragmentCacheOptions { Tags = ["menu"] }, CancellationToken.None);

        Assert.Equal(2, cache.EvictTags(["posts"]));
        Assert.Equal("new", await cache.GetOrAddAsync("post:1", _ => ValueTask.FromResult("new"), null, CancellationToken.None));
        Assert.Equal("c", await cache.GetOrAddAsync("menu", _ => ValueTask.FromResult("other"), null, CancellationToken.None));
    }

    [Fact]
    public void Output_cache_refuses_personal_or_stateful_responses()
    {
        static DefaultHttpContext Http(Action<HttpResponse>? configure = null)
        {
            var http = new DefaultHttpContext();
            http.Response.StatusCode = 200;
            configure?.Invoke(http.Response);
            return http;
        }

        Assert.True(WebLogicOutputCachePolicy.IsStorable(Http()));
        Assert.False(WebLogicOutputCachePolicy.IsStorable(Http(r => r.StatusCode = 404)));
        Assert.False(WebLogicOutputCachePolicy.IsStorable(Http(r => r.Headers.SetCookie = "a=b")));
        Assert.False(WebLogicOutputCachePolicy.IsStorable(Http(r => r.Headers.CacheControl = "private")));
        Assert.False(WebLogicOutputCachePolicy.IsStorable(Http(r => r.Headers.CacheControl = "no-store")));
    }
}
