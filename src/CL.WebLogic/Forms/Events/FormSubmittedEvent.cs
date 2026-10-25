using CodeLogic.Core.Events;

namespace CL.WebLogic.Forms.Events;

/// <summary>A form was submitted and validated (<paramref name="Valid"/> tells whether it passed).</summary>
public sealed record FormSubmittedEvent(string FormId, bool Valid, string Path) : IEvent;
