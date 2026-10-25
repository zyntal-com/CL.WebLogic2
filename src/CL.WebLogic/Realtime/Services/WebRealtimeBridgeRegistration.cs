namespace CL.WebLogic.Realtime.Services;

/// <summary>An event bridge registered at startup (<c>web.BridgeEvent&lt;T&gt;(…)</c>).</summary>
public sealed record WebRealtimeBridgeRegistration(Func<WebRealtimeBridge, IDisposable> Register);
