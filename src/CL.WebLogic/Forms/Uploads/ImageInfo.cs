namespace CL.WebLogic.Forms.Uploads;

/// <summary>Format and size of an image.</summary>
public readonly record struct ImageInfo(string ContentType, int Width, int Height);
