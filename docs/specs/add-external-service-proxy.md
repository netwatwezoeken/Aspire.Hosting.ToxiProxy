# Spec: AddExternalServiceProxy

**Feature:** Wrap an Aspire `ExternalServiceResource` with ToxiProxy so that toxics
(latency, bandwidth limits, packet loss, etc.) can be injected between the app and
an external dependency.

**Status:** Draft — awaiting approval gate (Phase 1 → 2)

---

## 1. Intent

`AddExternalService` in Aspire represents a dependency that is not managed by the
Aspire host — e.g. a third-party HTTP API, a vendor service, or a locally-running
process outside the solution. Today there is no way to wrap such a resource with
ToxiProxy, making it impossible to test resilience against external-service failures.

This feature closes that gap by adding `AddExternalServiceProxy` to the
`ToxiProxyResource` builder.

---

## 2. User-Facing API

### 2.1 Declare the proxy

```csharp
var weatherApi = builder.AddExternalService("weather-api", "http://api.weather.com:80");

var toxiProxy = builder.AddToxiProxyServer("toxiproxy");

var toxicWeatherApi = toxiProxy.AddExternalServiceProxy("weatherProxy", 8667, weatherApi)
    .AddLatency("slow", latency: 500, jitter: 100);
```

**Signature:**

```csharp
public static IResourceBuilder<ToxicExternalServiceResource> AddExternalServiceProxy(
    this IResourceBuilder<ToxiProxyResource> builder,
    string name,
    int port,
    IResourceBuilder<ExternalServiceResource> externalService)
```

Returns `IResourceBuilder<ToxicExternalServiceResource>`. All existing toxic
builder methods (`AddLatency`, `AddBandwidthLimit`, etc.) chain from this return
value exactly as they do for `AddHttpProxy`.

### 2.2 Wire the proxy URL into a consuming resource

```csharp
builder.AddProject<Projects.DemoApi>("demoapi")
    .WithReference(toxicWeatherApi);
```

`WithReference` on `IResourceBuilder<ToxicExternalServiceResource>` injects the
proxy's URL as service-discovery environment variables using the proxy's name:

```
services__weatherProxy__http__0 = http://localhost:8667
```

This mirrors the env-var shape that Aspire's own `WithReference(ExternalServiceResource)`
injects, so consuming code that uses `IConfiguration` / `ServiceDiscovery` needs no
changes — only the URL that gets injected changes (proxy URL instead of original URL).

---

## 3. Architecture

### 3.1 New resource type: `ToxicExternalServiceResource`

```
ToxicExternalServiceResource
  extends ToxicEndpointResource          (abstract base, already exists)
  holds   IResourceBuilder<ExternalServiceResource> TargetResource
  property Uri TargetUri  →  TargetResource.Resource.Uri!
```

Like `ToxicHttpEndpointResource`, it is added as a child to the `ToxiProxyResource`
via `_httpEndPointResources` (same list — both are HTTP-scheme proxies).

`ToxiProxyResource` stores both in the same `_httpEndPointResources` list because
the `OnResourceReady` callback only needs name, listen port, and upstream — it does
not distinguish HTTP proxies from external-service proxies.

### 3.2 Upstream construction

`ConfigureProxy` currently computes upstream as:

```csharp
var upstream = $"host.docker.internal:{targetPort}";
```

For `ToxicExternalServiceResource`, the upstream comes from the target URI:

```csharp
var uri = resource.TargetUri;                      // System.Uri
var host = NormalizeHost(uri.Host);                // localhost → host.docker.internal
var upstream = $"{host}:{uri.Port}";
```

`NormalizeHost` (new private static helper in `ToxiProxyBuilderExtensions`):

```csharp
private static string NormalizeHost(string host) =>
    host is "localhost" or "127.0.0.1" ? "host.docker.internal" : host;
```

### 3.3 `WithReference` overload

