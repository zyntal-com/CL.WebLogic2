namespace MiniBlog.Data.Posts;

/// <summary>The posts a fresh blog starts with (seeded once; edits are never overwritten).</summary>
public static class DemoContent
{
    /// <summary>Seed marker key; bump to seed a new set.</summary>
    public const string SeedVersion = "posts-v1";

    /// <summary>The demo posts.</summary>
    public static IReadOnlyList<BlogPostDraft> Posts { get; } =
    [
        new("Welcome to the Northwind Journal",
            """
            This blog is the **MiniBlog** sample for CL.WebLogic. Posts live in MySQL through `CL.MySQL2`;
            WebLogic renders them with the theme and encodes everything on the way out.

            - Pages are served from the output cache until a post changes.
            - Signing in, sessions and recovery codes use MySQL stores the app provides.
            - The admin area is a plugin loaded from the `plugins` folder at startup.

            Sign in at [/login](/login) and open the [admin area](/admin).
            """, ["weblogic", "meta"], true, "The editors"),
        new("Why the toolkit owns no database",
            """
            WebLogic defines *contracts* — `IWebUserStore`, `IWebSessionStore`, `IWebDashboardLayoutStore` — and
            the application decides where data lives. This blog uses MySQL; the starter site uses JSON files.

            > Keep the toolkit generic: persistence is policy, and policy belongs to the app.
            """, ["weblogic", "architecture"], true, "The editors"),
        new("Markdown, safely",
            """
            Bodies are Markdown. HTML typed into a post is shown as text, links only allow safe schemes,
            and code is escaped:

            ```
            <script>alert('this stays text')</script>
            ```

            Try it in the editor: write some `<b>HTML</b>` and see it encoded.
            """, ["markdown", "security"], true, "The editors")
    ];
}
