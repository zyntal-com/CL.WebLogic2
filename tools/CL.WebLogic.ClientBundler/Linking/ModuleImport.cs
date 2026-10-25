namespace CL.WebLogic.ClientBundler.Linking;

/// <summary>One <c>import</c> statement: <c>{ a, b as c }</c>, <c>* as ns</c>, or a side-effect import.</summary>
internal sealed record ModuleImport(string Specifier, string ResolvedPath, IReadOnlyList<(string Imported, string Local)> Names, string? Namespace, int Line);
