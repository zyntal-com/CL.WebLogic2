using System.ComponentModel.DataAnnotations;
using CL.WebLogic.Forms.Attributes;
using CL.WebLogic.Forms.Models;
using MiniBlog.Data.Posts;

namespace MiniBlog.Admin;

/// <summary>The post editor (registered by the plugin with <c>b.MapForm&lt;PostEditorForm&gt;()</c>).</summary>
[WebForm(Id = "post-editor", SubmitLabel = "Save", AntiBot = false)]
public sealed class PostEditorForm
{
    /// <summary>Title.</summary>
    [Required, StringLength(180, MinimumLength = 3)]
    [WebField(Label = "Title")]
    public string? Title { get; set; }

    /// <summary>Comma-separated tags.</summary>
    [StringLength(300), RegularExpression("^[A-Za-z0-9 ,_-]*$", ErrorMessage = "Tags use letters, digits, spaces, '-' and '_'.")]
    [WebField(Label = "Tags", Help = "Comma separated, e.g. weblogic, security")]
    public string? Tags { get; set; }

    /// <summary>Markdown body.</summary>
    [Required, StringLength(100_000)]
    [WebField(WebFieldType.TextArea, Label = "Body (Markdown)", Help = "HTML is shown as text; links allow http(s) and mailto.")]
    public string? Body { get; set; }

    /// <summary>Visible on the site.</summary>
    [WebField(WebFieldType.Checkbox, Label = "Published")]
    public bool Published { get; set; }

    /// <summary>The editor's input as a draft.</summary>
    public BlogPostDraft ToDraft(string author) =>
        new(Title!, Body!, (Tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), Published, author);

    /// <summary>The form filled from a post.</summary>
    public static PostEditorForm From(BlogPost post)
    {
        ArgumentNullException.ThrowIfNull(post);
        return new() { Title = post.Title, Tags = string.Join(", ", post.Tags), Body = post.Body, Published = post.Published };
    }
}
