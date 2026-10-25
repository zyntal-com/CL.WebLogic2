namespace CL.WebLogic.Security.Identity;

/// <summary>Well-known values of <see cref="WebIdentity.Scheme"/>.</summary>
public static class WebAuthenticationSchemes
{
    /// <summary>Not authenticated.</summary>
    public const string None = "none";

    /// <summary>Session cookie.</summary>
    public const string Session = "session";

    /// <summary>API key.</summary>
    public const string ApiKey = "apikey";

    /// <summary>Bearer token.</summary>
    public const string Bearer = "bearer";

    /// <summary>Header set by a trusted authenticating proxy.</summary>
    public const string TrustedHeader = "trusted-header";

    /// <summary>An ASP.NET Core authentication scheme (Negotiate, JwtBearer…).</summary>
    public const string AspNet = "aspnet";
}
