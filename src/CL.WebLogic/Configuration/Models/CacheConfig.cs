using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Output and fragment caching.</summary>
public sealed class CacheConfig
{
    /// <summary>Enable the output cache (routes opt in with <c>WithOutputCache</c>).</summary>
    [ConfigField(Label = "Output Cache", Group = "Caching", Order = 1, RequiresRestart = true)]
    public bool OutputCache { get; set; } = true;

    /// <summary>Lifetime of the built-in <c>default</c> output-cache policy.</summary>
    [ConfigField(Label = "Default Output Seconds", Min = 1, Max = 86400, Group = "Caching", Order = 2, RequiresRestart = true)]
    public int DefaultOutputSeconds { get; set; } = 60;

    /// <summary>Maximum entries in the fragment cache.</summary>
    [ConfigField(Label = "Fragment Entries", Min = 16, Max = 1000000, Group = "Caching", Order = 3, RequiresRestart = true)]
    public int FragmentMaxEntries { get; set; } = 10000;

    /// <summary>Default fragment lifetime.</summary>
    [ConfigField(Label = "Default Fragment Seconds", Min = 1, Max = 86400, Group = "Caching", Order = 4)]
    public int DefaultFragmentSeconds { get; set; } = 300;
}
