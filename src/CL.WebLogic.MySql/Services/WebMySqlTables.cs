namespace CL.WebLogic.MySql.Services;

/// <summary>Tables to create or update at startup on one connection (collected by <see cref="WebMySqlSchemaService"/>).</summary>
/// <param name="ConnectionId">The CL.MySQL2 connection id.</param>
/// <param name="EntityTypes">The row types.</param>
public sealed record WebMySqlTables(string ConnectionId, IReadOnlyList<Type> EntityTypes);
