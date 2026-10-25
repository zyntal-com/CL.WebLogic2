using CodeLogic.Core.Configuration;

namespace CL.WebLogic.Configuration.Models;

/// <summary>Password hashing (PBKDF2-SHA256) and the minimum policy.</summary>
public sealed class PasswordConfig
{
    /// <summary>PBKDF2 iterations for new hashes; older hashes are upgraded at the next sign-in.</summary>
    [ConfigField(Label = "Iterations", Min = 100000, Max = 10000000, Group = "Passwords", Order = 10, Collapsed = true)]
    public int Iterations { get; set; } = 600_000;

    /// <summary>Optional pepper (HMAC key applied before hashing). Store it outside the database.</summary>
    [ConfigField(Label = "Pepper", Description = "Secret mixed into every password hash; keep it out of the user database.",
        Secret = true, RequiresRestart = true, Group = "Passwords", Order = 11, Collapsed = true)]
    public string Pepper { get; set; } = "";

    /// <summary>Minimum password length.</summary>
    [ConfigField(Label = "Minimum Length", Min = 6, Max = 128, Group = "Passwords", Order = 12)]
    public int MinimumLength { get; set; } = 10;

    /// <summary>Maximum accepted password length (bounds hashing cost).</summary>
    [ConfigField(Label = "Maximum Length", Min = 16, Max = 4096, Group = "Passwords", Order = 13, Collapsed = true)]
    public int MaximumLength { get; set; } = 512;
}
