namespace CL.WebLogic.Forms.Models;

/// <summary>A field is shown (and validated) only while <see cref="Field"/> has one of <see cref="Values"/>.</summary>
public sealed record WebShowWhen(string Field, IReadOnlyList<string> Values);
