using CL.WebLogic.Runtime.Models;

namespace StarterWebsite.Pages;

/// <summary>Small helpers shared by the starter's page handlers.</summary>
internal static class PageModels
{
    /// <summary>Page metadata with the given title.</summary>
    public static WebPageMeta Meta(string title, string? description = null) => new() { Title = title, Description = description };
}
