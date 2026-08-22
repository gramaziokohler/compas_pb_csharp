# Architecture

`compas_pb_csharp` is a **language runtime** in the `compas_pb` cross-language
serialization architecture. It implements the registry and the recursive codec
for C#, and knows nothing about any domain model.

For the full architecture — who owns what, how domain models reach multiple
languages, and what a runtime must provide — see the upstream docs:

- [Architecture](https://github.com/gramaziokohler/compas_pb/blob/main/docs/architecture.md)
- [Implementing a runtime](https://github.com/gramaziokohler/compas_pb/blob/main/docs/implementing-a-runtime.md)

This document covers how `compas_pb_csharp` fulfils that contract.

---

## Two kinds of package

> **Read first:**
> [compas_pb Architecture](https://github.com/gramaziokohler/compas_pb/blob/main/docs/architecture.md)
> explains the full ecosystem design. This section summarizes how `compas_pb_csharp`
> fits into it.

Every package in the `compas_pb` ecosystem is one of two things:

| | Domain model owner | Language runtime |
| --- | --- | --- |
| **Role** | Defines data types for a problem domain | Provides the registry and codec for one language |
| **Owns** | `.proto` files and the domain classes they mirror | registry, discovery, recursive codec |
| **Publishes** | proto bundle + generated bindings, every release | a serialization library for one language |
| **Knows about** | its own types only | no domain types at all |

Concrete examples:

| Package | Role | What it owns |
| --- | --- | --- |
| `compas_pb` (Python) | **Both** (runtime + domain owner) | COMPAS core types (`Point`, `Frame`, `Mesh`, ...) + Python codec |
| **`compas_pb_csharp`** | **Runtime only** | C# registry and codec, no domain types |
| `compas_pb_ts` | **Runtime only** | TypeScript registry and codec |
| `antikythera` | **Domain owner only** | Robot task planning types |
| `compas_timber` | **Domain owner only** | Timber construction types |

`compas_pb` is both a runtime and a domain owner because the COMPAS core types
(`Point`, `Frame`, `Mesh`) have not been upstreamed into `compas` core yet.
Every other package is cleanly one or the other.

This runtime never imports a domain package, and a domain package never
implements codec logic. They meet at the registry.

---

## The wire format

Every message is a `MessageData` envelope: a version string plus one `AnyData`.
`AnyData` is a protobuf `oneof` — exactly one arm is set.

| Arm | Holds | Notes |
| --- | --- | --- |
| `message` | `google.protobuf.Any` | A registered type. Older payloads also wrap lists/dicts here |
| `value` | `google.protobuf.Value` | null, bool, string. Bytes travel as `"base64:<encoded>"` |
| `fallback` | `FallbackData` | The only arm that reconstructs a domain object |
| `int_value` | `int64` | Whole numbers (avoids `Value`'s double coercion) |
| `double_value` | `double` | Floats, even when the value is integral like `3.0` |
| `dict_value` | `DictData` | Recurses into `AnyData` per value |
| `list_value` | `ListData` | Recurses into `AnyData` per item |

### Version compatibility

The version is checked before trusting anything else. Compatibility keys:
under `0.x`, the key is `MAJOR.MINOR`; from `1.0` onwards, just `MAJOR`.
So `1.0.0` can read `1.2.9`. No version at all is an error.

See `Deserializer.ValidateVersion` and `Deserializer.WireCompatibilityKey`.

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
// In the domain package's AssemblyInfo.cs or any top-level file
[assembly: CompasPb.CompasPbRegistration]

// In the domain package's startup (e.g. Grasshopper GH_AssemblyInfo)
public override void OnLoadAssembly(GH_LoadingInstruction instruction)
{
    Registry.RegisterAssembly(typeof(ToolPathData).Assembly);
}
```

Today the attribute is a convention — you must still call `RegisterAssembly`
explicitly. It exists so a future version can scan for marked assemblies
automatically, without consumers needing to change their startup code.

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

### Future direction

The runtime contract recommends storing **functions** (serializer/deserializer
pairs) rather than requiring a class shape. This would allow domain packages to
register conversion functions between their own model types and protobuf messages,
via attributes like `[PbSerializer(typeof(Frame), typeof(FrameData))]`. This is
not implemented yet — the current registry works with `IMessage` types directly.

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
[runtime contract](https://github.com/gramaziokohler/compas_pb/blob/main/docs/implementing-a-runtime.md#checklist):

- [x] Version written, and checked with the compatibility key
- [x] All seven arms read and written, switching on the one that is set
- [x] Whole numbers and floats stay apart
- [x] Bytes go through the `base64:` prefix
- [x] `fallback` is written, not only read
- [x] Old `Any`-wrapped lists and dicts still decode
- [x] Type URLs matched on the full name after the last `/`
- [x] Registry holds functions, and other packages can register types.
      The registry stores `Func<Any, object?>` delegates internally and
      exposes `RegisterAssembly` for third-party registration. Full
      converter-function registration (where domain packages supply their
      own serializer/deserializer pairs for non-`IMessage` types) is not
      yet needed — all C# domain types are currently generated `IMessage`
      implementations. This will be added when a consumer with a separate
      domain model layer (analogous to Python's `Frame` vs `FrameData`)
      emerges.
- [x] One entry point each way, with no type checking left to callers
- [x] Bindings come from a pinned release, not copied into the repo
- [x] Shared `compas_pb` types come from the runtime package
- [x] Tested both directions against bytes Python produced
