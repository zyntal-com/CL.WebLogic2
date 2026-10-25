using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Default request size limits (routes override with <c>.WithBodyLimit(bytes)</c>).</summary>
public sealed class RequestLimitsConfig
{
    /// <summary>Default maximum request body.</summary>
    [ConfigField(Label = "Max Body (bytes)", Min = 1024, Max = 2147483647, Group = "Limits", Order = 70)]
    public long MaxBodyBytes { get; set; } = 1024 * 1024;

    /// <summary>Maximum form fields per request.</summary>
    [ConfigField(Label = "Max Form Fields", Min = 1, Max = 100000, Group = "Limits", Order = 71, Collapsed = true)]
    public int MaxFormFields { get; set; } = 1024;
}
