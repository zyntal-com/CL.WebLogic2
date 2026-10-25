using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Forms: rendering, anti-bot checks, uploads and lookups.</summary>
public sealed class FormsConfig
{
    /// <summary>Default renderer: <c>bootstrap5</c> or <c>plain</c>.</summary>
    [ConfigField(Label = "Renderer", Description = "bootstrap5 or plain", Group = "Forms", Order = 1)]
    public string Renderer { get; set; } = "bootstrap5";

    /// <summary>Add a hidden honeypot field; submissions that fill it are rejected.</summary>
    [ConfigField(Label = "Honeypot", Group = "Forms", Order = 2)]
    public bool Honeypot { get; set; } = true;

    /// <summary>Reject submissions faster than this after the form was rendered (0 = off).</summary>
    [ConfigField(Label = "Min Submit (s)", Min = 0, Max = 60, Group = "Forms", Order = 3)]
    public int MinSubmitSeconds { get; set; } = 2;

    /// <summary>Reject submissions of forms rendered longer ago than this.</summary>
    [ConfigField(Label = "Max Form Age (min)", Min = 5, Max = 10080, Group = "Forms", Order = 4, Collapsed = true)]
    public int MaxFormAgeMinutes { get; set; } = 480;

    /// <summary>Default maximum upload size per file.</summary>
    [ConfigField(Label = "Max File Size (bytes)", Min = 1024, Max = 2147483647, Group = "Forms", Order = 5)]
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Default allowed upload extensions.</summary>
    [ConfigField(Label = "Allowed Extensions", Group = "Forms", Order = 6, Collapsed = true)]
    public List<string> AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".txt", ".csv", ".docx", ".xlsx"];

    /// <summary>CL.Storage connection uploads are saved to by the default upload sink.</summary>
    [ConfigField(Label = "Upload Storage Connection", Group = "Forms", Order = 7, Collapsed = true)]
    public string UploadStorageConnection { get; set; } = "Default";

    /// <summary>Folder (in the storage connection) uploads are saved under.</summary>
    [ConfigField(Label = "Upload Folder", Group = "Forms", Order = 8, Collapsed = true)]
    public string UploadFolder { get; set; } = "uploads";

    /// <summary>Rate-limit policy for remote field validation and option lookups.</summary>
    [ConfigField(Label = "Endpoint Rate Policy", Group = "Forms", Order = 9, Collapsed = true)]
    public string EndpointRatePolicy { get; set; } = "forms";

    /// <summary>Hosts HTTP lookup providers may call (exact host names).</summary>
    [ConfigField(Label = "Lookup Hosts", Description = "Hosts that HTTP option lookups may call.", Group = "Forms", Order = 10, Collapsed = true)]
    public List<string> AllowedLookupHosts { get; set; } = [];
}
