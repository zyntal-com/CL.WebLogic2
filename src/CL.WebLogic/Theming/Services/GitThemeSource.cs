using CL.GitHelper;
using CL.GitHelper.Models;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Hosting.Services;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// <c>Source = Git</c>: clones the theme repository through CL.GitHelper into
/// <c>{data}/themes/{RepositoryId}</c> and fast-forwards it on every sync.
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
        var git = _libraries.GetLibrary<GitHelperLibrary>()
            ?? throw new InvalidOperationException("Theme source is Git, but CL.GitHelper is not loaded. Load GitHelperLibrary before WebLogic.");

        var localPath = Path.Combine(_context.DataDirectory, "themes", config.RepositoryId);
        try
        {
            git.RegisterRepository(new RepositoryConfiguration
            {
                Id = config.RepositoryId,
                Name = "WebLogic theme",
                RepositoryUrl = config.RepositoryUrl,
                LocalPath = localPath,
                DefaultBranch = config.Branch
            });
        }
        catch (ArgumentException)
        {
            // Already registered (re-sync).
        }

        var repository = await git.GetRepositoryAsync(config.RepositoryId).ConfigureAwait(false);
        var result = await repository.EnsureUpToDateAsync(config.Branch, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            if (Directory.Exists(Path.Combine(localPath, ".git")))
            {
                _context.Logger.Warning($"Theme repository '{config.RepositoryId}' could not be updated ({result.ErrorMessage}); serving the last synchronized copy.");
                return Combine(localPath, config.ThemeSubPath);
            }
            throw new InvalidOperationException($"Theme repository '{config.RepositoryId}' could not be cloned: {result.ErrorMessage}");
        }

        return Combine(result.Value.LocalPath, config.ThemeSubPath);
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
