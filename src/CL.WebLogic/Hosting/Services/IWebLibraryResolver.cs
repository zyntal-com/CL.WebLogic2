using CodeLogic.Framework.Libraries;

namespace CL.WebLogic.Hosting.Services;

/// <summary>
/// Finds other loaded CodeLogic libraries (CL.GitHelper, CL.Storage, CL.Mail…). Injected instead
/// of calling <c>Libraries.Get</c> directly, so features that use them can be tested.
/// </summary>
public interface IWebLibraryResolver
{
    /// <summary>The loaded library of type <typeparamref name="TLibrary"/>, or null when it is not loaded.</summary>
    TLibrary? GetLibrary<TLibrary>() where TLibrary : class, ILibrary;
}
