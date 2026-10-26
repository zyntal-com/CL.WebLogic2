namespace CL.WebLogic.Theming.Services;

/// <summary>One contributed static file, held in memory.</summary>
/// <param name="Content">The bytes.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="Fingerprint">Content hash used as the <c>?v=</c> value and the ETag.</param>
/// <param name="LastModified">When the contribution was registered.</param>
public sealed record WebContributedAsset(byte[] Content, string ContentType, string Fingerprint, DateTimeOffset LastModified);
