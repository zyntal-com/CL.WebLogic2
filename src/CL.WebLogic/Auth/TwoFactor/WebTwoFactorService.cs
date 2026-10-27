using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CodeLogic.Core.Results;
using Microsoft.AspNetCore.DataProtection;

namespace CL.WebLogic.Auth.TwoFactor;

/// <summary>
/// TOTP enrollment, verification (each time step accepted once) and recovery codes. Secrets are
/// stored DataProtection-protected on <see cref="WebUser.TwoFactorSecret"/>.
/// </summary>
public sealed class WebTwoFactorService
{
    private const string SecretPurpose = "CL.WebLogic.Auth.TwoFactor.Secret.v1";

    private readonly IWebTotpProvider _totp;
    private readonly IWebUserStore _users;
    private readonly IWebRecoveryCodeStore _recoveryCodes;
    private readonly IWebOneTimeUseStore _oneTime;
    private readonly IWebSecurityTokenService _tokens;
    private readonly WebNotificationDispatcher _notifications;
    private readonly IDataProtector _secrets;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly TimeProvider _time;

    /// <summary>Creates the service.</summary>
    public WebTwoFactorService(IWebTotpProvider totp, IWebUserStore users, IWebRecoveryCodeStore recoveryCodes, IWebOneTimeUseStore oneTime,
        IWebSecurityTokenService tokens, WebNotificationDispatcher notifications, IDataProtectionProvider protection,
        WebLogicConfigHolder config, IWebLogicContext context, TimeProvider? time = null)
    {
        _totp = totp ?? throw new ArgumentNullException(nameof(totp));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _recoveryCodes = recoveryCodes ?? throw new ArgumentNullException(nameof(recoveryCodes));
        _oneTime = oneTime ?? throw new ArgumentNullException(nameof(oneTime));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _secrets = (protection ?? throw new ArgumentNullException(nameof(protection))).CreateProtector(SecretPurpose);
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Starts enrollment: a new secret, its QR code and a 15-minute token to confirm with.</summary>
    public WebTwoFactorEnrollment BeginEnrollment(WebUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var config = _config.Current;
        var issuer = config.Auth.TwoFactor.Issuer.Length > 0 ? config.Auth.TwoFactor.Issuer : config.SiteName;
        var account = user.Email ?? user.UserName;
        var secret = _totp.GenerateSecret();
        return new WebTwoFactorEnrollment
        {
            Secret = secret,
            ProvisioningUri = _totp.GetProvisioningUri(secret, issuer, account),
            QrCodeDataUri = _totp.GetQrCodeDataUri(secret, issuer, account),
            EnrollmentToken = _tokens.Create(WebTokenPurposes.TwoFactorEnrollment, user, TimeSpan.FromMinutes(15), _secrets.Protect(secret))
        };
    }

    /// <summary>
    /// Confirms enrollment with the first code. Turns two-factor on, changes the security stamp
    /// and returns the recovery codes (show them once).
    /// </summary>
    public async Task<Result<IReadOnlyList<string>>> ConfirmEnrollmentAsync(WebUser user, string enrollmentToken, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var token = await _tokens.ValidateAsync(WebTokenPurposes.TwoFactorEnrollment, enrollmentToken, consume: false, cancellationToken).ConfigureAwait(false);
        if (token.IsFailure || token.Value!.User.Id != user.Id || token.Value.Data is null)
            return Result<IReadOnlyList<string>>.Failure(Error.Validation("weblogic.auth.enrollment_invalid", "The enrollment expired; start again."));

        var secret = _secrets.Unprotect(token.Value.Data);
        var step = _totp.Validate(code, secret);
        if (step is null || !await UseStepAsync(user.Id, step.Value, cancellationToken).ConfigureAwait(false))
            return Result<IReadOnlyList<string>>.Failure(Error.Validation("weblogic.auth.code_invalid", "The code is not valid."));

        await _tokens.ValidateAsync(WebTokenPurposes.TwoFactorEnrollment, enrollmentToken, consume: true, cancellationToken).ConfigureAwait(false);
        var updated = token.Value.User with { TwoFactorSecret = token.Value.Data, SecurityStamp = WebUser.NewStamp() };
        await _users.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        var codes = await ReplaceRecoveryCodesAsync(updated, cancellationToken).ConfigureAwait(false);

        _context.Events.Publish(new TwoFactorChangedEvent(user.Id, true));
        _notifications.Enqueue(updated, WebSecurityNotificationKind.TwoFactorEnabled);
        return Result<IReadOnlyList<string>>.Success(codes);
    }

    /// <summary>Checks a code for a user with two-factor on; each time step is accepted only once.</summary>
    public async Task<bool> VerifyCodeAsync(WebUser user, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.TwoFactorSecret is null)
            return false;

        string secret;
        try
        {
            secret = _secrets.Unprotect(user.TwoFactorSecret);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _context.Logger.Error($"The two-factor secret of user {user.Id} cannot be decrypted (DataProtection keys lost?): {ex.Message}");
            return false;
        }

        var step = _totp.Validate(code, secret);
        return step is not null && await UseStepAsync(user.Id, step.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Replaces the recovery codes and returns the new ones (show them once).</summary>
    public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var codes = await ReplaceRecoveryCodesAsync(user, cancellationToken).ConfigureAwait(false);
        _context.Events.Publish(new RecoveryCodesRegeneratedEvent(user.Id));
        return codes;
    }

    private async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(WebUser user, CancellationToken cancellationToken)
    {
        var codes = WebRecoveryCodes.Generate(_config.Current.Auth.TwoFactor.RecoveryCodeCount);
        await _recoveryCodes.ReplaceAsync(user.Id, [.. codes.Select(c => WebRecoveryCodes.Hash(user.Id, c))], cancellationToken).ConfigureAwait(false);
        return codes;
    }

    /// <summary>Spends a recovery code; the user is notified.</summary>
    public async Task<bool> RedeemRecoveryCodeAsync(WebUser user, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (WebRecoveryCodes.Normalize(code).Length != 10
            || !await _recoveryCodes.TryConsumeAsync(user.Id, WebRecoveryCodes.Hash(user.Id, code), cancellationToken).ConfigureAwait(false))
            return false;

        var remaining = await _recoveryCodes.CountAsync(user.Id, cancellationToken).ConfigureAwait(false);
        _context.Events.Publish(new RecoveryCodeRedeemedEvent(user.Id, remaining));
        _notifications.Enqueue(user, WebSecurityNotificationKind.RecoveryCodeUsed,
            values: new Dictionary<string, string> { ["remaining"] = remaining.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        return true;
    }

    /// <summary>Unused recovery codes.</summary>
    public Task<int> RemainingRecoveryCodesAsync(WebUser user, CancellationToken cancellationToken) =>
        _recoveryCodes.CountAsync((user ?? throw new ArgumentNullException(nameof(user))).Id, cancellationToken);

    /// <summary>Turns two-factor off and deletes the recovery codes.</summary>
    public async Task DisableAsync(WebUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var updated = user with { TwoFactorSecret = null, SecurityStamp = WebUser.NewStamp() };
        await _users.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        await _recoveryCodes.ReplaceAsync(user.Id, [], cancellationToken).ConfigureAwait(false);
        _context.Events.Publish(new TwoFactorChangedEvent(user.Id, false));
        _notifications.Enqueue(updated, WebSecurityNotificationKind.TwoFactorDisabled);
    }

    private ValueTask<bool> UseStepAsync(string userId, long step, CancellationToken cancellationToken) =>
        _oneTime.TryUseAsync("totp:" + userId + ":" + step.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _time.GetUtcNow().AddMinutes(5), cancellationToken);
}
