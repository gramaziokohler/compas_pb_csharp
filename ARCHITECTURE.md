# Architecture

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
