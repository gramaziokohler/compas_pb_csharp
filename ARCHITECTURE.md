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
│   │   ├── ICompasPbSerializer.cs      # Public interface (DI contract)
│   │   ├── CompasPbSerializer.cs       # Implementation of ICompasPbSerializer
│   │   ├── Serializer.cs               # Static facade — object -> protobuf bytes
│   │   ├── Deserializer.cs             # Static facade — protobuf bytes -> object
│   │   ├── Registry.cs                 # Auto-discovery + delegate cache
│   │   └── Helper.cs                   # Primitive type checking
│   ├── Generated/                       # 31 protoc-generated files (fetched by fetch_compas_pb.py)
│   │   ├── Message.cs                  # Envelope types (AnyData, MessageData, etc.)
│   │   ├── Point.cs, Vector.cs, ...    # Geometry primitives
│   │   ├── Mesh.cs, Box.cs, ...        # Complex geometry
│   │   └── Transformation.cs, ...      # Transformation types
│   └── Route/
│       └── CompasPbHttpClient.cs       # HTTP transport client
├── test/
│   ├── CompasPb.Test.csproj
│   ├── RegistryTest.cs                 # Registry tests
│   └── SerializerTest.cs              # Round-trip tests for all supported types
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
┌─────────────────────────────────────────────────────────────┐
│                      User Type A                            │
│         Grasshopper / Unity / simple callers                │
│   static Serializer.Pack()  /  static Deserializer.Unpack() │
│         (thin facades — delegate to CompasPbSerializer)      │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                      User Type B                            │
│              new CompasPbSerializer()                       │
│         .Pack(obj)  /  .Unpack<T>(bytes)                    │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                      User Type C                            │
│              ICompasPbSerializer (injected via DI)          │
│         .Pack(obj)  /  .Unpack<T>(bytes)                    │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│              CompasPbSerializer (implementation)            │
│   Pack(object?) → AnyData → MessageData → byte[]           │
│   Unpack(byte[]) → MessageData → AnyData → object?         │
│   Unpack<T>(byte[]) → MessageData → AnyData → T?           │
└──────────┬─────────────────────────────────────┬───────────┘
           │                                     │
┌──────────▼──────────┐             ┌────────────▼────────────┐
│   Serializer.cs     │             │    Deserializer.cs      │
│   (static internal) │             │    (static internal)    │
│                     │             │                         │
│  PackAsAnyData()    │             │  UnpackBytes()          │
│  PackPrimitive()    │             │  UnpackAnyData()        │
│  PackList()         │             │  UnpackMessage()        │
│  PackDict()         │             │  UnpackPrimitive()      │
└─────────────────────┘             └────────────┬────────────┘
                                                 │
                                    ┌────────────▼────────────┐
                                    │        Registry         │
                                    │                         │
                                    │  Auto-scan at startup   │
                                    │  Delegate cache (once)  │
                                    │  O(1) UnpackAs dispatch │
                                    └─────────────────────────┘
```

---

## Public API — Three Ways to Use

### User Type A — Static (Grasshopper / Unity, no DI)

```csharp
// Pack
byte[] bytes = Serializer.PackAsBytes(Serializer.PackAsAnyData(myFrame));

// Unpack — dynamic, returns object?
AnyData anyData = Deserializer.UnpackBytes(bytes);
object? result  = Deserializer.UnpackAnyData(anyData);
var frame       = result as FrameData;
```

### User Type B — Instance (knows the type, wants typed API)

```csharp
var serializer = new CompasPbSerializer();

byte[] bytes      = serializer.Pack(myFrame);
FrameData? frame  = serializer.Unpack<FrameData>(bytes);   // no cast needed
object?   dynamic = serializer.Unpack(bytes);              // dynamic path still available
```

### User Type C — Injected (ASP.NET, larger apps, testable)

```csharp
// Registration
services.AddSingleton<ICompasPbSerializer, CompasPbSerializer>();

