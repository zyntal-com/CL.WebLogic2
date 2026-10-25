using CL.WebLogic.Results;

namespace CL.WebLogic.Widgets.Models;

/// <summary>Handles a widget action (POST). Return commands/JSON, or <see cref="WebWidgetActionContext.RefreshAsync"/> to re-render.</summary>
public delegate ValueTask<WebResult> WebWidgetActionHandler(WebWidgetActionContext context, CancellationToken cancellationToken);
