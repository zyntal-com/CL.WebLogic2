namespace CL.WebLogic.Runtime.Models;

/// <summary>Twitter / X card tags.</summary>
public sealed class WebTwitterMeta
{
    /// <summary>twitter:card, e.g. <c>summary_large_image</c>.</summary>
    public string? Card { get; init; }

    /// <summary>twitter:title.</summary>
    public string? Title { get; init; }

    /// <summary>twitter:description.</summary>
    public string? Description { get; init; }

    /// <summary>twitter:image.</summary>
    public string? Image { get; init; }

    /// <summary>twitter:site.</summary>
    public string? Site { get; init; }

    /// <summary>twitter:creator.</summary>
    public string? Creator { get; init; }
}
