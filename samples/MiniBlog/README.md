# MiniBlog

A blog built on CL.WebLogic and CL.MySQL2. Run it with Docker as described in [`../docker`](../docker/README.md), or locally against the compose MySQL:

```bash
docker compose -f samples/docker/compose.yaml up -d mysql
dotnet run --project samples/MiniBlog     # http://localhost:5081
```

| Project | Contents |
|---|---|
| `MiniBlog` | The host. `BlogPages` serves home, tags, post, RSS and sitemap from the output cache, tagged `posts`. `AccountPages` handles sign-in, sign-out and password reset. `BlogSeeder` syncs the schema and seeds the editor account and demo posts once. `PluginInstaller` copies the shipped plugins into the writable data volume. |
| `MiniBlog.Data` | CL.MySQL2 entities, `BlogPostService`, and the MySQL stores WebLogic asks for: users (with lockout), sessions, recovery codes, widget settings and dashboards. Shared by the host and the plugin. |
| `Plugins/MiniBlog.Admin.Plugin` | The admin area as a CodeLogic plugin. It contributes its own templates (embedded from `theme/`, `b.MapTemplates(...)`), `/admin` routes (admin group only), the `post-editor` form (`b.MapForm<T>()`) and an editors-only sidebar widget. All of these go away if the plugin unloads. Every write evicts the `posts` cache tag. |

Post bodies are Markdown. HTML typed into a post is shown as text, and links only allow safe schemes.

The host theme can override any plugin template by putting a file at the same path, for example `theme/admin/index.html`.
