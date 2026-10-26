namespace CL.WebLogic.ExternalLogins.OAuth;

/// <summary>How the client authenticates at the token endpoint.</summary>
public enum WebOAuthClientAuthentication
{
    /// <summary><c>client_id</c> and <c>client_secret</c> in the form body.</summary>
    Post,

    /// <summary>HTTP Basic with the client id and secret.</summary>
    Basic
}
