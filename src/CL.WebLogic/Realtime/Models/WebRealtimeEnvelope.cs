namespace CL.WebLogic.Realtime.Models;

/// <summary>
/// A published message: routing data plus the serialized client frame
/// (<c>{ id, type, channel, data, timestamp }</c>), serialized once and shared by all receivers.
/// Plain strings only, so backplanes can forward it as is. A message with a
/// <see cref="TenantId"/> only reaches connections of that tenant, whatever the audience.
/// </summary>
public sealed record WebRealtimeEnvelope(string Id, string Type, string? Channel, WebRealtimeAudience Audience, string Json, DateTimeOffset CreatedAt, string? TenantId = null);
