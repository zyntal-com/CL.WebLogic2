namespace StarterWebsite.Widgets;

/// <summary>A message on the live wall.</summary>
public sealed record Shout(string Author, string Text, DateTimeOffset At);
