# ToxiProxy Integration — Feature Roadmap & Design

Canonical project: `src/Aspire.Hosting.ToxiProxy`. All file paths below are
relative to it unless noted.

All new toxic methods hang off `ToxicEndpointResource` (via the existing generic
`where TResource : ToxicEndpointResource` constraint), so a single
implementation serves both HTTP and connection-string proxies.

---

## Priority 2 — Robustness fixes

These are latent correctness bugs; worth doing before broadening surface area so
new features build on solid ground.

### 1a. Idempotent proxy population (`/populate`)

`CreateProxy` (`POST /proxies`) fails if a proxy already exists (re-run, hot
reload, or a pre-populated server). Add
`[Post("/populate")] Task Populate([Body] Proxy[] proxies)` to `IToxiClient` and
populate all proxies in one call inside `OnResourceReady`, then add toxics. This
is the ToxiProxy-recommended startup pattern and removes ordering/duplicate
errors.

### 1b. Replace hard-coded `host.docker.internal`

In `ConfigureProxy`, the upstream is `host.docker.internal:{targetPort}`. This
breaks on Linux Docker (no such host by default) and misrepresents
container-to-container targets. Design options to evaluate:

- Use Aspire's container-host substitution (`ContainerHostAddress` from the
  container's endpoint resolution) instead of a literal string.
- Fall back to `host.docker.internal` only when the target is a host-exposed
  port.

Flag this for a decision during implementation; at minimum make the host a
single constant/config point rather than inline literals in two code paths.

### 1c. `IToxiClient` completeness (needed by Priority 3)

Add the endpoints required for runtime control and health:

```csharp
[Post("/proxies/{proxyName}")]        Task UpdateProxy(string proxyName, [Body] ProxyUpdate update); // enabled toggle
[Delete("/proxies/{proxyName}/toxics/{toxicName}")] Task RemoveToxic(string proxyName, string toxicName);
[Post("/proxies/{proxyName}/toxics/{toxicName}")]   Task UpdateToxic(string proxyName, string toxicName, [Body] Toxic toxic);
[Post("/reset")]                      Task Reset();
```

### 1d. Parent null-safety & attach validation

`ToxicEndpointResource.Parent` is non-nullable but assigned late. In the
straightforward API it's always set (constructor takes the parent), but the
model still permits an unattached resource. Add a guard in
`OnResourceReady`/build that every `ToxicResource`'s proxy has a `Parent`,
throwing a clear `DistributedApplicationException` if a proxy was created but
never attached to a server. (This mainly protects the low-impact path being
deprecated, but is cheap insurance.)

**Files touched:** `Client/ToxiClient.cs`, `ToxiProxyBuilderExtensions.cs`.

---

## Priority 2 — Runtime control via Aspire dashboard commands

**Why third:** depends on the client methods from 2c. Delivers the README
"Ideas" (pause/control toxics & proxies) using the Aspire-native `WithCommand`
mechanism — buttons in the dashboard, no custom UI needed.

### Design

Add `WithCommand(...)` registrations to the resources so operators can inject/clear
chaos live:

- **On `ToxicHttpEndpointResource` / `ToxicConnectionStringResource` (the
  proxy):**
  - `Disable proxy` -> `UpdateProxy(name, enabled:false)` — this is the ToxiProxy
    "down" behavior (the missing "down" capability, delivered here as a live
    command rather than a static option).
  - `Enable proxy` -> `UpdateProxy(name, enabled:true)`.
  - `Reset toxics` -> remove all toxics on this proxy (or global `Reset()`).
- **On `ToxicResource` (individual toxic)** — optional, if we surface toxics as
  their own dashboard entries:
  - `Toggle` on/off via `RemoveToxic` / re-`AddToxic`.

Implementation notes:

- Use `WithCommand(name, displayName, executeCommand, ...)` with an icon and
  `updateState` so buttons enable/disable based on current proxy state.
- Command handlers reuse the same `RestService.For<IToxiClient>(parent.PrimaryEndpoint.Url)`
  pattern already in `ConfigureProxy`/`CheckProxyHealth`.
- Consider a `commandOptions` confirmation for destructive actions.

**Files touched:** `ToxiProxyBuilderExtensions.cs` (command registration
helpers), possibly small additions to the resource classes for state tracking.
**Design question for implementation:** whether to expose each toxic as its own
Aspire resource (richer dashboard, more nodes) or keep toxics as annotations and
only command at the proxy level. Recommend proxy-level first.

---

