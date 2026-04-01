# Architecture — compas_pb_csharp

**Type:** C# serialization library for COMPAS geometry data using Protocol Buffers
**Author:** Wei-Ting Chen (Gramazio Kohler Research, ETH Zurich)
**SDK:** .NET 9.0 | **Targets:** netstandard2.0, net48, net9.0

---

## Project Structure

```
compas_pb_csharp/
├── compas_pb_csharp.sln
├── Directory.Build.props / .targets     # Central MSBuild config
├── Directory.Packages.props             # Central package management
├── global.json                          # .NET SDK pin (9.0.306)
├── version.json                         # Nerdbank.GitVersioning (0.1.0)
├── fetch_compas_pb.py                   # Fetches generated C# from compas_pb releases
├── resources/
│   └── COMPAS_PB_VERSION.json           # Tracks upstream Python lib version
├── src/CompasPb/                        # Main library
│   ├── CompasPb.csproj
│   ├── PackageInfo.cs                   # Version metadata
│   ├── Data/                            # Core logic
│   │   ├── Serializer.cs               # Object -> protobuf bytes
│   │   ├── Deserializer.cs             # Protobuf bytes -> object
│   │   ├── Registry.cs                 # Auto-discovery type registry
│   │   └── Helper.cs                   # Primitive type checking
│   ├── Generated/                       # 31 protoc-generated files
│   │   ├── Message.cs                  # Envelope types (AnyData, MessageData, etc.)
│   │   ├── Point.cs, Vector.cs, ...    # Geometry primitives
│   │   ├── Mesh.cs, Box.cs, ...        # Complex geometry
│   │   └── Transformation.cs, ...      # Transformation types
│   └── Route/
│       ├── HttpClinet.cs               # HTTP transport client
│       └── HttpClient.cs.bak           # Unity-specific backup
├── test/
│   ├── CompasPb.Test.csproj
│   ├── ResgistyTest.cs                 # Registry tests
│   └── SerializerTest.cs              # Stub test
├── example/
│   ├── HttpExample/                    # HTTP client example
│   └── UserCase/                       # Serialize/deserialize example
└── .github/workflows/
    ├── build.yml                       # CI: build + format check
    └── release.yml                     # CD: publish on tag
```

---

## Layered Architecture

```
┌─────────────────────────────────────────────┐
│             Transport Layer                  │
│  RouteHttpClient (HTTP + protobuf binary)    │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│         Serialization Layer                  │
│  Serializer.PackAsBytes() <->                │
│  Deserializer.UnpackBytes()                  │
│  ┌────────────────────────────────────┐      │
│  │  Registry (type auto-discovery)    │      │
│  │  Helper (primitive type check)     │      │
│  └────────────────────────────────────┘      │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│       Generated Protobuf Types               │
│  PointData, VectorData, FrameData,           │
│  MeshData, LineData, AnyData, ListData,      │
│  DictData, MessageData, ... (31 types)       │
└─────────────────────────────────────────────┘
```

---

## Data Flow

```
C# Object (e.g., FrameData, List, Dict, int, string)
    │
    ▼
Serializer.PackAsAnyData(object) -> AnyData (polymorphic wrapping)
    │
    ▼
Serializer.PackAsBytes(AnyData) -> byte[] (MessageData envelope + version)
    │
    ▼  [RouteHttpClient.PostData() -- HTTP POST application/x-protobuf]
    │
byte[]
    │
    ▼
Deserializer.UnpackBytes(byte[]) -> AnyData (parse + version check)
    │
    ▼
Deserializer.GetType(AnyData) -> Type (via Registry lookup)
    │
    ▼
Deserializer.UnpackAnyData(AnyData) -> object (recursive for nested types)
```

---

## Design Patterns

### 1. Registry Pattern (`Registry.cs`)

A `ConcurrentDictionary<string, Type>` auto-populated via assembly reflection at static initialization. Maps protobuf type URLs to CLR types for runtime type resolution.

### 2. Static Utility / Facade Pattern (`Serializer.cs`, `Deserializer.cs`)

All public API is exposed through static methods. Acts as a facade over the protobuf serialization internals (Google.Protobuf.Any, Value, MessageParser, etc.).

### 3. Polymorphic Dispatch via Pattern Matching

`Serializer.PackAsAnyData()` uses C# pattern matching (`switch` on type) to decide serialization strategy per type. `Deserializer.UnpackAnyData()` mirrors this for deserialization.

### 4. Envelope / Wrapper Pattern (`MessageData`, `AnyData`)

