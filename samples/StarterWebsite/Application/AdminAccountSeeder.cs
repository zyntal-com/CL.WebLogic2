using System.Security.Cryptography;
using CL.WebLogic.Auth.Accounts;
using CL.WebLogic.Auth.Users;
using CL.WebLogic.Hosting.Services;
using Samples.Hosting;
using StarterWebsite.Configuration;

namespace StarterWebsite.Application;

/// <summary>
/// Creates the operator account once. Its password comes from <c>DEMO_ADMIN_PASSWORD</c>; without
/// one, a random password is generated and written to the log a single time. Restarts never touch
/// an existing account.
/// </summary>
public sealed class AdminAccountSeeder : IHostedService
{
    private readonly WebAccountService _accounts;
    private readonly IWebUserStore _users;
    private readonly StarterConfig _config;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the seeder.</summary>
    public AdminAccountSeeder(WebAccountService accounts, IWebUserStore users, StarterConfig config, IWebLogicContext context)
    {
        _accounts = accounts;
        _users = users;
        _config = config;
        _context = context;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await _users.FindByLoginAsync(_config.AdminUserName, cancellationToken).ConfigureAwait(false) is not null)
            return;

        var configured = DemoEnvironment.Text("DEMO_ADMIN_PASSWORD");
        var password = configured ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(15)).Replace('+', 'x').Replace('/', 'y');
        var created = await _accounts.RegisterAsync(new WebRegistration
        {
            UserName = _config.AdminUserName,
            Email = _config.AdminEmail,
            Password = password,
            Groups = ["admin"],
            Permissions = ["weblogic.explorer", "weblogic.internal", "starter.wall.moderate"],
            EmailConfirmed = true,
            SendVerificationEmail = false
        }, cancellationToken).ConfigureAwait(false);

        if (created.IsFailure)
            _context.Logger.Error($"Could not create the admin account: {created.Error!.Message}");
        else if (configured is null)
            _context.Logger.Warning($"Created admin account '{_config.AdminUserName}' with the one-time password: {password}  (set DEMO_ADMIN_PASSWORD to choose it; change it after signing in)");
        else
            _context.Logger.Info($"Created admin account '{_config.AdminUserName}' from DEMO_ADMIN_PASSWORD");
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
