namespace CL.WebLogic.ClientBundler.Linking;

/// <summary>A source error with its location.</summary>
internal sealed class BundleException : Exception
{
    /// <summary>Creates the exception.</summary>
    public BundleException(string message, string file, int line) : base(message)
    {
        File = file;
        Line = line;
    }

    /// <summary>Creates the exception (required by CA1032).</summary>
    public BundleException() : this("bundle error", "<unknown>", 0)
    {
    }

    /// <summary>Creates the exception (required by CA1032).</summary>
    public BundleException(string message) : this(message, "<unknown>", 0)
    {
    }

    /// <summary>Creates the exception (required by CA1032).</summary>
    public BundleException(string message, Exception innerException) : base(message, innerException)
    {
        File = "<unknown>";
    }

    /// <summary>Source file.</summary>
    public string File { get; } = "<unknown>";

    /// <summary>1-based line.</summary>
    public int Line { get; }
}
