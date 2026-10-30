using CL.WebLogic.Theming.Services;

namespace CL.WebLogic.Tests.Theming;

public sealed class ThemeLinksTests
{
    [Fact]
    public void Regression_A_linked_folder_anywhere_below_the_root_is_refused()
    {
        // Security review: only the file and its parent folder were checked, so a link two levels up
        // (a Git theme can carry one) served files from outside the theme.
        var temp = Directory.CreateTempSubdirectory("wl-links-");
        try
        {
            var root = Directory.CreateDirectory(Path.Combine(temp.FullName, "theme"));
            var outside = Directory.CreateDirectory(Path.Combine(temp.FullName, "outside", "deep"));
            File.WriteAllText(Path.Combine(outside.FullName, "secret.json"), "{}");
            Directory.CreateDirectory(Path.Combine(root.FullName, "assets", "plain"));
            File.WriteAllText(Path.Combine(root.FullName, "assets", "plain", "site.css"), "body{}");

            Assert.False(ThemeLinks.AnyBelow(root.FullName, new FileInfo(Path.Combine(root.FullName, "assets", "plain", "site.css"))));
            Assert.True(ThemeLinks.AnyBelow(root.FullName, new FileInfo(Path.Combine(outside.FullName, "secret.json"))));

            try
            {
                Directory.CreateSymbolicLink(Path.Combine(root.FullName, "assets", "x"), Path.Combine(temp.FullName, "outside"));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return; // Windows without the symlink privilege; Linux CI runs the rest.
            }

            Assert.True(ThemeLinks.AnyBelow(root.FullName, new FileInfo(Path.Combine(root.FullName, "assets", "x", "deep", "secret.json"))));
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }
}
