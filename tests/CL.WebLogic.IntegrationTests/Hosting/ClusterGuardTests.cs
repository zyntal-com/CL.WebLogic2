using CL.WebLogic.IntegrationTests.Security;

namespace CL.WebLogic.IntegrationTests.Hosting;

public sealed class ClusterGuardTests
{
    [Fact]
    public async Task A_cluster_node_with_in_memory_state_does_not_start()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SecurityTestSite.StartAsync(c => c.Cluster.Enabled = true));

        Assert.Contains("Cluster.Enabled", error.Message);
        Assert.Contains("IWebOneTimeUseStore", error.Message);
        Assert.Contains("IWebRealtimeBackplane", error.Message);
        Assert.Contains("DataProtection keys", error.Message);
    }

    [Fact]
    public async Task Without_cluster_mode_the_same_host_starts()
    {
        await using var host = await SecurityTestSite.StartAsync();
        Assert.DoesNotContain(host.Context.Log.Entries, e => e.Message.StartsWith("Cluster:", StringComparison.Ordinal));
    }
}
