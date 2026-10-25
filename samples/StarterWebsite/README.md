# StarterWebsite

The reference site for CL.WebLogic. Run it with `dotnet run --project samples/StarterWebsite` (http://localhost:5080), or with Docker as described in [`../docker`](../docker/README.md).

## Layout

| Folder | Contents |
|---|---|
| `Program.cs` | Boot sequence: env → config files, CodeLogic, then the ASP.NET host. |
| `Application/` | `StarterSetup` registers everything with `builder.AddCodeLogicWebLogic(...)`. Also the CodeLogic application and the admin seeder. |
| `Infrastructure/` | **App-owned persistence**: JSON-file stores for users, sessions, API keys, recovery codes, dashboards and widget settings. The toolkit only defines the contracts. |
| `Pages/` | Contributors: accounts (`AccountPages.*`), forms, dashboard, live wall, caching, `/api/v1`. |
| `Forms/` | Model classes that drive forms: markup, client rules, server validation and binding. |
| `Widgets/` | Clock, members, notes (per-instance settings with an ownership check), quick links. Also the live-wall service. |
| `Steam/` | An `IWebExternalLoginProvider` for Steam OpenID 2.0. |
| `theme/` | Layout, templates, widget templates, error pages, `site.js` page modules, Bootstrap 5 assets. |

Things to try:
- Register an account and click the verification mail.
- Enable two-factor, then sign in with a recovery code.
- Create an API key and `curl /api/v1/me` with it.
- Arrange the dashboard with Alt+arrow keys.
- Open the live wall in two browsers.
- Reload `/cache` while signed out, then while signed in.
