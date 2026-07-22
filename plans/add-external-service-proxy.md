# Plan: Add External Service Proxy

**Created**: 2026-07-22
**Branch**: main
**Status**: approved
**Gherkin persistence**: features

## Goal

Add `AddExternalServiceProxy` to the `ToxiProxyResource` builder so that an Aspire
`ExternalServiceResource` (a static-URI external dependency) can be wrapped by
ToxiProxy and have toxics applied to it. A companion `WithReference` overload
injects the proxy URL into consuming resources using Aspire's service-discovery
env-var convention — under the original external service's name — so consuming code
needs no configuration changes; only the URL changes (proxy URL instead of original).

## Acceptance Criteria

- [ ] `AddExternalServiceProxy(name, port, externalServiceBuilder)` exists on
      `IResourceBuilder<ToxiProxyResource>` and returns
      `IResourceBuilder<ToxicExternalServiceResource>`
- [ ] All toxic builder methods (`AddLatency`, `AddBandwidthLimit`, `AddSlowClose`,
      `AddTimeout`, `AddResetPeer`, `AddSlicer`, `AddLimitData`, `AddPacketLoss`)
      chain from the return value and return the same builder instance
- [ ] On ToxiProxy startup `POST /proxies` is called with `upstream` equal to
      `"{NormalizedHost}:{port}"` where NormalizedHost comes from `TargetUri.Host`
      and port from `TargetUri.Port`
- [ ] `localhost` and `127.0.0.1` in the target URI are normalised to
      `host.docker.internal` in the upstream string
- [ ] Toxics are applied via `POST /proxies/{name}/toxics` in the startup loop
- [ ] `WithReference(toxicProxy)` injects
      `services__{externalServiceName}__http__0 = http://localhost:{port}` where
      `externalServiceName` is the name of the underlying `ExternalServiceResource`
- [ ] Constructing `ToxicExternalServiceResource` with a null-URI `ExternalServiceResource`
      throws `InvalidOperationException` immediately at build time
- [ ] Unit tests cover: external hostname unchanged, `localhost` →
      `host.docker.internal`, `127.0.0.1` → `host.docker.internal`, null URI guard,
      collection registration
- [ ] No breaking changes to `AddHttpProxy` or `AddConnectionStringProxy`
- [ ] `dotnet build` and all existing tests pass

## Slices

### Slice 1: ToxicExternalServiceResource class and host normalization

**Depends-on:** none
**Files:** `src/Aspire.Hosting.ToxiProxy/ToxicExternalServiceResource.cs`,
           `src/Aspire.Hosting.ToxiProxy/ToxiProxyBuilderExtensions.cs`,
           `test/UnitTests/ToxicExternalServiceResourceTests.cs`

**Steps:**

#### Step 1.1: Create ToxicExternalServiceResource + NormalizeHost

**Complexity**: standard
**IMPLEMENT**: New class `ToxicExternalServiceResource` extending `ToxicEndpointResource`,
implementing `IResourceWithEndpoints, IResourceWithWaitSupport`. Eager null-URI validation
in constructor. `TargetUri` non-nullable. `internal static string NormalizeHost(string host)`
in `ToxiProxyBuilderExtensions`.
**TEST**: 5 unit tests — TargetUri, null guard, NormalizeHost × 3.
**REFACTOR**: XML doc comments; naming consistent with `ToxicHttpEndpointResource`.
**Files**: `src/Aspire.Hosting.ToxiProxy/ToxicExternalServiceResource.cs`,
           `src/Aspire.Hosting.ToxiProxy/ToxiProxyBuilderExtensions.cs`,
           `test/UnitTests/ToxicExternalServiceResourceTests.cs`
**Commit**: `feat: add ToxicExternalServiceResource and NormalizeHost helper`

---

### Slice 2: Builder pipeline wiring

**Depends-on:** 1
**Files:** `src/Aspire.Hosting.ToxiProxy/ToxiProxyResource.cs`,
           `src/Aspire.Hosting.ToxiProxy/ToxiProxyBuilderExtensions.cs`

**Steps:**

#### Step 2.1: Add external service list to ToxiProxyResource

**Complexity**: standard
**IMPLEMENT**: `_externalServiceResources` list, `ExternalServiceResources` property,
`AddExternalServiceProxy(ToxicExternalServiceResource)` with `EndpointAnnotation`.
**TEST**: Unit test `AddExternalServiceProxy_adds_to_collection`.
**REFACTOR**: Consistent with sibling members.
**Files**: `src/Aspire.Hosting.ToxiProxy/ToxiProxyResource.cs`
**Commit**: `feat: add external service proxy list to ToxiProxyResource`

#### Step 2.2: Extension method, ConfigureProxy refactor, and WithReference

**Complexity**: standard
**IMPLEMENT**:
(a) Refactor `ConfigureProxy` to take `string upstream` instead of `int targetPort`.
(b) Third `OnResourceReady` loop for `ExternalServiceResources`.
(c) `AddExternalServiceProxy` extension method with health check.
(d) `WithReference<TDestination>` overload using underlying service name.
**TEST**: Build green; all unit tests pass; `dotnet build test/AppHost` smoke test.
**REFACTOR**: XML doc comments on new public API.
**Files**: `src/Aspire.Hosting.ToxiProxy/ToxiProxyBuilderExtensions.cs`
**Commit**: `feat: wire AddExternalServiceProxy into builder pipeline with WithReference`

## Parallelization

| Wave | Slices (parallel) |
|------|-------------------|
| 1 | 1 |
| 2 | 2 |

## Pre-PR Quality Gate

- [ ] All tests pass (`dotnet test test/UnitTests`)
- [ ] Build passes (`dotnet build`)
- [ ] `dotnet build test/AppHost` passes
- [ ] XML doc comments on all new public API surface

## Skipped (low value)

| Finding | Rationale |
|---|---|
| Integration test snapshot update | Requires live ToxiProxy container; deferred in spec |
| `UrlParameter` external service support | Out of scope per spec decision |
| Unit test for `ConfigureProxy` upstream HTTP call | No HTTP mock pattern in codebase |

## Risks & Open Questions

- `ConfigureProxy` signature refactor touches 2 existing call sites — mechanical but warrants care.
- No `IResourceWithUri` in Aspire — `TargetResource` typed concretely as `IResourceBuilder<ExternalServiceResource>`.

## Build Progress

### Slices (grouped by wave)

#### Wave 1
- [ ] Slice 1: ToxicExternalServiceResource class and host normalization
  - [ ] Step 1.1: Create ToxicExternalServiceResource and NormalizeHost

#### Wave 2
- [ ] Slice 2: Builder pipeline wiring
  - [ ] Step 2.1: Add external service list to ToxiProxyResource
  - [ ] Step 2.2: Extension method, ConfigureProxy refactor, and WithReference
