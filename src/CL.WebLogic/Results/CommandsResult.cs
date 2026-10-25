using System.Text.Json;
using CL.WebLogic.Runtime.Models;

namespace CL.WebLogic.Results;

/// <summary>A JSON command list for the WebLogic client: <c>{ "ok": true, "commands": [ … ] }</c>.</summary>
public sealed class CommandsResult : WebResult
{
    /// <summary>Creates the result.</summary>
    public CommandsResult(IReadOnlyList<WebCommand> commands) => CommandList = commands ?? [];

    /// <summary>The commands to run, in order.</summary>
    public IReadOnlyList<WebCommand> CommandList { get; }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(WebRequestContext request, CancellationToken cancellationToken)
    {
        var response = request.HttpContext.Response;
        ApplyStatusAndHeaders(response);
        response.ContentType = "application/json; charset=utf-8";
        var payload = new { ok = true, commands = CommandList.Select(c => c.ToPayload()).ToList() };
        await JsonSerializer.SerializeAsync(response.Body, payload, request.JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