`MessageData` wraps `AnyData` + version string -- acts as a transport envelope. `AnyData` uses protobuf `oneof` to hold either a typed `Message`, a primitive `Value`, or a `FallbackData`.

### 5. Code Generation Pattern

All 31 geometry types are generated externally by `protoc` from `.proto` definitions. Fetched via `fetch_compas_pb.py` from the upstream Python repo releases.

### 6. Multi-Target Compatibility Pattern

Targets `netstandard2.0`, `net48`, `net9.0` for maximum platform reach (Unity, .NET Framework, modern .NET).

---

## Pros

| Area                           | Pro                                                                                                       |
| ------------------------------ | --------------------------------------------------------------------------------------------------------- |
| **Simplicity**                 | Minimal API surface -- just `Serializer`, `Deserializer`, and `RouteHttpClient`. Easy to learn and use.   |
| **Auto-discovery**             | `Registry` automatically finds all protobuf types via reflection -- no manual registration for new types. |
| **Cross-platform**             | Multi-target build ensures Unity, .NET Framework, and modern .NET compatibility.                          |
| **Interoperability**           | Binary protobuf format enables seamless C#-Python communication with the `compas_pb` Python library.     |
| **Separation of concerns**     | Generated code is cleanly separated from hand-written logic in distinct folders.                          |
| **Central package management** | `Directory.Packages.props` ensures consistent dependency versions.                                       |
| **CI/CD**                      | Automated build, format checking, and release pipeline.                                                   |

---

## Cons

| Area                             | Con                                                                                                              |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| **No interfaces / abstractions** | All logic is in static classes -- impossible to mock for unit testing, swap implementations, or use DI.          |
| **Reflection-heavy**             | `Registry` and `Deserializer` use runtime reflection -- performance cost and harder to debug.                    |
| **No error handling strategy**   | No custom exceptions; failures during type resolution or deserialization may throw opaque errors.                |
| **Minimal test coverage**        | Only `Registry` has real tests; `SerializerTest` is a stub; tests are disabled in CI.                           |
| **Typos in filenames**           | `HttpClinet.cs`, `ResgistyTest.cs` -- impacts discoverability and professionalism.                              |
| **Broken project reference**     | `UserCase` example uses a HintPath DLL reference instead of `ProjectReference`, will break on clean builds.     |
| **No `.proto` source files**     | Generated code is fetched as a build artifact -- you can't regenerate locally or see the schema definitions.    |
| **Thread safety unclear**        | `RouteHttpClient` wraps `HttpClient` but creates a new instance per construction -- potential socket exhaustion. |
| **No async support**             | `RouteHttpClient` methods are synchronous despite HTTP I/O.                                                     |

---

## Suggestions

| Priority   | Suggestion                                                                                                                                                               |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **High**   | **Introduce interfaces** (`ISerializer`, `IDeserializer`, `ITypeRegistry`) to enable unit testing with mocks and future DI integration.                                 |
| **High**   | **Add proper unit tests** -- test Serializer round-trips for all supported types (primitives, lists, dicts, nested geometry). Enable tests in CI.                        |
| **High**   | **Fix filename typos** -- `HttpClinet.cs` -> `HttpClient.cs`, `ResgistyTest.cs` -> `RegistryTest.cs`.                                                                  |
| **High**   | **Fix UserCase project reference** -- change from HintPath DLL to `<ProjectReference>`.                                                                                 |
| **Medium** | **Add async methods** to `RouteHttpClient` (`PostDataAsync`, `GetDataAsync`) -- HTTP I/O should not block the calling thread, especially in Unity.                      |
| **Medium** | **Use `IHttpClientFactory` or singleton `HttpClient`** instead of creating new instances -- prevents socket exhaustion.                                                  |
| **Medium** | **Create custom exceptions** (`SerializationException`, `TypeResolutionException`, `VersionMismatchException`) for better error handling.                               |
| **Medium** | **Include `.proto` source files** in the repo (or as a git submodule) so the schema is visible and locally reproducible.                                                |
| **Medium** | **Add source generators** (compile-time) to replace reflection-based type discovery -- improves performance and AOT compatibility.                                       |
| **Low**    | **Add integration tests** that verify C#-Python interop by round-tripping serialized data against the Python `compas_pb` library.                                       |
| **Low**    | **Add XML doc comments** to all public APIs for IntelliSense and NuGet documentation.                                                                                   |
| **Low**    | **Add test and example projects to the solution file** for discoverability.                                                                                              |
