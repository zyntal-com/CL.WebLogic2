using CodeLogic;
using CodeLogic.Framework.Libraries;

namespace CL.WebLogic.Hosting.Services;

/// <summary>Resolves libraries from the running CodeLogic runtime.</summary>
public sealed class CodeLogicLibraryResolver : IWebLibraryResolver
{
    /// <inheritdoc/>
    public TLibrary? GetLibrary<TLibrary>() where TLibrary : class, ILibrary => Libraries.Get<TLibrary>();
}
