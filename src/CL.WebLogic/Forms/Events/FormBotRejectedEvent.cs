using CodeLogic.Core.Events;

namespace CL.WebLogic.Forms.Events;

/// <summary>A form submission was rejected by the anti-bot checks (honeypot, too fast, too old, tampered).</summary>
public sealed record FormBotRejectedEvent(string FormId, string Reason, string Path) : IEvent;