// Consumer
public class MyService(ICompasPbSerializer serializer)
{
    public void Handle(byte[] incoming)
    {
        FrameData? frame = serializer.Unpack<FrameData>(incoming);
    }
}
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
    ▼  [CompasPbHttpClient.SendAsync() -- HTTP POST application/x-protobuf]
    │
byte[]
    │
    ▼
Deserializer.UnpackBytes(byte[]) -> AnyData (parse + version check)
    │
    ├── typed path:   Deserializer.Unpack<T>(AnyData) -> T?      [no reflection]
    │
    └── dynamic path: Deserializer.UnpackAnyData(AnyData) -> object?
                          └── Registry.UnpackAs(any, targetType) [delegate cache, O(1)]
```

---

## Design Patterns

### 1. Interface + Implementation (`ICompasPbSerializer` / `CompasPbSerializer`)

Public contract is an interface — enables DI, mocking, and multiple implementations. `CompasPbSerializer` is the single concrete implementation.

### 2. Static Facade Pattern (`Serializer.cs`, `Deserializer.cs`)

Static methods remain as a convenience facade over `CompasPbSerializer`. No behavior change for existing callers.

### 3. Delegate Cache in Registry (`Registry.cs`)

At startup, reflection scans all `IMessage` types and builds a `Dictionary<Type, Func<Any, object?>>`. Per-call dispatch is O(1) with no reflection. Replaces `MakeGenericMethod` which was called once per deserialization.

### 4. Polymorphic Dispatch via Pattern Matching (`Deserializer.cs`)

`UnpackAnyData()` uses `DataOneofCase` enum switch for the top-level dispatch. The typed `Unpack<T>` path uses a generic constraint — the compiler resolves `T`, no runtime reflection.

### 5. Envelope / Wrapper Pattern (`MessageData`, `AnyData`)

`MessageData` wraps `AnyData` + version string. `AnyData` uses protobuf `oneof` to hold either a typed `Message`, a primitive `Value`, or a `FallbackData`.

### 6. Code Generation Pattern (external)

All 31 geometry types are generated externally by `protoc`. Fetched via `fetch_compas_pb.py` from upstream Python repo releases. `fetch_compas_pb.py` is unchanged — no generated dispatch files.

### 7. Multi-Target Compatibility

Targets `netstandard2.0`, `net48`, `net9.0` for Unity, .NET Framework, and modern .NET.

---

## Pros

| Area | Pro |
|---|---|
| **Flexibility** | Three usage modes (static, instance, DI) — same implementation |
| **Typed API** | `Unpack<T>()` eliminates manual casting and runtime reflection in the hot path |
| **DI-friendly** | `ICompasPbSerializer` enables injection, mocking, and testing |
| **Backward compatible** | Static `Serializer`/`Deserializer` API unchanged |
| **Auto-discovery** | `Registry` still auto-discovers all protobuf types — no manual registration |
| **Performance** | Delegate cache replaces per-call `MakeGenericMethod` |
| **Cross-platform** | Multi-target build for Unity, .NET Framework, modern .NET |
| **Interoperability** | Binary protobuf format for C#-Python communication with `compas_pb` |

---

## Cons

| Area | Con |
|---|---|
| **No error handling strategy** | No custom exceptions; version mismatch is a warning only |
| **Minimal test coverage** | `SerializerTest` needs real round-trip tests |
| **No async on static facade** | Static `Serializer`/`Deserializer` are synchronous |
| **No `.proto` source files** | Schema not visible locally; fetched as build artifact |

---

## Suggestions

| Priority | Suggestion |
|---|---|
| **High** | Add round-trip tests in `SerializerTest.cs` for all supported types |
| **Medium** | Add `CompasPbHttpClient.SendAsync<T>` / `ReceiveAsync<T>` typed overloads |
| **Medium** | Add `services.AddCompasPb()` extension method for ASP.NET DI registration |
| **Medium** | Add custom exceptions (`SerializationException`, `VersionMismatchException`) |
| **Low** | Add XML doc comments to all public APIs |
| **Low** | Include `.proto` source files as a git submodule |
