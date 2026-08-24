# Architecture

`compas_pb_csharp` is a **language runtime** in the `compas_pb` cross-language
serialization architecture. It implements the registry and the recursive codec
for C#, and knows nothing about any domain model.

For the full architecture — who owns what, how domain models reach multiple
languages, and what a runtime must provide — see the upstream docs:

- [Architecture](https://compas.dev/compas_pb/latest/architecture/)
- [Implementing a runtime](https://compas.dev/compas_pb/latest/implementing-a-runtime/)

---

## Entry points

The runtime contract requires one entry point in and one out. Callers pass an
object and get bytes, or pass bytes and get an object.

```
CompasPbSerializer.Pack(object)   → byte[]
CompasPbSerializer.Unpack(byte[]) → object?
CompasPbSerializer.Unpack<T>(byte[]) → T?
```

JSON equivalents: `PackAsJson` / `UnpackJson` / `UnpackJson<T>`.

All public access goes through `CompasPbSerializer` (or the `ICompasPbSerializer`
interface for DI). The internal helpers `Serializer` and `Deserializer` are not
exposed.

---

## Recursive dispatch

### Encoding (`Serializer.PackAsAnyData`)

```
serialize(obj)
  ├─ list/IEnumerable?  → list_value, recurse per item
  ├─ IDictionary?       → dict_value, recurse per value
  ├─ ICompasFallback?   → fallback (serialize its dict form)
  ├─ IMessage?          → message (Any.Pack with type_url)
  ├─ byte[]?            → value ("base64:" + encoded)
  ├─ string?            → value
  ├─ bool?              → value
  ├─ integral?          → int_value
  ├─ floating-point?    → double_value
  ├─ null?              → value (null)
  └─ otherwise          → TypeError
```

`ICompasFallback` is checked before `IMessage` so that a domain object that
implements both gets the fallback path (preserving its full dict representation).

### Decoding (`Deserializer.UnpackAnyData`)

Switches on `AnyData.DataOneofCase` — the mirror image of encoding. The
`message` arm first checks for `Any`-wrapped `ListData`/`DictData` (backward
compatibility with older payloads), then resolves via the registry.

---

## Registry and type registration

### How types are registered

`Registry` maintains a `ConcurrentDictionary<string, Type>` mapping both simple
names (`"PointData"`) and full protobuf names (`"compas_pb.data.PointData"`) to
their CLR types. At startup it scans its own assembly. Third-party assemblies
register via:

```csharp
// Pick any type from the domain package's assembly as the anchor
Registry.RegisterAssembly(typeof(ToolPathData).Assembly);
```

This scans the assembly for all `IMessage` types, registers them by both
simple and full protobuf name, and rebuilds the unpack delegate and JSON type
caches. Call it once at startup (e.g. Grasshopper plugin load, Unity `Awake()`,
or `Program.cs`). Safe to call multiple times (idempotent) and works on all
targets: .NET Standard 2.0 (Unity), .NET Framework 4.8 (Rhino/Grasshopper),
and .NET 9.

### Type URL resolution

`Registry.GetType(string typeUrl)` resolves a protobuf type URL to a CLR type:

1. Strip `type.googleapis.com/` prefix if present
2. Look up the full protobuf name (e.g. `compas_pb.data.PointData`)
3. Fall back to simple name (e.g. `PointData`) for backward compatibility

This matches on the full name after the last `/`, as required by the runtime
contract.

### The `[CompasPbRegistration]` attribute

An assembly-level marker attribute that domain packages can apply:

```csharp
[assembly: CompasPb.CompasPbRegistration]
```

Today the attribute is a convention — you must still call `RegisterAssembly`
explicitly at startup. It exists so a future version can scan for marked
assemblies automatically, without consumers needing to change their code.

### Example: using third-party domain types

Domain owners define `.proto` files in their own repos. `compas_pb`'s build
tasks generate C# bindings, and the domain owner publishes them (as a NuGet
package or release asset). The C# consumer installs the bindings and registers
the assembly -- no changes to `compas_pb_csharp` needed.

```csharp
using CompasPb;
using CompasPb.Data;
using MyDomainPackage.Data;  // from the domain package's generated C# bindings

// At startup -- register the domain package's types with the runtime
Registry.RegisterAssembly(typeof(ToolPathData).Assembly);

// Pack/Unpack now works for the domain package's types
var serializer = new CompasPbSerializer();

var toolPath = new ToolPathData
{
    Name = "milling_path_01",
    Frame = new FrameData
    {
        Point = new PointData { X = 0.0, Y = 5.0, Z = 0.0 },
        Xaxis = new VectorData { X = 1.0, Y = 0.0, Z = 0.0 },
        Yaxis = new VectorData { X = 0.0, Y = 0.0, Z = 1.0 },
    },
};

// Send to Python -- Python receives the same object
byte[] bytes = serializer.Pack(toolPath);

// Receive from Python
var received = serializer.Unpack<ToolPathData>(bytes);
```

The runtime has no knowledge of the domain package -- it just scans the
assembly for `IMessage` types and makes them available to `Pack`/`Unpack`.

For a step-by-step guide, see [Using compas_pb_csharp as a Domain Package](./DOMAIN_PACKAGE.md).

### Converter-function registry

Domain packages often have their own model types (e.g. Unity's `Vector3`,
Rhino's `Point3d`) that are not `IMessage` implementations. The converter
registry lets them register serializer/deserializer pairs so `Pack`/`Unpack`
work transparently with those types.

```csharp
// At startup -- register converters for domain types
Registry.RegisterSerializer<Plane>(plane =>
    Any.Pack(new FrameData
    {
        Point = new PointData { X = plane.Origin.X, Y = plane.Origin.Y, Z = plane.Origin.Z },
        Xaxis = new VectorData { X = plane.XAxis.X, Y = plane.XAxis.Y, Z = plane.XAxis.Z },
        Yaxis = new VectorData { X = plane.YAxis.X, Y = plane.YAxis.Y, Z = plane.YAxis.Z },
    }));

Registry.RegisterDeserializer("compas_pb.data.FrameData", any =>
{
    var frame = any.Unpack<FrameData>();
    return new Plane(
        new Point(frame.Point.X, frame.Point.Y, frame.Point.Z),
        new Vector(frame.Xaxis.X, frame.Xaxis.Y, frame.Xaxis.Z),
        new Vector(frame.Yaxis.X, frame.Yaxis.Y, frame.Yaxis.Z));
});

// Now Pack/Unpack work with Plane directly
var serializer = new CompasPbSerializer();
byte[] bytes = serializer.Pack(myPlane);       // uses the registered serializer
var plane = (Plane)serializer.Unpack(bytes);    // uses the registered deserializer
```

The serializer converts a domain object to `Any`; the deserializer converts
`Any` back to a domain object. Both are keyed so the runtime can dispatch
without knowing the domain types at compile time.

---

## Target frameworks

| Target | Typical consumers |
| --- | --- |
| `netstandard2.0` | Unity, Grasshopper, .NET Standard-compatible apps |
| `net48` | Rhino, .NET Framework 4.8 apps |
| `net9.0` | Modern .NET apps and services |

---

## Runtime contract checklist

Status of `compas_pb_csharp` against the
[runtime contract](https://compas.dev/compas_pb/latest/implementing-a-runtime/#checklist):

- [x] Version written, and checked with the compatibility key
- [x] All seven arms read and written, switching on the one that is set
- [x] Whole numbers and floats stay apart
- [x] Bytes go through the `base64:` prefix
- [x] `fallback` is written, not only read
- [x] Old `Any`-wrapped lists and dicts still decode
- [x] Type URLs matched on the full name after the last `/`
- [x] Registry holds functions, and other packages can register types.
      `RegisterAssembly` scans for `IMessage` types;
      `RegisterSerializer<T>` / `RegisterDeserializer` let domain packages
      supply converter functions for non-`IMessage` types (e.g.
      Unity `Vector3` ↔ `PointData`).
- [x] One entry point each way, with no type checking left to callers
- [x] Bindings come from a pinned release, not copied into the repo
- [x] Shared `compas_pb` types come from the runtime package
- [x] Tested both directions against bytes Python produced
