# Cluster

Several instances of the same app behind a load balancer. Anything one node writes and another node
must read has to live outside the process. WebLogic can check this for you at startup.

## Turn on the check

```json
"cluster": { "enabled": true }
```

With cluster mode on, startup **fails** while any shared contract still uses a per-node
implementation, or the DataProtection key ring is not shared, and warns about the softer ones. The
report lists exactly what to fix.

## What must be shared

| Contract | Holds | Register with |
|---|---|---|
| `IWebSessionStore` | Sessions | `web.UseSessionStore<T>()` |
| `IWebUserStore`, `IWebApiKeyStore`, `IWebRecoveryCodeStore`, `IWebPasskeyStore` | Accounts | `UseUserStore`, `UseApiKeyStore`, `UseRecoveryCodeStore`, `UsePasskeyStore` |
| `IWebOneTimeUseStore` | Spent TOTP steps, single-use links, spent passkey challenges | `web.UseOneTimeUseStore<T>()` |
| `IWebLoginThrottle` | Sign-in back-off | `web.UseLoginThrottle<T>()` |
| `IWebRateLimitStore` | Rate-limit buckets | `web.UseRateLimitStore<T>()` |
| `IWebRealtimeBackplane` | Realtime fan-out | `web.UseRealtimeBackplane<T>()` |
| `IWebSecurityNotifier` | E-mail (the outbox writes to a local folder) | `web.UseMailNotifications()` |
| DataProtection key ring | Cookies, CSRF secrets, link tokens, 2FA secrets, wizard state | `AddDataProtection().PersistKeysToCodeLogicStorage(connectionId).ProtectKeysWithSecret("KEYRING_SECRET").SetApplicationName(...)` ([Deployment](../guides/deployment.md#dataprotection-keys)) |

Advised (warnings): widget settings, dashboards, the fragment cache and the output cache store.

The toolkit has no Redis or database dependency; implement these contracts on whatever your cluster
already runs (Redis, SQL, a document store). Each is small: the session store is five methods, the
backplane two. On MySQL, the [`CodeLogic.WebLogic.MySql`](../guides/mysql-stores.md) add-on implements
every store above plus the key ring (`UseMySqlStores`, `PersistKeysToMySql`); MiniBlog runs on it.

## DataProtection

```csharp
builder.Services.AddDataProtection()
    .SetApplicationName("my-site")                 // the same on every node
    .PersistKeysToCodeLogicStorage("shared")       // CL.Storage; or PersistKeysToMySql(...) with the MySQL add-on
    .ProtectKeysWithSecret("KEYRING_SECRET");     // encrypted at rest
```

Without a shared key ring a session started on node A cannot be read on node B, and 2FA secrets
encrypted on one node cannot be decrypted on another.

## Deliberate trade-offs

Some contracts may stay per node on purpose, for example per-node rate limits behind sticky sessions:

```json
"cluster": { "enabled": true, "allowNodeLocal": ["IWebRateLimitStore", "IWebLoginThrottle"] }
```

Each allowed one is logged at startup. Per-node throttles mean an attacker spread across nodes gets
N times the attempts; account lockout in the shared user store still applies.

## Other things to check

- **Output cache:** register a shared `IOutputCacheStore`, or evicting a tag only clears one node.
- **Theme:** every node needs the same theme (bake it into the image, or use the Git/Storage source).
- **Uploads:** store through an `IWebUploadSink` on shared storage (CL.Storage), not the local disk.
- **Sticky sessions** are not required. WebSocket/SSE connections stay on the node they connected to;
  the backplane makes sure every node delivers every event.
- **Health:** point the load balancer at `/_weblogic/health/ready`.

## Testing a cluster locally

Run two instances on different ports against the same stores and key folder, sign in on one and load a
page on the other. `samples/docker` is a good starting point: scale the starter service after moving
its JSON stores to a shared database.
