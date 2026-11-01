using CL.Data;

namespace CL.WebLogic.Data.Entities;

/// <summary>A passkey: the fields that change get columns (for atomic updates), the rest is JSON.</summary>
[Table(Name = "wl_passkeys")]
[MySqlTable(Collation = WebDataCollations.MySql)]
public sealed class PasskeyRow
{
    /// <summary>SHA-256 (hex) of the credential id (ids can be longer than an index allows).</summary>
    [Column(Name = "id_hash", DataType = DataType.FixedString, Size = 64, Primary = true, NotNull = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string IdHash { get; set; } = string.Empty;

    /// <summary>The credential id (base64url).</summary>
    [Column(Name = "credential_id", DataType = DataType.Text, NotNull = true)]
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>Owner.</summary>
    [Column(Name = "user_id", DataType = DataType.String, Size = 64, NotNull = true, Index = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Last signature counter.</summary>
    [Column(Name = "sign_count", DataType = DataType.BigInt, NotNull = true)]
    public long SignCount { get; set; }

    /// <summary>1 when backed up at the last use.</summary>
    [Column(Name = "backed_up", DataType = DataType.TinyInt, NotNull = true)]
    public int BackedUp { get; set; }

    /// <summary>Display name.</summary>
    [Column(Name = "name", DataType = DataType.String, Size = 64, NotNull = true)]
    [SqlServerColumn(Collation = WebDataCollations.SqlServer)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Registration time (UTC).</summary>
    [Column(Name = "created_utc", DataType = DataType.DateTime, NotNull = true)]
    public DateTime CreatedUtc { get; set; }

    /// <summary>Last sign-in (UTC).</summary>
    [Column(Name = "last_used_utc", DataType = DataType.DateTime)]
    public DateTime? LastUsedUtc { get; set; }

    /// <summary>The rest of the WebPasskey (public key, user handle, attestation…) as JSON.</summary>
    [Column(Name = "record_json", DataType = DataType.Text, NotNull = true)]
    public string RecordJson { get; set; } = "{}";
}
