using CL.GitHelper.Models;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Theming.Services;

namespace CL.WebLogic.Tests.Theming;

public sealed class GitThemeCredentialsTests
{
    private static RepositoryConfiguration Repo() => new() { Id = "t", Name = "t", RepositoryUrl = "https://example.test/t.git", LocalPath = "t" };

    [Fact]
    public void A_token_from_the_environment_becomes_the_https_password()
    {
        var variable = "WL_TEST_GIT_TOKEN_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, " ghp_secret ");
        try
        {
            var repo = GitThemeSource.Configure(Repo(), new ThemeConfig { GitTokenEnvironmentVariable = variable, GitToken = "ignored" });
            Assert.Equal("x-access-token", repo.Username);
            Assert.Equal("ghp_secret", repo.Password);

            Assert.Equal("oauth2", GitThemeSource.Configure(Repo(), new ThemeConfig { GitTokenEnvironmentVariable = variable, GitUserName = "oauth2" }).Username);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void A_missing_variable_fails_without_revealing_anything()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GitThemeSource.Configure(Repo(), new ThemeConfig { GitTokenEnvironmentVariable = "WL_TEST_NOT_SET_" + Guid.NewGuid().ToString("N") }));
        Assert.Contains("not set", ex.Message);
    }

    [Fact]
    public void Deploy_keys_and_no_credentials()
    {
        var ssh = GitThemeSource.Configure(Repo(), new ThemeConfig { GitSshKeyPath = "/run/secrets/theme_key" });
        Assert.Equal("/run/secrets/theme_key", ssh.SshKeyPath);
        Assert.Null(ssh.Password);

        var none = GitThemeSource.Configure(Repo(), new ThemeConfig());
        Assert.Null(none.Username);
        Assert.Null(none.Password);
        Assert.Null(none.SshKeyPath);
    }
}
