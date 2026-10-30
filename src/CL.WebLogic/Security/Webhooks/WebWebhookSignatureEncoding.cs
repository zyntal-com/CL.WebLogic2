namespace CL.WebLogic.Security.Webhooks;

/// <summary>How a webhook sender writes the HMAC in its signature header.</summary>
public enum WebWebhookSignatureEncoding
{
    /// <summary>Hexadecimal, upper or lower case (GitHub, Stripe, Slack).</summary>
    Hex,

    /// <summary>Standard Base64 (Shopify, many others).</summary>
    Base64
}
