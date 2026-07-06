# Testing Plan — Functional Coverage of the ToxiProxy Integration

## Goal

Assert the ToxiProxy server configuration (proxies + toxics + rewritten
connection strings) that the integration produces, with **fast unit tests
carrying the bulk of functional coverage** and **`Aspire.Hosting.Testing`
reduced to a minimal end-to-end footprint** (2 tests, quarantined in
`test/Tests`).

Canonical source under test: `src/Aspire.Hosting.ToxiProxy`.

## Central idea

The config-building logic is currently entangled with runtime concerns, so the
only way to observe "what ToxiProxy receives" is a full container boot + HTTP
scrape (`Test.Check_toxiproxy_config`). Specifically, in
`ToxiProxyBuilderExtensions.cs`:

- **Payload mapping** (domain `Toxic` -> client `Toxic`/`Attributes`, the
  `if/else if` type -> attribute logic) is inline in
  `private static ConfigureProxy(...)` — only reachable after `OnResourceReady`
  fires against a live server.
- **Port/upstream resolution** reads `AllocatedEndpoint` /
  `GetEndpoint("http").Port` — needs DCP allocation.
- **Connection-string rewriting** (`BuildConnectionString`) runs inside
  `OnConnectionStringAvailable`.

The plan **separates the decision logic** (what config *should* be) **from the
delivery logic** (HTTP calls, endpoint-allocation timing), then unit-tests the
decision logic exhaustively without booting a container.

## Test pyramid (target distribution)

| Layer | What it covers | Tech | Speed | Project |
|---|---|---|---|---|
| **1. Unit — payload mapping** | Every toxic type -> correct `Toxic`/`Attributes` JSON | xUnit + Verify, no Aspire host | ms | `test/UnitTests` |
| **2. Unit — model building** | `builder.Build()` model: proxies registered, annotations, ports, toxic list, health checks, `WithReference` | xUnit + in-memory builder (no `Start`) | ms | `test/UnitTests` |
| **3. Unit — connection-string rewriter** | Per-provider rewrite correctness (MSSQL, Postgres, Redis, MySQL, …) | pure xUnit `[Theory]` | ms | `test/UnitTests` |
| **4. Contract — client serialization** | `IToxiClient` request routes/bodies match ToxiProxy's wire format | xUnit + stub `HttpMessageHandler` | ms | `test/UnitTests` |
| **5. E2E smoke — 2 tests** | Whole thing boots, real container, config lands + services wire up | `Aspire.Hosting.Testing` + Verify | seconds, Docker | `test/Tests` |

---

## Layer-by-layer detail

### Layer 1 — Toxic payload mapping (highest ROI)

- **What:** For each builder method (`AddLatency`, `AddBandwidthLimit`,
  `AddSlowClose`, `AddTimeout`, `AddResetPeer`, `AddSlicer`, `AddLimitData`),
  assert the produced `Client.Toxic` (name, type, stream, toxicity, attributes)
  serializes to the exact JSON ToxiProxy expects.
- **How:** Call the builder method on a minimal in-memory resource, run the
  extracted `ToxicMapper`, `VerifyJson(...)` the serialized payload. One
  snapshot per toxic.
- **Covers:** direction -> stream mapping, toxicity defaults, snake_case
  attribute names (`average_size`, `size_variation`, `rate`), null-omission of
  unused attributes.
- **No Aspire host needed.**

### Layer 2 — Model composition

- **What:** After `builder.Build()` (NOT `Start()`), assert the in-memory model:
  - `AddHttpProxy` / `AddConnectionStringProxy` add the resource + the
    `EndpointAnnotation` (scheme, port) on `ToxiProxyResource`.
  - Toxics are attached to the right proxy in the right order.
  - Health checks registered (`{name}_check`).
  - `WithReference` wires the expected env vars / connection name.
- **How:** Build the `IDistributedApplicationBuilder`, inspect `app.Resources`,
  annotations, and the health-check registrations. Uses `Aspire.Hosting` (the
  builder) but **not** `Aspire.Hosting.Testing`, and **never starts
  containers** — runs in ms.
- **Covers:** the wiring correctness that today only surfaces at runtime.

### Layer 3 — Connection-string rewriting

- **What:** Table-driven tests: input connection string + proxy host/port ->
  expected rewritten string, per provider (MSSQL comma-port, Postgres, Redis
  `host:port`, MySQL `Port=`, URI-style). Also negative cases (unmatched format
  -> clear exception or documented fallback).
