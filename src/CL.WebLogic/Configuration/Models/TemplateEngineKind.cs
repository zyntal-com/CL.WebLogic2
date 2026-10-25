namespace CL.WebLogic.Configuration.Models;

/// <summary>Template execution engine.</summary>
public enum TemplateEngineKind
{
    /// <summary>Templates are compiled to delegate trees once and re-used (default).</summary>
    Closure,

    /// <summary>The AST is walked on every render — the reference implementation.</summary>
    Interpreter
}
