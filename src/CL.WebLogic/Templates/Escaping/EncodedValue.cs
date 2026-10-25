namespace CL.WebLogic.Templates.Escaping;

/// <summary>
/// A value already encoded for one specific context (produced by filters such as <c>json</c>).
/// Written as-is in that context; in any other context it is encoded again as plain text.
/// </summary>
public sealed class EncodedValue
{
    /// <summary>Creates the value.</summary>
    public EncodedValue(string text, TemplateEncodingContext context)
    {
        Text = text ?? string.Empty;
        Context = context;
    }

    /// <summary>The encoded text.</summary>
    public string Text { get; }

    /// <summary>The context the text is safe in.</summary>
    public TemplateEncodingContext Context { get; }

    /// <inheritdoc/>
    public override string ToString() => Text;
}
