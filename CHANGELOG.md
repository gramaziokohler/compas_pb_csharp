# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

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
