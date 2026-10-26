namespace CL.WebLogic.Auth.Passkeys.Cbor;

/// <summary>Malformed or unsupported CBOR, or a value of an unexpected shape.</summary>
public sealed class WebCborException : FormatException
{
    /// <summary>Creates the exception.</summary>
    public WebCborException()
    {
    }

    /// <summary>Creates the exception.</summary>
    public WebCborException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    public WebCborException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
