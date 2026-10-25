using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.IntegrationTests.Security;

namespace CL.WebLogic.IntegrationTests.Forms;

public sealed class FormEndpointTests
{
    [Fact]
    public async Task Remote_validation_checks_one_field_with_the_form_validators()
    {
        await using var host = await FormsTestSite.StartAsync();
        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);

        async Task<JsonElement> ValidateAsync(string field, params (string, string)[] values)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/_weblogic/forms/contact/validate/{field}")
            {
                Content = new FormUrlEncodedContent(values.Select(v => new KeyValuePair<string, string>(v.Item1, v.Item2)))
            };
            request.Headers.Add("X-CSRF-Token", token);
            var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        var taken = await ValidateAsync("Name", ("Name", "taken"), ("Email", "blocked@example.com"));
        Assert.False(taken.GetProperty("valid").GetBoolean());
        Assert.Equal(["That name is taken."], taken.GetProperty("errors").EnumerateArray().Select(e => e.GetString()));

        var fine = await ValidateAsync("Name", ("Name", "Ada"));
        Assert.True(fine.GetProperty("valid").GetBoolean());

        var short_ = await ValidateAsync("Name", ("Name", "A"));
        Assert.Equal("Name must be at least 2 characters.", short_.GetProperty("errors")[0].GetString());
    }

    [Fact]
    public async Task Remote_validation_requires_csrf_and_a_known_form()
    {
        await using var host = await FormsTestSite.StartAsync();

        var noToken = await host.Client.PostAsync("/_weblogic/forms/contact/validate/Name", new FormUrlEncodedContent([new("Name", "x")]));
        Assert.Equal(HttpStatusCode.Forbidden, noToken.StatusCode);

        var token = await SecurityTestSite.GetCsrfTokenAsync(host.Client);
        var request = new HttpRequestMessage(HttpMethod.Post, "/_weblogic/forms/nope/validate/Name") { Content = new FormUrlEncodedContent([]) };
        request.Headers.Add("X-CSRF-Token", token);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.SendAsync(request)).StatusCode);

        // Regression (security review): only [WebField(Remote = true)] fields answer, so the form
        // validators behind other fields (e.g. "e-mail blocked") cannot be probed anonymously.
        var notRemote = new HttpRequestMessage(HttpMethod.Post, "/_weblogic/forms/contact/validate/Email")
        {
            Content = new FormUrlEncodedContent([new("Email", "blocked@example.com")])
        };
        notRemote.Headers.Add("X-CSRF-Token", token);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.SendAsync(notRemote)).StatusCode);
    }

    [Fact]
    public async Task Options_endpoint_lists_and_searches()
    {
        await using var host = await FormsTestSite.StartAsync();

        var all = await host.Client.GetFromJsonAsync<JsonElement>("/_weblogic/forms/options/countries");
        Assert.Equal(3, all.GetProperty("options").GetArrayLength());

        var search = await host.Client.GetFromJsonAsync<JsonElement>("/_weblogic/forms/options/countries?q=swe");
        var option = Assert.Single(search.GetProperty("options").EnumerateArray());
        Assert.Equal("se", option.GetProperty("value").GetString());
        Assert.Equal("Sweden", option.GetProperty("label").GetString());

        var tooLong = await host.Client.GetFromJsonAsync<JsonElement>("/_weblogic/forms/options/countries?q=" + new string('a', 101));
        Assert.Equal(0, tooLong.GetProperty("options").GetArrayLength());
    }

    [Fact]
    public async Task Options_endpoint_enforces_provider_access()
    {
        await using var host = await FormsTestSite.StartAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/_weblogic/forms/options/nope")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/_weblogic/forms/options/staff")).StatusCode);

        await host.Client.GetAsync("/signin?user=u1");
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/_weblogic/forms/options/staff")).StatusCode);
    }
}