## Priority 3 — Broader connection-string support

**Why last:** highest risk of provider-specific edge cases; benefits from the
idempotent/robust foundation above.

### Problem

`BuildConnectionString` does:

```csharp
connectionString.Replace($"{connectionStringResource.TargetPort};", $"{port};")
```

This only works when the port is immediately followed by `;` and the host stays
reachable. It breaks for:

- **MSSQL** `Server=host,port` — comma separator, and host must also be swapped
  to the proxy host.
- **Redis** `host:port,password=...` or `host:port` (no trailing `;`).
- **MySQL/MariaDB** `Server=host;Port=nnnn;...` — port is a separate `Port=` key,
  not adjacent to host.
- **MongoDB** `mongodb://host:port/db` — URI form.
- **RabbitMQ** `amqp://user:pass@host:port/vhost` — URI form.

Also: the proxy listens inside the container network, but the app connecting to
it needs the **proxy's host + host-port**, not just a port swap — the current
code only rewrites the port, leaving the original host (works only when host is
already `localhost`/`127.0.0.1`).

### Design

Introduce a small **connection-string rewriting strategy** rather than a blind
string replace:

1. Detect format: keyword-based (`key=value;`) vs URI (`scheme://...`).
2. For keyword strings: parse with `DbConnectionStringBuilder`, locate host+port
   keys per known providers (map of provider -> host key / port key /
   combined-host-port format), rewrite host -> proxy host and port -> proxy port.
3. For URI strings: parse with `Uri`/`UriBuilder`, replace host+port.
4. Provide a way to **override** the rewriter per resource for exotic providers
   (e.g. an optional delegate parameter on `AddConnectionStringProxy`), so users
   aren't blocked when a provider isn't built-in.

Target providers to explicitly support & test (matches the README matrix):
**MSSQL, Postgres, Redis, MySQL, MariaDB** (+ MongoDB/RabbitMQ as stretch).

Also resolve the README TODO **"arbitrary external TCP service"** here: add an
`AddConnectionStringProxy` (or `AddTcpProxy`) overload that accepts a raw
upstream `host:port` (or an Aspire connection-string parameter) so any TCP
service — not just Aspire-managed DBs — can be proxied.

**Files touched:** `ToxiProxyBuilderExtensions.cs` (rewriter + overloads),
possibly a new `ConnectionStringRewriter.cs`, `ToxicConnectionStringResource.cs`
(store host as well as port).
**Tests:** parameterized tests per provider verifying the rewritten connection
string points at the proxy host:port; extend the AppHost with Redis + MySQL for
end-to-end Verify coverage.

---

## Cross-cutting / smaller items (fold into the above PRs)

- **Optional port** (README TODO): overloads of `AddHttpProxy` /
  `AddConnectionStringProxy` without `port`, auto-allocating via Aspire endpoint
  allocation or ToxiProxy's `listen: host:0` ephemeral-port feature (the
  `listen` response returns the chosen port). Reduces boilerplate — pairs
  naturally with the Priority 4 work since both touch port/endpoint handling.
- **Health checks** are already present for proxies; audit that every add-path
  registers one consistently (the low-impact `WithToxicity(endpoint)` path
  duplicates the health-check lambda inline — consolidate with
  `CheckProxyHealth`).
- **`down` as static config**: if you want a service that starts *already* down
  (not just via runtime command), add `enabled` to the proxy creation path
  (`Proxy.enabled=false`) exposed as `AddHttpProxy(..., enabled: false)` or
  `.Disabled()`. Cheap, rides along with Priority 2a's populate work.

---

## Suggested sequencing

2. **P1 robustness** (client methods + populate + host fix) — foundation.
3. **P2 dashboard commands** (depends on P2c) — delivers the "control" ideas and
   "down".
4. **P3 connection-string breadth + external TCP + optional port** (largest,
   riskiest, most provider testing).

## Out of scope (per selections)

- Repo cleanup of legacy `/Aspire.Hosting.ToxiProxy`, `/AppHost`,
  `/src/_AppHost`, empty `/src/ChaosCharp` — not selected, but noted: these
  duplicate the canonical source and are likely to cause confusion. Recommend a
  separate cleanup pass.
- Full removal of the low-impact API — the chosen direction is "straightforward",
  so new features are designed to be reachable from it and the low-impact
  overloads are marked as deprecation candidates, but actual removal is a
  separate decision.

---

## Related documents

- `Testing-Plan.md` — how each of item is covered by the test pyramid.
