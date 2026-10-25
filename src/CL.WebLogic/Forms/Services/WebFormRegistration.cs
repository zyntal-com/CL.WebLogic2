namespace CL.WebLogic.Forms.Services;

/// <summary>A form registered in DI (<c>web.AddForm&lt;T&gt;()</c>).</summary>
public sealed record WebFormRegistration(Type ModelType);
