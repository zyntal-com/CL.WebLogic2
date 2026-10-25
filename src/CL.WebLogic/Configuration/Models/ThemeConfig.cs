using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Theme location and static asset serving.</summary>
public sealed class ThemeConfig
{
    /// <summary>Where the theme files come from.</summary>
    [ConfigField(Label = "Source", Description = "Local folder, Git repository or CL.Storage connection.",
        RequiresRestart = true, Group = "Theme", Order = 10)]
    public ThemeSourceKind Source { get; set; } = ThemeSourceKind.Local;

    /// <summary>Theme folder when <see cref="Source"/> is Local. Relative paths resolve against the content root.</summary>
    [ConfigField(Label = "Local Path", Description = "Theme folder (relative to the app's content root, or absolute).",
        Placeholder = "theme", RequiresRestart = true, Group = "Theme", Order = 11)]
    public string LocalPath { get; set; } = "theme";

    /// <summary>Stable id of the theme repository when <see cref="Source"/> is Git (also the cache folder name).</summary>
    [ConfigField(Label = "Repository ID", Description = "Identifier of the theme repository; the clone lives in {data}/themes/{id}.",
        Placeholder = "weblogic-theme", RequiresRestart = true, Group = "Theme", Order = 30, Collapsed = true)]
    public string RepositoryId { get; set; } = "weblogic-theme";

    /// <summary>Theme repository URL when <see cref="Source"/> is Git. Credentials belong in the CL.GitHelper config.</summary>
    [ConfigField(Label = "Repository URL", InputType = ConfigInputType.Url, Description = "HTTPS or SSH URL of the theme Git repository.",
        Placeholder = "https://github.com/org/theme.git", RequiresRestart = true, Group = "Theme", Order = 31, Collapsed = true)]
    public string RepositoryUrl { get; set; } = "";

    /// <summary>Branch to check out when <see cref="Source"/> is Git.</summary>
    [ConfigField(Label = "Branch", Group = "Theme", Order = 32, Collapsed = true)]
    public string Branch { get; set; } = "main";

    /// <summary>Folder inside the repository that contains the theme (empty = repository root).</summary>
    [ConfigField(Label = "Theme Sub-path", Description = "Folder inside the repository that holds the theme.",
        RequiresRestart = true, Group = "Theme", Order = 33, Collapsed = true)]
    public string ThemeSubPath { get; set; } = "";

    /// <summary>CL.Storage connection id when <see cref="Source"/> is Storage.</summary>
    [ConfigField(Label = "Storage Connection", Description = "CL.Storage connection the theme is downloaded from.",
        Placeholder = "Default", RequiresRestart = true, Group = "Theme", Order = 34, Collapsed = true)]
    public string StorageConnectionId { get; set; } = "Default";

    /// <summary>Folder in the storage connection that holds the theme.</summary>
    [ConfigField(Label = "Storage Path", Description = "Folder in the storage connection that holds the theme.",
        Placeholder = "themes/site", RequiresRestart = true, Group = "Theme", Order = 35, Collapsed = true)]
    public string StoragePath { get; set; } = "theme";

    /// <summary>
    /// Folder inside the theme that holds public static files. Only files under this folder
    /// are ever served — templates, layouts and partials live outside it and are unreachable.
    /// </summary>
    [ConfigField(Label = "Assets Folder", Description = "Folder inside the theme whose files are served publicly. Nothing outside it is ever served.",
        Placeholder = "assets", RequiresRestart = true, Group = "Theme", Order = 12)]
    public string AssetsFolder { get; set; } = "assets";

    /// <summary>URL prefix the assets folder is served under.</summary>
    [ConfigField(Label = "Assets URL Prefix", Description = "URL path the assets folder is served under.",
        Placeholder = "/assets", RequiresRestart = true, Group = "Theme", Order = 13)]
    public string AssetsUrlPrefix { get; set; } = "/assets";

    /// <summary>Browser cache lifetime for un-fingerprinted assets, in seconds.</summary>
    [ConfigField(Label = "Asset Cache (s)", Description = "Cache-Control max-age for assets without a ?v= fingerprint. Fingerprinted assets are cached for a year.",
        Min = 0, Max = 31536000, Group = "Theme", Order = 14, Collapsed = true)]
    public int AssetCacheSeconds { get; set; } = 3600;

    /// <summary>File extensions that may be served from the assets folder.</summary>
    [ConfigField(Label = "Allowed Asset Extensions", Description = "Only files with these extensions are served from the assets folder.",
        Group = "Theme", Order = 15, Collapsed = true)]
    public List<string> AllowedAssetExtensions { get; set; } =
    [
        ".css", ".js", ".mjs", ".map", ".json", ".webmanifest", ".txt", ".xml",
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".svg", ".ico", ".bmp",
        ".woff", ".woff2", ".ttf", ".otf", ".eot",
        ".mp4", ".webm", ".mp3", ".ogg", ".wav", ".pdf"
    ];

    /// <summary>How templates are executed.</summary>
    [ConfigField(Label = "Template Engine", Description = "Closure (compiled delegates, default) or Interpreter (AST walk, the reference engine).",
        Group = "Templates", Order = 20, Collapsed = true)]
    public TemplateEngineKind TemplateEngine { get; set; } = TemplateEngineKind.Closure;

    /// <summary>How often a cached template's file is re-checked for changes, in seconds (0 = never).</summary>
    [ConfigField(Label = "Template Change Check (s)", Description = "Minimum seconds between checks of a cached template's file. 0 disables re-checking (production with immutable themes).",
        Min = 0, Max = 3600, Group = "Templates", Order = 21, Collapsed = true)]
    public int TemplateChangeCheckSeconds { get; set; } = 2;
}
