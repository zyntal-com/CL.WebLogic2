using CL.WebLogic.Auth.Events;
using CL.WebLogic.Auth.Notifications;
using CL.WebLogic.Auth.Passkeys;
using CL.WebLogic.Auth.Passwords;
using CL.WebLogic.Auth.Tokens;
using CL.WebLogic.Auth.TwoFactor;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Security.Sessions;
using CodeLogic.Core.Results;

namespace CL.WebLogic.Auth.Accounts;

/// <summary>
/// Account lifecycle: registration, e-mail verification and change, password reset and change,
/// magic links, deletion. E-mails go through the background notification queue. Links are built from
/// <c>PublicBaseUrl</c> — never from the request's Host header.
/// </summary>
public sealed partial class WebAccountService
{
    private readonly IWebUserStore _users;
    private readonly IWebPasswordHasher _hasher;
    private readonly IWebPasswordPolicy _policy;
    private readonly IWebSecurityTokenService _tokens;
    private readonly WebNotificationDispatcher _notifications;
    private readonly IWebSessionStore _sessions;
    private readonly WebLogicConfigHolder _config;
    private readonly IWebLogicContext _context;
    private readonly IWebPasskeyStore _passkeys;
    private readonly IWebRecoveryCodeStore _recoveryCodes;

    /// <summary>Creates the service.</summary>
    public WebAccountService(IWebUserStore users, IWebPasswordHasher hasher, IWebPasswordPolicy policy, IWebSecurityTokenService tokens,
        WebNotificationDispatcher notifications, IWebSessionStore sessions, WebLogicConfigHolder config, IWebLogicContext context,
        IWebPasskeyStore passkeys, IWebRecoveryCodeStore recoveryCodes)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passkeys = passkeys ?? throw new ArgumentNullException(nameof(passkeys));
        _recoveryCodes = recoveryCodes ?? throw new ArgumentNullException(nameof(recoveryCodes));
    }

    /// <summary>Creates an account (and sends the verification e-mail).</summary>
    public async Task<Result<WebUser>> RegisterAsync(WebRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var userName = registration.UserName?.Trim();
        if (!WebAccountValidation.IsValidUserName(userName))
            return Fail<WebUser>("weblogic.auth.invalid_user_name", "Choose a user name of 1 to 64 characters.");
        var email = registration.Email?.Trim();
        if (email is not null && !WebAccountValidation.IsValidEmail(email))
            return Fail<WebUser>("weblogic.auth.invalid_email", "Enter a valid e-mail address.");

        var user = new WebUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = userName!,
            Email = email,
            EmailConfirmed = registration.EmailConfirmed && email is not null,
            Groups = registration.Groups,
            Permissions = registration.Permissions,
            TenantId = registration.TenantId
        };
        if (registration.Password is not null)
        {
            var policy = _policy.Validate(registration.Password, user);
            if (policy.IsFailure)
                return Result<WebUser>.Failure(policy.Error!);
            user = user with { PasswordHash = _hasher.Hash(registration.Password) };
        }

        var created = await _users.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
            return Result<WebUser>.Failure(created.Error!);

        _context.Events.Publish(new UserRegisteredEvent(user.Id));
        if (registration.SendVerificationEmail && email is not null && !user.EmailConfirmed)
            SendEmailVerification(user);
        return Result<WebUser>.Success(user);
    }

    /// <summary>Queues the verification e-mail.</summary>
    public void SendEmailVerification(WebUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var tokens = _config.Current.Auth.Tokens;
        var token = _tokens.Create(WebTokenPurposes.EmailVerification, user, TimeSpan.FromHours(tokens.EmailVerificationHours));
        _notifications.Enqueue(user, WebSecurityNotificationKind.EmailVerification, BuildLink(tokens.EmailVerificationPath, token));
    }

    /// <summary>Confirms the e-mail address from a verification link.</summary>
    public async Task<Result<WebUser>> ConfirmEmailAsync(string token, CancellationToken cancellationToken)
    {
        var validated = await _tokens.ValidateAsync(WebTokenPurposes.EmailVerification, token, consume: true, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
            return Result<WebUser>.Failure(validated.Error!);
        var user = validated.Value!.User with { EmailConfirmed = true };
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        _context.Events.Publish(new EmailConfirmedEvent(user.Id));
        return Result<WebUser>.Success(user);
    }

    /// <summary>Asks for a new e-mail address: a confirmation link goes to the new address.</summary>
    public async Task<Result> RequestEmailChangeAsync(WebUser user, string newEmail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        newEmail = newEmail?.Trim() ?? string.Empty;
        if (!WebAccountValidation.IsValidEmail(newEmail))
            return Result.Failure(Error.Validation("weblogic.auth.invalid_email", "Enter a valid e-mail address."));
        if (await FindByEmailAsync(newEmail, cancellationToken).ConfigureAwait(false) is { } existing && existing.Id != user.Id)
            return Result.Success(); // do not reveal that the address is taken; the confirmation will fail

        var tokens = _config.Current.Auth.Tokens;
        var token = _tokens.Create(WebTokenPurposes.EmailChange, user, TimeSpan.FromHours(tokens.EmailChangeHours), newEmail);
        _notifications.Enqueue(user, WebSecurityNotificationKind.EmailChangeConfirmation, BuildLink(tokens.EmailChangePath, token), toAddress: newEmail);
        return Result.Success();
    }

    /// <summary>Applies an e-mail change from its confirmation link; the old address is told.</summary>
    public async Task<Result<WebUser>> ConfirmEmailChangeAsync(string token, CancellationToken cancellationToken)
    {
        var validated = await _tokens.ValidateAsync(WebTokenPurposes.EmailChange, token, consume: true, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
            return Result<WebUser>.Failure(validated.Error!);
        var newEmail = validated.Value!.Data!;
        var old = validated.Value.User;
        if (await FindByEmailAsync(newEmail, cancellationToken).ConfigureAwait(false) is { } existing && existing.Id != old.Id)
            return Fail<WebUser>("weblogic.auth.email_taken", "This e-mail address is already in use.");

        var user = old with { Email = newEmail, EmailConfirmed = true, SecurityStamp = WebUser.NewStamp() };
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        var changed = !string.Equals(old.Email, newEmail, StringComparison.OrdinalIgnoreCase);
        if (old.Email is not null && changed)
            _notifications.Enqueue(old, WebSecurityNotificationKind.EmailChanged);
        _context.Events.Publish(new EmailConfirmedEvent(user.Id));
        if (changed)
            _context.Events.Publish(new EmailChangedEvent(user.Id));
        return Result<WebUser>.Success(user);
    }

    /// <summary>Queues a sign-in link. Behaves the same whether or not the address exists.</summary>
    public async Task RequestMagicLinkAsync(string email, CancellationToken cancellationToken)
    {
        var user = await FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDisabled)
            return;
        var tokens = _config.Current.Auth.Tokens;
        var token = _tokens.Create(WebTokenPurposes.MagicLink, user, TimeSpan.FromMinutes(tokens.MagicLinkMinutes));
        _notifications.Enqueue(user, WebSecurityNotificationKind.MagicLink, BuildLink(tokens.MagicLinkPath, token));
    }

    /// <summary>An absolute link from <c>PublicBaseUrl</c> (relative, with a warning, when it is not set).</summary>
    public string BuildLink(string path, string token)
    {
        var relative = path + (path.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "token=" + Uri.EscapeDataString(token);
        var baseUrl = _config.Current.PublicBaseUrl.TrimEnd('/');
        if (baseUrl.Length > 0)
            return baseUrl + relative;
        _context.Logger.Warning("PublicBaseUrl is not set; e-mailed account links are relative. Set PublicBaseUrl.");
        return relative;
    }

    private async Task<WebUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        email = email?.Trim() ?? string.Empty;
        if (!WebAccountValidation.IsValidEmail(email))
            return null;
        return _users is IWebUserEmailStore byEmail
            ? await byEmail.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false)
            : await _users.FindByLoginAsync(email, cancellationToken).ConfigureAwait(false) is { } user
              && string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase) ? user : null;
    }

    private static Result<T> Fail<T>(string code, string message) => Result<T>.Failure(Error.Validation(code, message));
}
