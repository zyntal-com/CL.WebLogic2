namespace CL.WebLogic.Runtime.Models;

/// <summary>Thrown when a request body exceeds the route's limit. The exception boundary maps it to 413.</summary>
public sealed class WebRequestTooLargeException : Exception
{
    /// <summary>Creates the exception for a body larger than <paramref name="limitBytes"/>.</summary>
    public WebRequestTooLargeException(long limitBytes)
        : base($"The request body exceeds the {limitBytes}-byte limit for this route.")
    {
        LimitBytes = limitBytes;
    }

    /// <summary>The limit that was exceeded.</summary>
    public long LimitBytes { get; }
}
