using System.Globalization;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Sessions;

namespace CL.WebLogic.Auth.Sessions;

/// <summary>
/// Ends signed-in sessions whose user was deleted, disabled or had their credentials changed
/// (password, two-factor, e-mail) — checked every <c>Auth.SecurityStampCheckMinutes</c>.
/// </summary>
public sealed class SecurityStampSessionValidator : IWebSessionValidator
{
    private const string CheckedKey = "__wl_stamp_at";

    private readonly IWebUserStore _users;
    private readonly WebLogicConfigHolder _config;
    private readonly TimeProvider _time;

    /// <summary>Creates the validator.</summary>
    public SecurityStampSessionValidator(IWebUserStore users, WebLogicConfigHolder config, TimeProvider? time = null)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ValidateAsync(LazyWebSession session, WebSecurityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var minutes = _config.Current.Auth.SecurityStampCheckMinutes;
        if (minutes <= 0 || session.UserId is null || session.SecurityStamp is null || _users is NullWebUserStore)
            return true;

        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        if (long.TryParse(session.GetString(CheckedKey), NumberStyles.None, CultureInfo.InvariantCulture, out var checkedAt) && now - checkedAt < minutes * 60L)
            return true;

        var user = await _users.FindByIdAsync(session.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDisabled || WebSuspension.Of(user, _time.GetUtcNow()) is not null
            || !string.Equals(user.SecurityStamp, session.SecurityStamp, StringComparison.Ordinal))
            return false;

        session.SetString(CheckedKey, now.ToString(CultureInfo.InvariantCulture));
        return true;
    }
}
