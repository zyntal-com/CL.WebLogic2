namespace CL.WebLogic.Realtime.Models;

/// <summary>Who receives a realtime message.</summary>
public enum WebRealtimeAudienceKind
{
    /// <summary>Every connection, including anonymous ones.</summary>
    Public,
    /// <summary>Signed-in connections.</summary>
    Authenticated,
    /// <summary>Connections whose user is in a group.</summary>
    Group,
    /// <summary>Connections whose user has a permission (wildcards honoured).</summary>
    Permission,
    /// <summary>Connections of one user.</summary>
    User,
    /// <summary>Signed-in connections of one tenant.</summary>
    Tenant,
    /// <summary>Operators: connections with <c>Realtime.InternalPermission</c>.</summary>
    Internal
}
