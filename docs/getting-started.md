# Getting started

This walks through a small site: one page, one form, sign-in. It runs in Development with the
in-memory stores; the last section lists what to replace before going to production.

## 1. Project

```bash
dotnet new web -n MySite
cd MySite
dotnet add package CodeLogic
dotnet add package CodeLogic.WebLogic
```

## 2. Program.cs

CodeLogic starts first (libraries, configuration, your CodeLogic application), then the ASP.NET Core
host with WebLogic on top.

```csharp
using CL.WebLogic;
using CL.WebLogic.Hosting.Services;

var init = await CodeLogic.CodeLogic.InitializeAsync(o =>
{
    o.FrameworkRootPath = "codelogic";
    o.HandleShutdownSignals = false;      // ASP.NET Core owns Ctrl+C / SIGTERM
});
if (!init.Success || init.ShouldExit)
    return 1;

await WebLogicBootstrap.LoadAsync();      // the WebLogic library
await CodeLogic.CodeLogic.ConfigureAsync();
await CodeLogic.CodeLogic.StartAsync();

var builder = WebApplication.CreateBuilder(args);
builder.AddCodeLogicWebLogic(web =>
{
    web.StopCodeLogicOnShutdown();
    web.UseInMemoryAccounts();            // development only: users, sessions, keys in memory
    web.AddForm<ContactForm>();
    web.AddContributor<SitePages>();
});

var app = builder.Build();
app.UseCodeLogicWebLogic();               // security pipeline, sessions, hardening
app.MapCodeLogicWebLogic();               // your routes, assets, health, realtime, explorer
await app.RunAsync();
return 0;
```

WebLogic's settings live in `codelogic/Libraries/CL.WebLogic/config.weblogic.json`, created on first
run with every default. See [Configuration](guides/configuration.md).

## 3. A contributor

Routes, widgets and forms are contributed by `IWebContributor` classes. Every route requires a
signed-in user unless it says otherwise.

```csharp
using CL.WebLogic.Forms.Models;
using CL.WebLogic.Results;
using CL.WebLogic.Routing;
using CL.WebLogic.Routing.Services;

public sealed class SitePages : IWebContributor
{
    public Task ContributeAsync(WebContributionBuilder b, CancellationToken ct)
    {
        b.MapPage("/", (r, _) => ValueTask.FromResult<WebResult>(
            WebResult.Page("templates/home", new { Greeting = "Hello" }))).AllowAnonymous();

        b.MapPage("/contact", (r, _) => ValueTask.FromResult<WebResult>(WebResult.Page("templates/contact"))).AllowAnonymous();
        b.MapPage("/contact", async (r, ct) =>
        {
            var submission = await r.Forms.BindAsync<ContactForm>(ct);
            if (!submission.IsValid)
                return submission.Invalid(fallbackTemplate: "templates/contact");
            // ... send the message ...
            return FormResult.Success("Thanks, we will get back to you.", "/");
        }, "POST").AllowAnonymous();

        b.MapPage("/members", (r, _) => ValueTask.FromResult<WebResult>(
            WebResult.Page("templates/members", new { r.Identity.UserName })));   // sign-in required
        return Task.CompletedTask;
    }
}
```

## 4. A form

Forms are plain classes. DataAnnotations give the rules, `[WebField]` the presentation; the same
rules run in the browser and on the server.

```csharp
using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;

[WebForm(Id = "contact", SubmitLabel = "Send")]
public sealed class ContactForm
{
    [Required, StringLength(80)]
    public string? Name { get; set; }

    [Required, EmailAddress]
    public string? Email { get; set; }

    [Required, StringLength(4000)]
    [WebField(WebFieldType.TextArea, Label = "Message")]
    public string? Message { get; set; }
}
```

## 5. The theme

By default the theme is the `theme` folder next to the app:

```
theme/
  layouts/main.html
  templates/home.html
  templates/contact.html
  templates/members.html
  assets/site.css
```

`theme/layouts/main.html`:

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  {renderhead}
  <link rel="stylesheet" href="{asset:site.css}">
  {weblogic_scripts}
</head>
<body>
  <main data-wl-shell>{renderbody}</main>
</body>
</html>
```

`theme/templates/contact.html`:

```html
{layout:layouts/main}
<h1>Contact us</h1>
{form:contact}
```

`{form:contact}` renders the form with labels, ARIA, validation attributes, the CSRF field and the
anti-bot fields. Values are always HTML-encoded; see [Templates](guides/templates.md).

## 6. Run it

```bash
dotnet run
```

- `/` and `/contact` work anonymously; `/members` redirects to `Security.LoginPath`.
- `/_weblogic/explorer` lists every route with its policy and the security pipeline (Development only by default).
- Response headers already include a CSP with a nonce, `X-Content-Type-Options` and the other hardening headers.

## Before production

Outside Development, WebLogic refuses to start while a security contract still uses an in-memory
development store. Replace them with your own implementations:

```csharp
web.UseUserStore<MyUserStore>()
   .UseSessionStore<MySessionStore>()
   .UseApiKeyStore<MyApiKeyStore>()              // only if you offer API keys
   .UseRecoveryCodeStore<MyRecoveryCodeStore>()  // only if you offer two-factor sign-in
   .UseMailNotifications();                      // security e-mails via CL.Mail
```

Then set `PublicBaseUrl`, allowed hosts and (behind a proxy) trusted proxies. The
[Deployment](guides/deployment.md) guide has the checklist; the samples show complete stores for
JSON files (`StarterWebsite`) and MySQL (`MiniBlog`).
