using System.Net;
using System.Text.RegularExpressions;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Security;

public sealed partial class TemplateSecurityTests
{
    [GeneratedRegex("name=\"__wl_csrf\" value=\"([A-Za-z0-9_-]+)\"")]
    private static partial Regex CsrfField();

    private static async Task<WebLogicTestHost> StartAsync()
    {
        var host = await SecurityTestSite.StartAsync(web: w => w.AddContributor(new DelegateContributor(b =>
            b.MapPage("/form", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/form"))).AllowAnonymous()),
            new WebContributorDescriptor { Id = "tests.form", Name = "Form" }));
        host.WriteContentFile("theme/templates/form.html",
            "<form method=\"post\" action=\"/submit\">{csrf}</form>{if:permission:posts.edit}CAN-EDIT{/if}{if:group:editor}EDITOR{/if}");
        return host;
    }

    [Fact]
    public async Task CsrfField_InTemplate_ValidatesOnPost()
    {
        await using var host = await StartAsync();

        var html = await host.Client.GetStringAsync("/form");
        var token = CsrfField().Match(html).Groups[1].Value;

        var response = await host.Client.PostAsync("/submit", new FormUrlEncodedContent([new("__wl_csrf", token)]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PermissionAndGroupConditions_UseTheSignedInIdentity()
    {
        await using var host = await StartAsync();
        Assert.DoesNotContain("CAN-EDIT", await host.Client.GetStringAsync("/form"));

        await host.Client.GetAsync("/signin?user=alice&groups=editor&perms=posts.*");
        var html = await host.Client.GetStringAsync("/form");

        Assert.Contains("CAN-EDIT", html);
        Assert.Contains("EDITOR", html);
    }
}
