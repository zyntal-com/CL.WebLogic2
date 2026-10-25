namespace CL.WebLogic.Theming.Services;

/// <summary>Resolves the absolute folder the active theme lives in.</summary>
public interface IWebThemeRoot
{
    /// <summary>Absolute path of the theme root folder.</summary>
    string RootPath { get; }
}
