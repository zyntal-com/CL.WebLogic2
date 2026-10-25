namespace CL.WebLogic.Forms.Uploads;

/// <summary>A saved upload.</summary>
public sealed record WebStoredFile(string Path, string FileName, long Length, string ContentType);