```csharp
public static IResourceBuilder<TTarget> WithReference<TTarget>(
    this IResourceBuilder<TTarget> builder,
    IResourceBuilder<ToxicExternalServiceResource> proxy)
    where TTarget : IResourceWithEnvironment
```

Injects one environment variable per the Aspire service-discovery convention:

```
services__{proxyName}__{scheme}__0 = http://localhost:{proxyPort}
```

The scheme is always `http` (ToxiProxy proxies are plain TCP; no TLS is terminated
by ToxiProxy itself). The host is `localhost` because the proxy port is exposed on
the Aspire host machine (same as `ToxicHttpEndpointResource` endpoints today).

Implementation uses `WithEnvironment(ctx => ...)` to resolve the proxy's allocated
endpoint URL from `proxy.Resource.PrimaryEndpoint.Url`.

---

## 4. Scope Boundaries

| In scope | Out of scope |
|---|---|
| Static `Uri` external services | `UrlParameter` (parameterized) external services |
| HTTP/TCP upstream (any scheme the external service uses) | TLS termination or TLS passthrough |
| Canonical `AddExternalServiceProxy` API | Low-impact `WithToxicity` API for external services |
| `WithReference` injection of proxy URL | Connection-string–style external services |
| Unit tests for new mapper path | Integration test updating snapshot |

---

## 5. Acceptance Criteria

1. **`AddExternalServiceProxy` exists** on `IResourceBuilder<ToxiProxyResource>` and
   returns `IResourceBuilder<ToxicExternalServiceResource>`.

2. **All toxic builder methods chain** from the return value (`AddLatency`,
   `AddBandwidthLimit`, `AddSlowClose`, `AddTimeout`, `AddResetPeer`, `AddSlicer`,
   `AddLimitData`, `AddPacketLoss`).

3. **Proxy is registered with ToxiProxy on startup.** `ConfigureProxy` creates the
   proxy via `POST /proxies` with:
   - `listen`: `0.0.0.0:{port}` (the declared proxy port)
   - `upstream`: `{normalizedHost}:{upstreamPort}` derived from `TargetUri`

4. **`localhost` / `127.0.0.1` are normalised** to `host.docker.internal` in the
   upstream string.

5. **Toxics are applied** via `POST /proxies/{name}/toxics` in the same order and
   format as for HTTP proxies.

6. **`WithReference` injects** `services__{name}__http__0 = http://localhost:{port}`
   into any `IResourceWithEnvironment` resource.

7. **Unit tests** cover:
   - `ToxicExternalServiceResource` upstream construction with a non-localhost URI
   - `ToxicExternalServiceResource` upstream construction with `localhost` → normalised
   - `ToxicExternalServiceResource` upstream construction with `127.0.0.1` → normalised

8. **No breaking changes** to the existing `AddHttpProxy` or `AddConnectionStringProxy`
   APIs or their behaviour.

9. **Build is green** (`dotnet build`) and all existing tests pass.

---

## 6. Open Questions (resolved)

| Question | Decision |
|---|---|
| API style | `AddExternalServiceProxy` on `ToxiProxyResource` builder (canonical style) |
| Parameterized URLs | Out of scope for this iteration |
| Localhost translation | Auto-translate `localhost` / `127.0.0.1` → `host.docker.internal` |
| Consuming-resource wiring | `WithReference` overload provided |

---

## 7. Files Affected

| File | Change |
|---|---|
| `src/Aspire.Hosting.ToxiProxy/ToxicExternalServiceResource.cs` | **New** |
| `src/Aspire.Hosting.ToxiProxy/ToxiProxyBuilderExtensions.cs` | Add `AddExternalServiceProxy`, `WithReference<TTarget>`, `NormalizeHost` |
| `src/Aspire.Hosting.ToxiProxy/ToxiProxyResource.cs` | No change (reuses `_httpEndPointResources`) |
| `test/UnitTests/ToxicExternalServiceResourceTests.cs` | **New** — upstream construction tests |
