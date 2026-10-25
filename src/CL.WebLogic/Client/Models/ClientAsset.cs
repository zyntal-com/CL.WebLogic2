namespace CL.WebLogic.Client.Models;

/// <summary>One embedded client file with its precompressed variants.</summary>
public sealed class ClientAsset
{
    /// <summary>Logical name, e.g. <c>weblogic.esm.js</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Fingerprinted name, e.g. <c>weblogic.esm.3fa1c2b9d0e4.js</c>.</summary>
    public required string HashedName { get; init; }

    /// <summary>Content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>Short content hash.</summary>
    public required string Hash { get; init; }

    /// <summary>Subresource-integrity value (<c>sha384-…</c>).</summary>
    public required string Integrity { get; init; }

    /// <summary>Uncompressed bytes.</summary>
    public required byte[] Content { get; init; }

    /// <summary>Brotli bytes, when embedded.</summary>
    public byte[]? Brotli { get; init; }

    /// <summary>Gzip bytes, when embedded.</summary>
    public byte[]? Gzip { get; init; }

    /// <summary>Strong ETag.</summary>
    public string ETag => "\"" + Hash + "\"";
}
