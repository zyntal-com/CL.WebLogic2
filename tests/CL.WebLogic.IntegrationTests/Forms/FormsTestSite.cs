using System.Net;
using System.Text.RegularExpressions;
using CL.WebLogic.Configuration.Models;
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Forms.Providers;
using CL.WebLogic.Hosting.Services;
using CL.WebLogic.Results;
using CL.WebLogic.Routing.Models;
using CL.WebLogic.Routing.Services;
using CL.WebLogic.IntegrationTests.Security;
using CL.WebLogic.Testing.Services;

namespace CL.WebLogic.IntegrationTests.Forms;

/// <summary>A host with form pages and handlers.</summary>
internal static partial class FormsTestSite
{
    public static async Task<WebLogicTestHost> StartAsync(Action<WebLogicConfig>? configure = null)
    {
        var host = await SecurityTestSite.StartAsync(config =>
        {
            config.Forms.MinSubmitSeconds = 0;
            configure?.Invoke(config);
        }, web =>
        {
            web.AddForm<ContactForm>()
               .AddForm<JoinForm>()
               .AddForm<AvatarForm>()
               .AddFormValidator<ContactForm, TakenNameValidator>()
               .AddFormOptions("countries", [new WebFieldOption("dk", "Denmark"), new WebFieldOption("se", "Sweden"), new WebFieldOption("no", "Norway")])
               .AddFormOptions("staff", [new WebFieldOption("u1", "Ada")], allowAnonymous: false)
               .AddContributor(new DelegateContributor(Routes), new WebContributorDescriptor { Id = "tests.forms", Name = "Forms" });
        });
        host.WriteContentFile("theme/layouts/main.html", "<!doctype html><html><head>{renderhead}</head><body>{renderbody}</body></html>");
        host.WriteContentFile("theme/templates/contact.html", "{layout:layouts/main}<h1>Contact</h1>{form:contact submit=\"Send it\"}");
        host.WriteContentFile("theme/templates/join.html", "{layout:layouts/main}{form:join}");
        return host;
    }

    private static void Routes(WebContributionBuilder b)
    {
        b.MapPage("/contact", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/contact"))).AllowAnonymous();
        b.MapPage("/contact/edit", (r, _) =>
        {
            r.Forms.Prefill(new ContactForm { Name = "Grace", Email = "grace@example.com" });
            return ValueTask.FromResult<WebResult>(WebResult.Page("templates/contact"));
        }).AllowAnonymous();
        b.MapPage("/contact/html", async (r, ct) => WebResult.Html((await r.Forms.RenderAsync<ContactForm>(cancellationToken: ct)).Html)).AllowAnonymous();
        b.MapPage("/contact", async (r, ct) =>
        {
            var submission = await r.Forms.BindAsync<ContactForm>(ct);
            if (!submission.IsValid)
                return submission.Invalid(fallbackTemplate: "templates/contact");
            return FormResult.Success($"Thanks {submission.Model.Name}", "/thanks", new { submission.Model.Name, submission.Model.Country, submission.Model.IsAdmin });
        }, "POST").AllowAnonymous();

        b.MapPage("/join", (_, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/join"))).AllowAnonymous();
        b.MapPage("/join", async (r, ct) =>
        {
            var submission = await r.Forms.BindAsync<JoinForm>(ct);
            if (!submission.IsComplete || !submission.IsValid)
                return submission.Continue("templates/join");
            return FormResult.Success(data: new { submission.Model.Email, submission.Model.DisplayName });
        }, "POST").AllowAnonymous();

        b.MapPage("/avatar", async (r, ct) => WebResult.Html((await r.Forms.RenderAsync<AvatarForm>(cancellationToken: ct)).Html)).AllowAnonymous();
        b.MapApi("/avatar", async (r, ct) =>
        {
            var submission = await r.Forms.BindAsync<AvatarForm>(ct);
            if (!submission.IsValid)
                return submission.Invalid();
            var avatar = submission.Model.Avatar!;
            return FormResult.Success(data: new { avatar.FileName, avatar.DetectedType, avatar.Width, avatar.Height, avatar.Length });
        }, "POST").AllowAnonymous();
    }

    /// <summary>The hidden fields of the rendered form (CSRF, timestamp, wizard state).</summary>
    public static Dictionary<string, string> HiddenFields(string html) =>
        Hidden().Matches(html).ToDictionary(m => WebUtility.HtmlDecode(m.Groups[1].Value), m => WebUtility.HtmlDecode(m.Groups[2].Value));

    /// <summary>Builds a post body from the page's hidden fields plus <paramref name="fields"/>.</summary>
    public static FormUrlEncodedContent Post(string html, params (string Key, string Value)[] fields) =>
        new([.. HiddenFields(html).Select(p => new KeyValuePair<string, string>(p.Key, p.Value)), .. fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))]);

    /// <summary>Marks a request as coming from the WebLogic client.</summary>
    public static HttpRequestMessage AsClient(HttpRequestMessage request)
    {
        request.Headers.Add("X-Requested-With", "WebLogic");
        request.Headers.Add("Accept", "application/json");
        return request;
    }

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\">")]
    private static partial Regex Hidden();
}
