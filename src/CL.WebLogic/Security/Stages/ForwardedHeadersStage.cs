using System.Net;
using CL.WebLogic.Security.Pipeline;
using CL.WebLogic.Security.Services;
using Microsoft.AspNetCore.Http;

namespace CL.WebLogic.Security.Stages;

/// <summary>
/// Applies <c>X-Forwarded-For/Proto/Host/Prefix</c> — only when the direct peer is a configured
/// trusted proxy. Walks <c>X-Forwarded-For</c> from the right through trusted hops, up to
/// <c>ForwardLimit</c>. A forwarded host is accepted only if it is an allowed host.
/// </summary>
public sealed class ForwardedHeadersStage : IWebSecurityStage
{
    /// <inheritdoc/>
    public string Name => "forwarded-headers";

    /// <inheritdoc/>
    public WebSecurityPhase Phase => WebSecurityPhase.PreRouting;

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public ValueTask<WebSecurityDecision> ExecuteAsync(WebSecurityContext context, CancellationToken cancellationToken)
    {
        var settings = context.Settings;
        var config = settings.Config.ForwardedHeaders;
        if (!config.Enabled || !IpNetworkParser.ContainsAny(settings.TrustedProxies, context.ClientIp))
            return ValueTask.FromResult(WebSecurityDecision.Continue);

        var request = context.HttpContext.Request;
        var client = context.ClientIp!;
        var applied = 0;
        var forwardedFor = request.Headers["X-Forwarded-For"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = forwardedFor.Length - 1; i >= 0 && applied < config.ForwardLimit; i--)
        {
            // Some proxies append the client port ("1.2.3.4:5678", "[2001:db8::1]:443").
            if (!IPAddress.TryParse(forwardedFor[i], out var hop))
            {
                if (!IPEndPoint.TryParse(forwardedFor[i], out var endpoint))
                    break;
                hop = endpoint.Address;
            }
            client = IpNetworkParser.Normalize(hop);
            applied++;
            if (!IpNetworkParser.ContainsAny(settings.TrustedProxies, client))
                break;
        }

        if (applied > 0)
        {
            context.ClientIp = client;
            context.HttpContext.Connection.RemoteIpAddress = client;
        }

        var proto = LastValue(request.Headers["X-Forwarded-Proto"]);
        if (string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase) || string.Equals(proto, "http", StringComparison.OrdinalIgnoreCase))
            request.Scheme = proto!.ToLowerInvariant();

        var host = LastValue(request.Headers["X-Forwarded-Host"]);
        if (!settings.AllowedHosts.AllowsAny && host is not null && HostString.FromUriComponent(host) is { HasValue: true } forwardedHost
            && settings.AllowedHosts.IsAllowed(forwardedHost.Host))
            request.Host = forwardedHost;

        var prefix = LastValue(request.Headers["X-Forwarded-Prefix"]);
        if (config.HonorPrefix && IsSafePrefix(prefix))
            request.PathBase = new PathString(prefix!.TrimEnd('/')) + request.PathBase;

        return ValueTask.FromResult(WebSecurityDecision.Continue);
    }

    private static string? LastValue(Microsoft.Extensions.Primitives.StringValues values)
    {
        var text = values.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var comma = text.LastIndexOf(',');
        return (comma < 0 ? text : text.Substring(comma + 1)).Trim();
    }

    private static bool IsSafePrefix(string? prefix) =>
        prefix is { Length: > 1 and <= 256 } && prefix[0] == '/' && !prefix.Contains("//", StringComparison.Ordinal)
        && !prefix.Contains("..", StringComparison.Ordinal)
        && prefix.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.');
}
