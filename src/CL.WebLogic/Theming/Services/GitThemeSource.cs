using CL.GitHelper;
using CL.GitHelper.Errors;
using CL.GitHelper.Models;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// <c>Source = Git</c>: clones the theme repository through CL.GitHelper into
/// <c>{data}/themes/{RepositoryId}</c> and fast-forwards it on every sync. Credentials come from the
/// <c>Theme.Git…</c> fields (token or deploy key; secrets read from environment variables) or, when
/// those are empty, from a repository already configured in CL.GitHelper.
/// </summary>
public sealed class GitThemeSource : IWebThemeSource
{
    private readonly IWebLibraryResolver _libraries;
    private readonly IWebLogicContext _context;

    /// <summary>Creates the source.</summary>
    public GitThemeSource(IWebLibraryResolver libraries, IWebLogicContext context)
    {
        _libraries = libraries ?? throw new ArgumentNullException(nameof(libraries));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public ThemeSourceKind Kind => ThemeSourceKind.Git;

    /// <inheritdoc/>
    public async Task<string> SyncAsync(ThemeConfig config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var git = _libraries.GetLibrary<GitHelperLibrary>()
            ?? throw new InvalidOperationException("Theme source is Git, but CL.GitHelper is not loaded. Load GitHelperLibrary before WebLogic.");

        var localPath = Path.Combine(_context.DataDirectory, "themes", config.RepositoryId);
        var registration = git.RegisterRepository(Configure(new RepositoryConfiguration
            {
                Id = config.RepositoryId,
                Name = "WebLogic theme",
                RepositoryUrl = config.RepositoryUrl,
                LocalPath = localPath,
                DefaultBranch = config.Branch
            }, config));
        // An existing registration is expected on re-sync; other failures must stop startup.
        if (registration.IsFailure && registration.Error?.Code != GitErrors.RepositoryExistsCode)
            throw new InvalidOperationException($"Theme repository '{config.RepositoryId}' could not be registered: {registration.Error?.Message}");

        var lookup = await git.GetRepositoryAsync(config.RepositoryId).ConfigureAwait(false);
        if (lookup.IsFailure || lookup.Value is not { } repository)
            throw new InvalidOperationException($"Theme repository '{config.RepositoryId}' could not be opened: {lookup.Error?.Message}");
        var result = await repository.EnsureUpToDateAsync(config.Branch, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.IsFailure || result.Value is not { } info)
        {
            var reason = result.Error?.Message ?? "no repository information";
            if (Directory.Exists(Path.Combine(localPath, ".git")))
            {
                _context.Logger.Warning($"Theme repository '{config.RepositoryId}' could not be updated ({reason}); serving the last synchronized copy.");
                return Combine(localPath, config.ThemeSubPath);
            }
            throw new InvalidOperationException($"Theme repository '{config.RepositoryId}' could not be cloned: {reason}");
        }

        return Combine(info.LocalPath, config.ThemeSubPath);
    }

    /// <summary>Applies the credentials from <paramref name="config"/>; never logs them.</summary>
    internal static RepositoryConfiguration Configure(RepositoryConfiguration repository, ThemeConfig config)
    {
        var token = !string.IsNullOrWhiteSpace(config.GitTokenEnvironmentVariable)
            ? Environment.GetEnvironmentVariable(config.GitTokenEnvironmentVariable.Trim()) is { Length: > 0 } value
                ? value.Trim()
                : throw new InvalidOperationException($"Theme.GitTokenEnvironmentVariable names '{config.GitTokenEnvironmentVariable}', but that environment variable is not set.")
            : config.GitToken.Trim();
        if (token.Length > 0)
        {
            repository.Username = string.IsNullOrWhiteSpace(config.GitUserName) ? "x-access-token" : config.GitUserName.Trim();
            repository.Password = token;
        }
        else if (!string.IsNullOrWhiteSpace(config.GitUserName))
        {
            repository.Username = config.GitUserName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(config.GitSshKeyPath))
        {
            repository.SshKeyPath = config.GitSshKeyPath.Trim();
            if (!string.IsNullOrWhiteSpace(config.GitSshPassphraseEnvironmentVariable))
                repository.SshPassphrase = Environment.GetEnvironmentVariable(config.GitSshPassphraseEnvironmentVariable.Trim())
                    ?? throw new InvalidOperationException($"Theme.GitSshPassphraseEnvironmentVariable names '{config.GitSshPassphraseEnvironmentVariable}', but that environment variable is not set.");
        }
        return repository;
    }

    private static string Combine(string root, string subPath)
    {
        var full = Path.GetFullPath(string.IsNullOrWhiteSpace(subPath) ? root : Path.Combine(root, subPath));
        var rootFull = Path.GetFullPath(root);
        return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            ? full
            : throw new InvalidOperationException("Theme sub-path must stay inside the repository.");
    }
}
