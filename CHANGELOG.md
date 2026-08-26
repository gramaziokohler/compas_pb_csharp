# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Breaking Changes

- Upgrade the bundled `compas_pb` schema and generated C# bindings from 1.0 to 1.2
- Serialize integers, floating-point values, dictionaries, and lists through their dedicated `AnyData` fields
- Move `CompasPbSerializer` and `ICompasPbSerializer` from `CompasPb.Data` to the `CompasPb` namespace; `Registry`, `ICompasFallback` and `CompasPbRegistrations` stay in `CompasPb.Data`
- Make `Serializer` and `Deserializer` internal, leaving `CompasPbSerializer` as the single entry point in each direction
- Remove `CompasPbHttpClient` and the `CompasPb.Route` namespace, along with the `System.Net.Http` dependency that only supported it
- Remove the unused `CompasPb.Data.Helper` class

### Features

- Add byte-array serialization using the compas_pb base64 representation
- Add `ICompasFallback` support for interoperable fallback objects such as `compas_model` models
- Preserve deserialization compatibility with legacy dictionaries and lists packed inside protobuf `Any` values
- Add function-based downstream type registration with inheritance-aware serializer lookup
- Add registered fallback reconstruction by COMPAS `dtype`
- Add loaded-assembly discovery for external protobuf messages
- Add `CompasPbRegistrations` assembly attribute so a package's conversions register by being referenced instead of from application startup
- Add the `dev.compas.compas-pb` Unity Package Manager distribution for publication to OpenUPM
- Add JSON serialization with `PackAsJson`, `UnpackJson` and `UnpackJson<T>`, implementing upstream `pb_dump_json` / `pb_load_json` over the same envelope
- Add `PackAsAnyData` and `UnpackAnyData` so a domain package can fill the `AnyData` fields of its own messages, as `MeshData.edge_keys` and `AttributeColumn.values` require

### Tests

- Add compas_pb version compatibility coverage
- Add a Python-generated `compas_model` fixture to verify Python-to-C# wire interoperability
- Add a Python-generated JSON fixture covering the `pb_dump_json` shape, including oneof fields at their default values
- Add a `.proto` fixture that stands in for a third-party domain package, so registration is tested against an out-of-tree message
- Run the `net48` suite, which reported "No test is available" because the hand-copied VSTest adapter overwrote the `net462` build with the `net6.0` one

### Development

- Remove the pinned .NET SDK so supported installed SDKs can be selected through roll-forward behavior
- Fetch the exact pinned C# binding asset from the official `compas-dev/compas_pb` release and verify its checksum
- Preserve the packed NuGet and symbol packages as release workflow artifacts
- Add `build_upm.py` to compile, stage, and validate the Unity package
- Replace the hand-rolled release script and changelog-parsing trigger with release-please
- Publish the Unity package to a dedicated `upm` branch tagged `upm/vX.Y.Z` so compiled assemblies stay out of `main`
- Build the example project from a `ProjectReference` instead of a placeholder DLL path, and build it in CI, so a breaking API change fails the build
- Add the test and example projects to the solution so a root `dotnet build` and `dotnet test` cover them

### Bug Fixes

- Match `Any` type URLs by their full protobuf name after the last slash instead of by collision-prone C# class name
- Remove duplicate README inclusion from the NuGet package build
- Fix the build and documentation links embedded in the NuGet README
- Add the missing root `LICENSE` file that the README license badge links to
- Point the Unity git-URL install instructions at the `upm/vX.Y.Z` tags; the documented `main` subfolder URL produced a package with no assemblies

## [0.1.0] - 2026-08-09

### Features

- Add `ICompasPbSerializer` interface and `CompasPbSerializer` implementation with typed `Unpack<T>` and DI support
- Add typed `SendAsync` / `ReceiveAsync<T>` to HTTP client

### Refactor

- Dispatch `UnpackAnyData` via `DataOneofCase` enum (compile-time exhaustive)
- Replace per-call `MakeGenericMethod` with startup delegate cache in `Registry`
- Rename `HttpClinet` → `CompasPbHttpClient`

### Bug Fixes

- Embed `COMPAS_PB_VERSION.json` as assembly resource — fixes version returning `unknown` in Grasshopper/Unity

### CI/CD

- Add csharpier format check and `dotnet test` to build and release workflows

### Documentation

- Update README with all three usage patterns and HTTP transport example
- Update ARCHITECTURE.md to reflect new design
