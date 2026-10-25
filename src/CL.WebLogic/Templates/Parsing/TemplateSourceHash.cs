using System.Security.Cryptography;
using System.Text;

namespace CL.WebLogic.Templates.Parsing;

/// <summary>
/// Canonical template source hash shared by the source generator (build time) and the theme
/// engine (render time). A leading BOM is dropped and CRLF is normalized to LF first, so
/// end-of-line conversion never invalidates a compiled template.
/// </summary>
public static class TemplateSourceHash
{
    /// <summary>Upper-case hex SHA-256 of the normalized source.</summary>
    public static string Compute(string source)
    {
        var text = source ?? string.Empty;
        if (text.Length > 0 && text[0] == '﻿')
            text = text.Substring(1);
        text = text.Replace("\r\n", "\n");

        // Shared with the netstandard2.0 source generator, which has no SHA256.HashData.
#pragma warning disable CA1850
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
#pragma warning restore CA1850

        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            builder.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
