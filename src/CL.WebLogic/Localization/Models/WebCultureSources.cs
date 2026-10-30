namespace CL.WebLogic.Localization.Models;

/// <summary>Names of the culture sources (<c>Localization.Sources</c> and <see cref="WebRequestCulture.Source"/>).</summary>
public static class WebCultureSources
{
    /// <summary>The query-string parameter (<c>Localization.QueryParameter</c>).</summary>
    public const string Query = "query";

    /// <summary>The culture cookie (<c>Localization.CookieName</c>).</summary>
    public const string Cookie = "cookie";

    /// <summary>The <c>Accept-Language</c> request header.</summary>
    public const string Header = "header";

    /// <summary>No source matched: <c>Localization.DefaultCulture</c>.</summary>
    public const string Default = "default";
}
