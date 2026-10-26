namespace CL.WebLogic.Auth.Passkeys.Cbor;

/// <summary>The kind of a decoded CBOR value.</summary>
public enum WebCborType
{
    /// <summary>An integer (major types 0 and 1), within the range of <see cref="long"/>.</summary>
    Number,

    /// <summary>A byte string.</summary>
    ByteString,

    /// <summary>A UTF-8 text string.</summary>
    TextString,

    /// <summary>An array.</summary>
    Array,

    /// <summary>A map.</summary>
    Map,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary><c>null</c>.</summary>
    Null,

    /// <summary><c>undefined</c>.</summary>
    Undefined,

    /// <summary>A half, single or double precision float.</summary>
    Real
}
