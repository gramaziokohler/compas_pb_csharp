# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0](https://github.com/gramaziokohler/compas_pb_csharp/compare/v0.1.0...v1.0.0) (2026-08-31)


### ⚠ BREAKING CHANGES

* `CompasPb.Route.CompasPbHttpClient` is removed, along with the `CompasPb.Route` namespace. CompasPb has never been published to NuGet.org, so no released consumer is affected. Callers needing HTTP should own the transport and use `CompasPbSerializer.Pack` / `Unpack` on the payload.
* `CompasPbSerializer` and `ICompasPbSerializer` moved from the `CompasPb.Data` namespace to `CompasPb`. Consumers add `using CompasPb;` alongside their existing `using CompasPb.Data;` for the message types. `Serializer` and `Deserializer` are no longer public; call `CompasPbSerializer` instead. The unused `CompasPb.Data.Helper` class is removed. `Registry`, `ICompasFallback` and `CompasPbRegistrationsAttribute` stay in `CompasPb.Data`.
* releases are now driven by Conventional Commits. The manual "run the release workflow with a bump type" flow is gone.

### Features

* add [CompasPbRegistration] assembly marker attribute ([c850e39](https://github.com/gramaziokohler/compas_pb_csharp/commit/c850e3911f3442f4b5ebc346efb03e5add5591cd))
* add api layer and unpack pack as jsonstring ([11ebed8](https://github.com/gramaziokohler/compas_pb_csharp/commit/11ebed8209e61a0316b76e1f31ce3ff240e93dc5))
* add ICompasPbSerializer interface and CompasPbSerializer implementation with round-trip tests ([9abff3d](https://github.com/gramaziokohler/compas_pb_csharp/commit/9abff3d82bdb0c419cfba9152e9f8753ffc8772b))
* add public Registry.RegisterAssembly for third-party type registration ([cf19f52](https://github.com/gramaziokohler/compas_pb_csharp/commit/cf19f52accb7851f36431d449b7f64a7141bbe3a))
* add the Unity package for OpenUPM publication ([0cb5c64](https://github.com/gramaziokohler/compas_pb_csharp/commit/0cb5c6414e398a7f074a07777f4767f84f4305a0))
* adopt release-please for versioning and releases ([c5b8dcb](https://github.com/gramaziokohler/compas_pb_csharp/commit/c5b8dcb1b6235f946a5e6ae8534603a439e4101c))
* discover conversion registrations from assembly attributes ([ba0222f](https://github.com/gramaziokohler/compas_pb_csharp/commit/ba0222fcd06a76e17adbecbe9224579df1bc5c2e))
* drop CompasPbHttpClient ([44970cb](https://github.com/gramaziokohler/compas_pb_csharp/commit/44970cbc2cc24fdf088d3156fdf2a083b17aa3c9))
* expose AnyData-level conversion on the serializer ([147be7b](https://github.com/gramaziokohler/compas_pb_csharp/commit/147be7ba8223acd8a2497ab31b5746545c65daa6))
* implement the extensible compas_pb runtime registry ([f69a819](https://github.com/gramaziokohler/compas_pb_csharp/commit/f69a8191ea105d5cbb879c19a58b2c80c28ed5f1))
* merge the internal serializer/deserializer branch ([d2a1374](https://github.com/gramaziokohler/compas_pb_csharp/commit/d2a13740937a4a895a1f7fc9b5fa65082a8d3f9d))
* upgrade the pinned compas_pb release from 1.1.0 to 1.2.0 ([a7be241](https://github.com/gramaziokohler/compas_pb_csharp/commit/a7be2415cdf877d77cd8057a8dfc291ccd852c41))


### Bug Fixes

* add net48 binding redirects so the Windows test run loads ([9b64cb4](https://github.com/gramaziokohler/compas_pb_csharp/commit/9b64cb47287ad3c1c3e773930d03671ef290d89a))
* add the missing MIT license and align the package copyright ([db621fa](https://github.com/gramaziokohler/compas_pb_csharp/commit/db621fa3585a7ab6af8ed0bad09755cf483658b0))
* better error handle ([02fb955](https://github.com/gramaziokohler/compas_pb_csharp/commit/02fb955fcd3fd35245ffb2fd59824eaa971f5fcc))
* csharpier version ([74a2caa](https://github.com/gramaziokohler/compas_pb_csharp/commit/74a2caa834013bbf1ab0dc3a50cb5aee1c7731af))
* discover registrations from assemblies that load later ([b09c888](https://github.com/gramaziokohler/compas_pb_csharp/commit/b09c888ccca6288b03044e32fa853e7d9feac110))
* embed COMPAS_PB_VERSION.json as assembly resource instead of reading from working directory ([a925d3c](https://github.com/gramaziokohler/compas_pb_csharp/commit/a925d3c9e6d2634e3040221b34da9379398949ab))
* keep one broken registrar from stopping registration discovery ([4db2ef2](https://github.com/gramaziokohler/compas_pb_csharp/commit/4db2ef2a8eaeeb0d88b6caaebd46d5753d6b883f))
* match type URLs on full protobuf name after last / ([3c12d41](https://github.com/gramaziokohler/compas_pb_csharp/commit/3c12d418bf1c7e4259f946a3922989aa2703ea14))
* move the (de)serlizer in to internal ([39cb4ff](https://github.com/gramaziokohler/compas_pb_csharp/commit/39cb4ff36a3392461753b709b32d18223d7591c6))
* package the build ([c87a3db](https://github.com/gramaziokohler/compas_pb_csharp/commit/c87a3db11d3a9930b98a9940f29000cd2d5578c9))
* run the net48 tests and build the example project ([059b37f](https://github.com/gramaziokohler/compas_pb_csharp/commit/059b37f94bc9fce5e8034fde33a4c6792ec01e12))
* use csharpier global tool correctly in CI ([7edc889](https://github.com/gramaziokohler/compas_pb_csharp/commit/7edc8890e3b8da30ccbe4952611ec53b54dd4be0))

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
