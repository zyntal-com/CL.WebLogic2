namespace CL.WebLogic.Widgets.Models;

/// <summary>Renders a widget.</summary>
public delegate ValueTask<WebWidgetResult> WebWidgetHandler(WebWidgetContext context, CancellationToken cancellationToken);
