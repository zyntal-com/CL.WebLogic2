namespace CL.WebLogic.Forms.Models;

/// <summary>One step of a multi-step form.</summary>
public sealed record WebFormStep(string Name, string Title, int Order);
