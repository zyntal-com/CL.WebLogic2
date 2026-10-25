using System.Collections.Concurrent;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Configuration.Services;
using CL.WebLogic.Templates.Parsing;
using CL.WebLogic.Templates.Rendering;

namespace CL.WebLogic.Theming.Services;

/// <summary>
/// Loads templates from the active theme folder and caches them compiled. A cached entry is
/// re-validated at most every <c>Theme.TemplateChangeCheckSeconds</c>: the file's timestamp and
/// length are checked first, then its source hash, so touching a file never forces a recompile.
/// Paths are contained to the theme root; links are refused. Templates the theme does not have
/// fall back to those contributed by plugins (<see cref="WebContributedTemplates"/>).
/// </summary>
public sealed class ThemeTemplateLoader : ITemplateLoader
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (bool Exists, DateTimeOffset CheckedAt)> _existence = new(StringComparer.OrdinalIgnoreCase);
    private readonly IWebThemeRoot _root;
    private readonly WebLogicConfigHolder _config;
    private readonly TemplateInterpreter _interpreter;
    private readonly ClosureTemplateCompiler _closure;
    private readonly TimeProvider _time;
    private readonly WebContributedTemplates? _contributed;

    /// <summary>Creates the loader.</summary>
    public ThemeTemplateLoader(IWebThemeRoot root, WebLogicConfigHolder config, TemplateInterpreter interpreter, ClosureTemplateCompiler closure,
        TimeProvider? time = null, WebContributedTemplates? contributed = null)
    {
        _contributed = contributed;
        if (contributed is not null)
            contributed.Changed += path => _cache.TryRemove(path, out _);
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
        _closure = closure ?? throw new ArgumentNullException(nameof(closure));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Answers are reused like compiled templates (<c>Theme.TemplateChangeCheckSeconds</c>): form
    /// fields and error pages ask for optional override templates on every render, and each
    /// file-system check costs more than rendering the field.
    /// </remarks>
    public bool Exists(string normalizedPath)
    {
        if (_contributed?.TryGet(normalizedPath, out _, out _) ?? false)
            return true;
        var now = _time.GetUtcNow();
        var checkSeconds = _config.Current.Theme.TemplateChangeCheckSeconds;
        if (_cache.TryGetValue(normalizedPath, out var cached) && cached.Length >= 0
            && (checkSeconds == 0 || now - cached.CheckedAt < TimeSpan.FromSeconds(checkSeconds)))
            return true;
        if (_existence.TryGetValue(normalizedPath, out var known)
            && (checkSeconds == 0 || now - known.CheckedAt < TimeSpan.FromSeconds(checkSeconds)))
            return known.Exists;

        var exists = TryResolve(normalizedPath, out _);
        if (_existence.Count >= 4096)
            _existence.Clear();
        _existence[normalizedPath] = (exists, now);
        return exists;
    }

    /// <inheritdoc/>
    public async ValueTask<CompiledTemplate> LoadAsync(string normalizedPath, CancellationToken cancellationToken)
    {
        var compiler = Compiler();
        var now = _time.GetUtcNow();
        var checkSeconds = _config.Current.Theme.TemplateChangeCheckSeconds;

        if (_cache.TryGetValue(normalizedPath, out var cached) && cached.Template.Engine == compiler.Name
            && (checkSeconds == 0 || now - cached.CheckedAt < TimeSpan.FromSeconds(checkSeconds)))
            return cached.Template;

        if (!TryResolve(normalizedPath, out var file))
        {
            if (_contributed is not null && _contributed.TryGet(normalizedPath, out var contributedSource, out var contributedHash))
            {
                var contributed = cached is not null && cached.Template.SourceHash == contributedHash && cached.Template.Engine == compiler.Name
                    ? cached.Template
                    : compiler.Compile(normalizedPath, contributedHash, TemplateParser.Parse(contributedSource, normalizedPath));
                _cache[normalizedPath] = new Entry(contributed, DateTime.MinValue, -1, now);
                return contributed;
            }
            _cache.TryRemove(normalizedPath, out _);
            throw new TemplateRenderException("Template not found.", normalizedPath);
        }

        if (cached is not null && cached.Template.Engine == compiler.Name
            && cached.LastWriteUtc == file.LastWriteTimeUtc && cached.Length == file.Length)
        {
            _cache[normalizedPath] = cached with { CheckedAt = now };
            return cached.Template;
        }

        var source = await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false);
        var hash = TemplateSourceHash.Compute(source);
        var template = cached is not null && cached.Template.SourceHash == hash && cached.Template.Engine == compiler.Name
            ? cached.Template
            : compiler.Compile(normalizedPath, hash, TemplateParser.Parse(source, normalizedPath));

        _cache[normalizedPath] = new Entry(template, file.LastWriteTimeUtc, file.Length, now);
        return template;
    }

    /// <summary>Drops one cached template, or all when <paramref name="normalizedPath"/> is null.</summary>
    public void Invalidate(string? normalizedPath = null)
    {
        if (normalizedPath is null)
        {
            _cache.Clear();
            _existence.Clear();
        }
        else
        {
            _cache.TryRemove(normalizedPath, out _);
            _existence.TryRemove(normalizedPath, out _);
        }
    }

    private ITemplateCompiler Compiler() =>
        _config.Current.Theme.TemplateEngine == TemplateEngineKind.Interpreter ? _interpreter : _closure;

    private bool TryResolve(string normalizedPath, out FileInfo file)
    {
        file = null!;
        var root = Path.GetFullPath(_root.RootPath);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(rootWithSeparator, PathComparison))
            return false;

        var candidate = new FileInfo(full);
        if (!candidate.Exists || candidate.LinkTarget is not null)
            return false;

        file = candidate;
        return true;
    }

    private sealed record Entry(CompiledTemplate Template, DateTime LastWriteUtc, long Length, DateTimeOffset CheckedAt);
}