- **How:** Pure function calls against `ConnectionStringRewriter`,
  `[Theory]`/`[InlineData]` or Verify per provider.
- **Ties into** the P4 feature work — tests written against the rewriter
  contract.

### Layer 4 — Client contract (serialization safety net)

- **What:** Guarantee `IToxiClient` (Refit) emits the exact routes/bodies
  ToxiProxy accepts (`POST /proxies`, `POST /proxies/{p}/toxics`, `/populate`,
  update/delete/reset).
- **How:** Refit client over a stub `HttpMessageHandler` that captures the
  request; assert method + path + JSON body. No network, no container.
- **Covers:** regressions in JSON attribute names / enum string values
  (`slow_close`, `reset_peer`, etc.) independent of the mapper.

### Layer 5 — E2E smoke (2 tests, `Aspire.Hosting.Testing`)

- **What:** Keep exactly two boot-the-world tests as the integration guarantee
  that the real container accepts what we generate and the pieces connect:
  1. **Config assertion** — the existing `Check_toxiproxy_config`: start ->
     `GET /proxies` -> `VerifyJson`.
  2. **Wiring smoke** — the existing `Check_wiring_between_demoApi_and_weatherApi`.
- **How:** `DistributedApplicationTestingBuilder.CreateAsync<AppHost>` -> start
  -> assert. Keep the `TEST_RUN=true` fast path (no UI, fixed ports) already in
  `AppHost.cs`.
- **Scope discipline:**
  - These tests assert **presence/shape end-to-end**, not every toxic
    permutation (those are Layer 1).
  - The AppHost keeps a **representative** sample (one HTTP proxy, one DB proxy,
    a couple toxics) — it does **not** need one of every toxic, since Layer 1
    covers exhaustiveness.
  - Tag with `[Trait("Category", "EndToEnd")]` so they can be excluded in
    fast/CI-inner loops and Docker-less environments.

---

## Project structure

- **New project `test/UnitTests`** (`Aspire.Hosting.ToxiProxy.UnitTests`):
  - References only `src/Aspire.Hosting.ToxiProxy` + `Aspire.Hosting` + xUnit +
    Verify + coverlet.
  - **No** `Aspire.Hosting.Testing`, **no** `AppHost`/`WeatherApi` project refs.
  - Hosts Layers 1–4.
  - Add the project to `Aspire.Hosting.ToxiProxy.slnx`.
- **Existing `test/Tests`** (deps unchanged): slimmed to Layer 5 (the 2 E2E
  tests). Keeps the `Aspire.Hosting.Testing` dependency isolated here.
- **`InternalsVisibleTo`** from `src` -> `UnitTests` for the internal
  mapper/rewriter.

This keeps the slow/Docker dependency quarantined in one project and two tests.

---

## CI / execution

- Split into two test runs:
  - **Fast** (`dotnet test test/UnitTests`): no Docker, runs on every push/PR,
    gates merges.
  - **E2E** (`dotnet test test/Tests`): Docker-dependent, runs where a daemon is
    available (separate CI job/stage; filterable via the `EndToEnd` trait).
- **Coverage:** `coverlet` is already referenced. Measure coverage primarily
  from the **fast** run so functional coverage is attributed to unit tests, per
  the goal of this plan.

---

## Roadmap feature -> owning test layer

| Feature (from roadmap) | Primary layer(s) |
|---|---|
| P1 — new toxic types (`slow_close`, `timeout`, `reset_peer`, `slicer`, `limit_data`) | Layer 1 (mapping) + Layer 4 (wire format) |
| P1 — `ConfigureProxy` `switch` refactor | Layer 1 |
| P2 — `/populate` idempotency | Layer 4 (request body) + Layer 5 (real server accepts) |
| P2 — replace `host.docker.internal` | Layer 2 (upstream resolution) + Layer 5 |
| P2 — `IToxiClient` update/delete/reset | Layer 4 |
| P3 — dashboard commands (enable/disable, reset) | Layer 2 (command registration) + Layer 4 (client calls) |
| P4 — broader connection-string support | Layer 3 (rewriter) |
| P4 — external TCP proxy / optional port | Layer 2 + Layer 3 |

---

## Suggested sequencing

1. Land the **enabling refactor** (mapper, rewriter, port-resolution split,
   `InternalsVisibleTo`).
2. Stand up **`test/UnitTests`** and implement Layers 1–4.
3. Slim **`test/Tests`** to the 2 tagged E2E tests.
4. Split **CI** into fast + E2E jobs and point coverage at the fast run.
