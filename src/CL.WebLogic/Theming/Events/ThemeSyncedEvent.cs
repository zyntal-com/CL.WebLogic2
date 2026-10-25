using CodeLogic.Core.Events;

namespace CL.WebLogic.Theming.Events;

/// <summary>Published after the theme was synchronized from its source.</summary>
/// <param name="Source">The source kind (Local, Git, Storage).</param>
/// <param name="LocalPath">The local theme root now in use.</param>
public sealed record ThemeSyncedEvent(string Source, string LocalPath) : IEvent;
