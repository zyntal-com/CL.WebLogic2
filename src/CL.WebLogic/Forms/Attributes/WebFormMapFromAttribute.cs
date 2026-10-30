namespace CL.WebLogic.Forms.Attributes;

/// <summary>
/// Pairs a property with a differently named property on the other side of a
/// <see cref="Services.WebFormMapper"/> mapping. On a form property it names the entity property;
/// on an entity property it names the form property. Names match case-insensitively.
/// </summary>
/// <example>
/// <code>
/// public sealed class PostForm
/// {
///     [WebFormMapFrom(nameof(Post.Headline))]
///     public string? Title { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class WebFormMapFromAttribute : Attribute
{
    /// <summary>Creates the attribute.</summary>
    /// <param name="name">The name of the property on the other side.</param>
    public WebFormMapFromAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The name of the property on the other side.</summary>
    public string Name { get; }
}
