namespace CL.WebLogic.Configuration.Models;

/// <summary>Where the theme files come from.</summary>
public enum ThemeSourceKind
{
    /// <summary>A folder on disk (relative paths resolve against the app's content root).</summary>
    Local,

    /// <summary>A Git repository synchronized through CL.GitHelper.</summary>
    Git,

    /// <summary>A CL.Storage connection (local disk, S3, Azure, FTP…).</summary>
    Storage
}
