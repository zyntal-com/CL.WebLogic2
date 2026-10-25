using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CL.WebLogic.Forms.Events;

namespace CL.WebLogic.IntegrationTests.Forms;

public sealed class FormSubmissionTests
{
    private static async Task<HttpResponseMessage> ClientPostAsync(HttpClient client, string path, HttpContent content) =>
        await client.SendAsync(FormsTestSite.AsClient(new HttpRequestMessage(HttpMethod.Post, path) { Content = content }));

    [Fact]
    public async Task Template_directive_renders_the_registered_form()
    {
        await using var host = await FormsTestSite.StartAsync();

        var html = await host.Client.GetStringAsync("/contact");

        Assert.Contains("<form id=\"wl-form-contact\"", html);
        Assert.Contains("action=\"/contact\"", html);
        Assert.Contains(">Send it</button>", html);
        Assert.Contains("<option value=\"se\">Sweden</option>", html);
        Assert.Contains("__wl_csrf", FormsTestSite.HiddenFields(html).Keys);
        Assert.Contains("__wl_ts", FormsTestSite.HiddenFields(html).Keys);
        Assert.Contains("\"remote\":true", html);
    }

    [Fact]
    public async Task Prefill_and_request_render_api()
    {
        await using var host = await FormsTestSite.StartAsync();

        Assert.Contains("value=\"grace@example.com\"", await host.Client.GetStringAsync("/contact/edit"));
        Assert.Contains("data-wl-form=\"contact\"", await host.Client.GetStringAsync("/contact/html"));
    }

