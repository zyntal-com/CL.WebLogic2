using CodeLogic.Framework.Libraries;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Resolves no libraries — used when WebLogic runs without the CodeLogic runtime (tests).</summary>
public sealed class EmptyLibraryResolver : IWebLibraryResolver
{
    /// <inheritdoc/>
    public TLibrary? GetLibrary<TLibrary>() where TLibrary : class, ILibrary => null;
}
