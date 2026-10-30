namespace CL.WebLogic.Runtime.Models;

/// <summary>A one-shot message carried to the next page (shown by the client as a toast).</summary>
/// <param name="Message">Plain text; the client never treats it as HTML.</param>
/// <param name="Variant"><c>success</c>, <c>info</c>, <c>warning</c> or <c>danger</c>.</param>
public sealed record WebFlashMessage(string Message, string Variant);