    [Fact]
    public async Task Valid_client_post_returns_the_json_form_result()
    {
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/contact");

        var response = await ClientPostAsync(host.Client, "/contact", FormsTestSite.Post(page, ("Name", "Ada"), ("Email", "ada@example.com"), ("Country", "dk"), ("IsAdmin", "true")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("ok").GetBoolean());
        Assert.Equal("Thanks Ada", json.GetProperty("message").GetString());
        Assert.Equal("/thanks", json.GetProperty("redirect").GetString());
        Assert.Equal("dk", json.GetProperty("data").GetProperty("country").GetString());
        Assert.False(json.GetProperty("data").GetProperty("isAdmin").GetBoolean()); // over-posting ignored
    }

    [Fact]
    public async Task Invalid_client_post_returns_422_with_field_errors()
    {
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/contact");

        var response = await ClientPostAsync(host.Client, "/contact", FormsTestSite.Post(page, ("Name", "A"), ("Email", "nope"), ("Country", "xx")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("ok").GetBoolean());
        Assert.Equal("contact", json.GetProperty("formId").GetString());
        var errors = json.GetProperty("errors");
        Assert.Equal("Name must be at least 2 characters.", errors.GetProperty("Name")[0].GetString());
        Assert.Equal("Email must be a valid e-mail address.", errors.GetProperty("Email")[0].GetString());
    }

    [Fact]
    public async Task Provider_choices_and_form_validators_run_after_field_rules()
    {
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/contact");

        var badChoice = await ClientPostAsync(host.Client, "/contact", FormsTestSite.Post(page, ("Name", "Ada"), ("Email", "a@example.com"), ("Country", "xx")));
        var choiceErrors = (await badChoice.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Country contains a choice that is not available.", choiceErrors.GetProperty("Country")[0].GetString());

        var taken = await ClientPostAsync(host.Client, "/contact", FormsTestSite.Post(page, ("Name", "taken"), ("Email", "a@example.com")));
        var takenErrors = (await taken.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("That name is taken.", takenErrors.GetProperty("Name")[0].GetString());
    }

    [Fact]
    public async Task Plain_posts_redirect_or_rerender_the_page_with_errors()
    {
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/contact");

        var ok = await host.Client.PostAsync("/contact", FormsTestSite.Post(page, ("Name", "Ada"), ("Email", "ada@example.com")));
        Assert.Equal(HttpStatusCode.SeeOther, ok.StatusCode);
        Assert.Equal("/thanks", ok.Headers.Location!.OriginalString);

        var invalid = await host.Client.PostAsync("/contact", FormsTestSite.Post(page, ("Name", "Ada"), ("Email", "bad<b>")));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var html = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("<h1>Contact</h1>", html);
        Assert.Contains("Email must be a valid e-mail address.", html);
        Assert.Contains("value=\"Ada\"", html);
        Assert.Contains("value=\"bad&lt;b&gt;\"", html);
        Assert.Contains("aria-invalid=\"true\"", html);
    }

    [Fact]
    public async Task Posts_without_csrf_token_are_refused()
    {
        await using var host = await FormsTestSite.StartAsync();
        await host.Client.GetStringAsync("/contact");

        var response = await ClientPostAsync(host.Client, "/contact", new FormUrlEncodedContent([new("Name", "Ada"), new("Email", "a@b.co")]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Honeypot_and_timing_reject_bots()
    {
        await using var host = await FormsTestSite.StartAsync(c => c.Forms.MinSubmitSeconds = 30);
        var rejected = new List<FormBotRejectedEvent>();
        host.Context.Events.Subscribe<FormBotRejectedEvent>(e => rejected.Add(e));
        var page = await host.Client.GetStringAsync("/contact");

        var fast = await ClientPostAsync(host.Client, "/contact", FormsTestSite.Post(page, ("Name", "Ada"), ("Email", "ada@example.com")));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, fast.StatusCode);
        Assert.Contains("could not be submitted", (await fast.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("summary").GetString());

        var trap = await ClientPostAsync(host.Client, "/contact", FormsTestSite.Post(page, ("Name", "Ada"), ("Email", "ada@example.com"), ("__wl_hp", "spam")));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, trap.StatusCode);
        Assert.Equal(["too-fast", "honeypot"], rejected.Select(e => e.Reason));
    }

    [Fact]
    public async Task Wizard_carries_validated_steps_in_a_protected_token()
    {
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/join");
        Assert.Contains("name=\"Email\"", page);

        var invalid = await ClientPostAsync(host.Client, "/join", FormsTestSite.Post(page, ("Email", "nope")));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);

        var first = await ClientPostAsync(host.Client, "/join", FormsTestSite.Post(page, ("Email", "ada@example.com")));
        var step = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("profile", step.GetProperty("step").GetString());
        var command = step.GetProperty("commands")[0];
        Assert.Equal("replace", command.GetProperty("type").GetString());
        Assert.Equal("#wl-form-join", command.GetProperty("selector").GetString());
        var stepHtml = command.GetProperty("html").GetString()!;
        Assert.Contains("name=\"DisplayName\"", stepHtml);
        Assert.Contains("__wl_wizard", FormsTestSite.HiddenFields(stepHtml).Keys);

        // A tampered earlier-step value is ignored: the protected state wins.
        var done = await ClientPostAsync(host.Client, "/join", FormsTestSite.Post(stepHtml, ("DisplayName", "Ada"), ("Email", "evil@example.com")));
        var result = await done.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("ok").GetBoolean());
        Assert.Equal("ada@example.com", result.GetProperty("data").GetProperty("email").GetString());
        Assert.Equal("Ada", result.GetProperty("data").GetProperty("displayName").GetString());

        var back = await ClientPostAsync(host.Client, "/join", FormsTestSite.Post(stepHtml, ("__wl_wizard_back", "1")));
        Assert.Contains("name=\"Email\"", (await back.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("commands")[0].GetProperty("html").GetString());
    }

    [Fact]
    public async Task Wizard_without_script_rerenders_the_page_at_the_next_step()
    {
        await using var host = await FormsTestSite.StartAsync();
        var page = await host.Client.GetStringAsync("/join");

        var response = await host.Client.PostAsync("/join", FormsTestSite.Post(page, ("Email", "ada@example.com")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<html>", html);
        Assert.Contains("name=\"DisplayName\"", html);
    }
}
