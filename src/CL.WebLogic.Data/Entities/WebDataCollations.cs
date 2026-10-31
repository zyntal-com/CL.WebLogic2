namespace CL.WebLogic.Data.Entities;

/// <summary>
/// Collations that keep keys case-sensitive (session hashes, API key prefixes, credential ids, login
/// keys). PostgreSQL and SQLite compare text exactly by default; MySQL and SQL Server need a binary
/// collation, or <c>"abc"</c> and <c>"ABC"</c> would be the same key.
/// </summary>
internal static class WebDataCollations
{
    /// <summary>MySQL: every <c>wl_*</c> table.</summary>
    public const string MySql = "utf8mb4_bin";

    /// <summary>SQL Server: key and lookup columns.</summary>
    public const string SqlServer = "Latin1_General_100_BIN2";
}
