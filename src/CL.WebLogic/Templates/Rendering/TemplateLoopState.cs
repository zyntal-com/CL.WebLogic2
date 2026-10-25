namespace CL.WebLogic.Templates.Rendering;

/// <summary>Loop metadata exposed inside a <c>{foreach}</c> through the <c>{loop:…}</c> scope.</summary>
public sealed class TemplateLoopState
{
    /// <summary>Creates the state for item <paramref name="index"/> of <paramref name="count"/>.</summary>
    public TemplateLoopState(int index, int count)
    {
        Index = index;
        Count = count;
    }

    /// <summary>Zero-based index.</summary>
    public int Index { get; }

    /// <summary>Number of items.</summary>
    public int Count { get; }

    /// <summary>One-based position.</summary>
    public int Number => Index + 1;

    /// <summary>True for the first item.</summary>
    public bool First => Index == 0;

    /// <summary>True for the last item.</summary>
    public bool Last => Index == Count - 1;

    /// <summary>True for even zero-based indexes.</summary>
    public bool Even => Index % 2 == 0;

    /// <summary>True for odd zero-based indexes.</summary>
    public bool Odd => Index % 2 != 0;

    /// <summary>Value for <c>{loop:key}</c>, or null for an unknown key.</summary>
    public object? Get(string key) => key.Trim().ToLowerInvariant() switch
    {
        "index" => Index,
        "number" => Number,
        "first" => First,
        "last" => Last,
        "count" => Count,
        "even" => Even,
        "odd" => Odd,
        _ => null
    };
}
