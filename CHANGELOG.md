# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Breaking Changes

- Upgrade the bundled `compas_pb` schema and generated C# bindings from 1.0 to 1.1
- Serialize integers, floating-point values, dictionaries, and lists through their dedicated `AnyData` fields
- Make `Serializer` and `Deserializer` `internal`; consumers must use the public `ICompasPbSerializer` API
- Move `ICompasPbSerializer` and `CompasPbSerializer` from `CompasPb.Data` to the `CompasPb` namespace
- Add typed `SendAsync` / `ReceiveAsync<T>` to HTTP client

### Features

- Add `ICompasPbSerializer` interface and `CompasPbSerializer` implementation with typed `Unpack<T>` and DI support
- Add byte-array serialization using the compas_pb base64 representation
- Add `ICompasFallback` support for interoperable fallback objects such as `compas_model` models
- Preserve deserialization compatibility with legacy dictionaries and lists packed inside protobuf `Any` values
- Add JSON serialization via `PackAsJson` and `UnpackJson` / `UnpackJson<T>` on `ICompasPbSerializer`, with input validation that rejects null, empty, or whitespace strings

### Refactor

- Dispatch `UnpackAnyData` via `DataOneofCase` enum (compile-time exhaustive)
- Replace per-call `MakeGenericMethod` with startup delegate cache in `Registry`
- Rename `HttpClinet` → `CompasPbHttpClient`

### Bug Fixes

- Embed `COMPAS_PB_VERSION.json` as assembly resource — fixes version returning `unknown` in Grasshopper/Unity

### Tests

- Add compas_pb version compatibility coverage
- Add a Python-generated `compas_model` fixture to verify Python-to-C# wire interoperability

### Documentation

- Update README with all three usage patterns and HTTP transport example
- Update README and ARCHITECTURE with JSON serialization usage and design notes
- Update ARCHITECTURE.md to reflect new design

### CI/CD

- Add csharpier format check and `dotnet test` to build and release workflows

### Development

- Remove the pinned .NET SDK so supported installed SDKs can be selected through roll-forward behavior
- Remove unused `Data/Helper.cs`
