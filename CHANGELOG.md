# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Breaking Changes

- Upgrade the bundled `compas_pb` schema and generated C# bindings from 1.0 to 1.1
- Serialize integers, floating-point values, dictionaries, and lists through their dedicated `AnyData` fields

### Features

- Add byte-array serialization using the compas_pb base64 representation
- Add `ICompasFallback` support for interoperable fallback objects such as `compas_model` models
- Preserve deserialization compatibility with legacy dictionaries and lists packed inside protobuf `Any` values
- Add function-based downstream type registration with inheritance-aware serializer lookup
- Add registered fallback reconstruction by COMPAS `dtype`
- Add loaded-assembly discovery for external protobuf messages

### Tests

- Add compas_pb version compatibility coverage
- Add a Python-generated `compas_model` fixture to verify Python-to-C# wire interoperability

### Development

- Remove the pinned .NET SDK so supported installed SDKs can be selected through roll-forward behavior
- Fetch the exact pinned C# binding asset from the official `compas-dev/compas_pb` release and verify its checksum
- Preserve the packed NuGet and symbol packages as release workflow artifacts

### Bug Fixes

- Match `Any` type URLs by their full protobuf name after the last slash instead of by collision-prone C# class name
- Remove duplicate README inclusion from the NuGet package build
- Fix the build and documentation links embedded in the NuGet README

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
