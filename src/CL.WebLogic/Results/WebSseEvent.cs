namespace CL.WebLogic.Results;

/// <summary>One server-sent event.</summary>
/// <param name="Data">Event payload (multi-line text is split into several <c>data:</c> lines).</param>
/// <param name="Event">Optional event name.</param>
/// <param name="Id">Optional event id (sent back by the browser as <c>Last-Event-ID</c>).</param>
/// <param name="RetryMs">Optional reconnect delay hint in milliseconds.</param>
public sealed record WebSseEvent(string Data, string? Event = null, string? Id = null, int? RetryMs = null);
