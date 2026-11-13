# Changelog

All notable changes to Relay are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `net11.0` target alongside `net10.0`. The package and tests now build and run on both.

## [2.1.0]

### Added

- **Circuit breaker for multi-relays**: `WithCircuitBreaker(failureThreshold, breakDuration)` stops
  calling a relay that failed repeatedly, skipping it until the break elapses and then allowing a
  single trial call. Applies to the `Failover` and `FirstSuccessful` strategies, composes with
  `WithRetry`, and is driven by `TimeProvider` so it is deterministic in tests.
  `MultiRelay<T>.GetCircuitSnapshots()` exposes live circuit state for health endpoints.
- **Metrics**: adapter chains, multi-relay operations and circuit breaker transitions are now
  published on a `Meter` named `Relay` (`RelayDiagnostics.MeterName`), alongside the existing
  traces. Seven instruments cover execution counts, durations, circuit transitions, the number of
  currently open circuits and relays skipped by an open circuit. Wire them up with
  `AddMeter(RelayDiagnostics.MeterName)`.

## [2.0.0]

### Added

- **Container validation** (`Relay.Validation`): `ValidateRelayRegistrations()` and
  `ValidateRelayRegistrationsOrThrow()` statically analyse an `IServiceCollection` and report
  captive dependencies, missing registrations, singletons capturing disposable transients and
  duplicate registrations. Keyed registrations, open generics, `IEnumerable<T>`, `Lazy<T>` and
  optional constructor parameters are all understood.
- **`IRelayScopeRunner`**: run work in a fresh DI scope from a singleton, the standard fix for
  "cannot consume scoped service from singleton". Registered by `AddRelayServices()`, or on its
  own with `AddRelayScopeRunner()`.
- **Lazy resolution**: `AddLazyResolution()` enables `Lazy<T>` for every registered service (and
  `AddLazyResolution<T>()` for a single one), deferring expensive construction and breaking
  constructor cycles.
- **Decorator API**: `Decorate<TInterface, TDecorator>()`, `TryDecorate(...)` and
  `DecorateKeyed(...)` for keyed services.
- **Trimming and native AOT support**: the package is `IsAotCompatible`/`IsTrimmable` with full
  trim annotations. Assembly-scanning entry points are annotated `[RequiresUnreferencedCode]`.
- `MultiRelay` accepts a `TimeProvider`, so retry backoff is deterministic in tests. A registered
  `TimeProvider` is picked up automatically by `AddMultiRelay`.

### Changed

- **Breaking:** `IMultiRelay<T>.RelayToAll` and `RelayToAllWithResults` take an optional
  `CancellationToken`. Call sites are unaffected; custom implementations of the interface must be
  updated.
- **Breaking:** `Decorate<TInterface>(...)` now decorates *every* non-keyed registration of the
  service instead of only the last one, and no longer matches keyed descriptors (which previously
  threw when their implementation type was read).
- Adapter chains execute with strongly-typed invokers created at registration time: no reflection,
  no `MethodInfo.Invoke`, no `MakeGenericType`. Chains built by hand still fall back to reflection.
- `AddRelayServices()` is idempotent — calling it twice no longer duplicates registrations.
- Adapter registrations inside chains use `TryAdd`, so reusing an adapter across steps registers it
  once.
- Assembly scanning survives partially loadable assemblies instead of throwing
  `ReflectionTypeLoadException`.
- `DefaultRelayContext` reads `DOTNET_ENVIRONMENT` when `ASPNETCORE_ENVIRONMENT` is not set.
- Failover now reports every relay failure through an `AggregateException`, and the `Parallel`
  strategy surfaces all failures rather than just the first.
- Packages are produced into `artifacts/packages` and are no longer generated on every build.

## [1.2.0]

- .NET 10 upgrade, async adapter chains, context-aware routing, keyed DI, `ActivitySource` tracing.
