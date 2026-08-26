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

JSON equivalents, implementing upstream `pb_dump_json` / `pb_load_json`:

```
CompasPbSerializer.PackAsJson(object)  → string
CompasPbSerializer.UnpackJson(string)  → object?
CompasPbSerializer.UnpackJson<T>(string) → T?
```

`CompasPb.Route.CompasPbHttpClient` wraps the binary pair for talking to a
running `compas_pb` Python server over HTTP; it is transport, not part of the
runtime contract.

All public access goes through `CompasPbSerializer` (or the `ICompasPbSerializer`
interface for DI). Both live in the `CompasPb` namespace; the generated message
types live in `CompasPb.Data`. The `Serializer` and `Deserializer` helpers behind
them are `internal`, so a caller cannot end up doing its own type dispatch — which
is the point of the contract's "no type checking left to callers".

---

## Recursive dispatch

### Encoding (`Serializer.PackAsAnyData`)

```
serialize(obj)
  ├─ null?                    → value (null)
  ├─ registered serializer?   → message (Any.Pack of the returned message)
  ├─ registered fallback?     → fallback (its dict form, tagged with the dtype)
  ├─ ICompasFallback?         → fallback (serialize its dict form)
  ├─ IMessage?                → message (Any.Pack with type_url)
  ├─ IDictionary?             → dict_value, recurse per value
  ├─ byte[]?                  → value ("base64:" + encoded)
  ├─ string?                  → value
  ├─ bool?                    → value
  ├─ integral?                → int_value
  ├─ floating-point?          → double_value
  ├─ IEnumerable?             → list_value, recurse per item
  └─ otherwise                → ArgumentException
```

Registered conversions are consulted before the structural arms, so a model type
that happens to implement `IDictionary` or `IEnumerable` still travels as its
registered message rather than as a container. `IEnumerable` sits last for the
same reason — `string` and `byte[]` are enumerable too, and each has its own arm.

`ICompasFallback` is checked before `IMessage` so that a domain object that
implements both gets the fallback path (preserving its full dict representation).

### Decoding (`Deserializer.UnpackAnyData`)

Switches on `AnyData.DataOneofCase` — the mirror image of encoding. The
`message` arm first checks for `Any`-wrapped `ListData`/`DictData` (backward
compatibility with older payloads), then resolves via the registry.

---

## Registry and type registration

The registry stores **functions**, not a required class shape — a domain model
never has to implement a CompasPb interface or inherit a CompasPb base class.
This mirrors `compas_pb.registry.SerializerRegistry` in Python, and the
signatures are deliberately the same shape:

| Python | C# |
| --- | --- |
| `@pb_serializer(Plane)` | `Registry.RegisterSerializer<Plane, FrameData>(fn)` |
| `@pb_deserializer(FrameData)` | `Registry.RegisterDeserializer<FrameData, Plane>(fn)` |
| both at once | `Registry.Register<Plane, FrameData>(to, from)` |
| `_SERIALIZERS[type]`, MRO walk | serializer lookup walks the inheritance chain |
| `_DESERIALIZERS[full_name]` | deserializer keyed by `Descriptor.FullName` |

A serializer function returns the protobuf message and the runtime wraps it in
`Any`; a deserializer function receives the message already parsed. Keeping the
`Any` handling inside the runtime is what lets the same registration read the
same way in Python and C#.

### Generated messages

Generated protobuf messages need no conversion function — they are already the
wire type. `Registry.RegisterAssembly(assembly)` scans an assembly for `IMessage`
implementations and registers each under its descriptor's full protobuf name.
The runtime scans its own assembly at startup;
`Registry.DiscoverLoadedAssemblies()` covers everything else the process has
loaded. Both are idempotent and work on all targets: .NET Standard 2.0 (Unity),
.NET Framework 4.8 (Rhino/Grasshopper), and .NET 9.

### Fallback conversions

`Registry.RegisterFallback<TObject>(dtype, to, from)` registers a COMPAS
JSON-dump conversion for a type with no `.proto` message, keyed by COMPAS
`dtype`. The runtime writes the envelope as well as reading it. An unregistered
dtype deliberately decodes to its dictionary instead of throwing, so an unknown
payload stays inspectable and forwardable — the equivalent of Python handing
back what `DataDecoder` could not resolve.

### Type URL resolution

`Registry.GetType(string typeUrl)` takes everything after the last `/` and looks
it up as a full protobuf name. There is no simple-name fallback: matching
`"PointData"` on its own would collide the moment a domain package ships a
message of the same name, and the contract is explicit that type URLs match on
the full name.

### The `[CompasPbRegistrations]` attribute

Requiring the host application to call each package's registration code means
every host has to know about every package. A package declares its registrar on
its own assembly instead:

```csharp
[assembly: CompasPbRegistrations(typeof(ToolPathConversions))]

public static class ToolPathConversions
{
    public static void Register()
    {
        Registry.RegisterAssembly(typeof(ToolPathData).Assembly);
        Registry.Register<ToolPath, ToolPathData>(/* ... */);
    }
}
```

`Registry`'s static constructor reads that attribute off every loaded assembly
and invokes each registrar once, so a package's types work by being referenced.
Reading an assembly-level attribute does not enumerate types, which keeps the
startup pass cheap; the expensive `IMessage` scan stays lazy in
`DiscoverLoadedAssemblies`.

The registrations themselves stay explicit `Register<,>` calls, so the generic
instantiations remain statically visible and survive IL2CPP or trimming; only
the call *into* `Register` is discovered reflectively. Under a stripping linker,
preserve the registrar type so its method is kept.

For a step-by-step guide, see [Using CompasPb from a domain package](./DOMAIN_PACKAGE.md).

---

## JSON

`PackAsJson` / `UnpackJson` implement upstream `pb_dump_json` / `pb_load_json`
over the same `MessageData` envelope, so a JSON payload crosses between runtimes
exactly as a binary one does.

protobuf-json cannot read or write an `Any` field without a descriptor for the
message inside it. Python gets that from the default descriptor pool; C# needs
an explicit `TypeRegistry`, which `Registry.GetJsonTypeRegistry()` builds from
the descriptors the registry already holds and caches until a new type is
registered.

`FormatDefaultValues` is left off, matching `MessageToJson`'s defaults, so the
two runtimes emit comparable JSON for the same object. Every `AnyData` arm is a
`oneof` member, and protobuf-json always writes a set `oneof` field, so a zero,
an empty string, or a `false` still survives the round trip.

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
- [x] Type URLs matched on the full name after the last `/`, with no simple-name fallback
- [x] Registry holds functions, and other packages can register types.
      `Register<TObject, TMessage>` takes a conversion function in each
      direction for non-`IMessage` model types (e.g. Unity `Vector3` ↔
      `PointData`); `RegisterAssembly` covers generated messages;
      `[assembly: CompasPbRegistrations(...)]` lets a package register itself
      without the host calling into it. Serializer lookup follows the
      inheritance chain, so a base-class registration covers derived types.
- [x] One entry point each way, with no type checking left to callers
- [x] Bindings come from a pinned release, not copied into the repo
- [x] Shared `compas_pb` types come from the runtime package
- [x] Tested both directions against bytes Python produced
