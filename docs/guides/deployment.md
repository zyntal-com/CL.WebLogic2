# Deployment

## Production checklist

| | Setting / code | Why |
|---|---|---|
| ☐ | Real stores for users, sessions, API keys, recovery codes | The app refuses to start with in-memory security stores outside Development |
| ☐ | `UseMailNotifications()` (or your own notifier) | Reset, verification and alert e-mails; the outbox notifier refuses to run in production |
| ☐ | DataProtection keys persisted (`PersistKeysTo...`) and an application name set | Session tokens, 2FA secrets, link tokens and wizard state are protected with these keys; losing them signs everybody out and breaks 2FA |
| ☐ | `publicBaseUrl` = the public `https://` URL | Links in e-mails, canonical redirects, secure cookies behind a TLS proxy |
| ☐ | `security.hosts.allowedHosts` | Host header attacks, cache poisoning |
| ☐ | `security.forwardedHeaders` with the proxy's address (behind a proxy) | Real client IPs for rate limits, lockout and audit |
| ☐ | `auth.passwords.pepper` (optional), `security.audit.ipHashKey` | Secrets: keep them out of source control |
| ☐ | `auth.twoFactor.issuer`, `auth.notifications.fromAddress` | What users see in authenticator apps and e-mails |
| ☐ | Review `/_weblogic/explorer.json` in a staging run | Every anonymous route should be intended |
| ☐ | `theme.templateChangeCheckSeconds = 0` for immutable themes | No file checks per render |

## Behind a reverse proxy

```json
"publicBaseUrl": "https://www.example.com",
"security": {
  "forwardedHeaders": { "enabled": true, "trustedProxies": ["172.18.0.2"], "forwardLimit": 1 },
  "hosts": { "allowedHosts": ["www.example.com"] },
  "https": { "enforce": true }
}
```

Mounted under a path (`https://example.com/app/`)? Set `forwardedHeaders.honorPrefix` and let the proxy
send `X-Forwarded-Prefix: /app`; generated URLs then include the prefix.

## Docker

`samples/docker` is a complete, deployable example: a multi-stage Dockerfile that packs WebLogic to a
local feed and publishes the samples against it, a non-root runtime image with a health probe
(`dotnet App.dll --probe`), named volumes for data and keys, MySQL, Mailpit and an optional Caddy proxy
with automatic HTTPS. See its README for `docker compose up` and server deployment.

## Health

- `/_weblogic/health/live`: the process answers.
- `/_weblogic/health/ready`: CodeLogic libraries healthy, WebLogic ready, `restartPending` when a
  config change needs a restart.

Both are exempt from the HTTPS redirect (`security.probePaths`) so container probes can use plain HTTP.

## Several instances

See the [cluster scenario](../scenarios/cluster.md): shared stores, a shared key ring, a realtime
backplane, and `cluster.enabled` to have WebLogic check all of it at startup.
