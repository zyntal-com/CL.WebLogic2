using CodeLogic.Core.Results;

namespace CL.WebLogic.Forms.Uploads;

/// <summary>Saves accepted uploads. The default saves to a CL.Storage connection; apps can save anywhere.</summary>
public interface IWebUploadSink
{
    /// <summary>Saves <paramref name="file"/> under <paramref name="folder"/> with a generated name; returns the stored path.</summary>
    Task<Result<WebStoredFile>> SaveAsync(WebUploadedFile file, string? folder, CancellationToken cancellationToken);
}
