# Using CompasPb from a domain package

This guide explains how to bring a domain package's types into C# with
`compas_pb_csharp` as the runtime.

For background on domain packages vs. language runtimes, see the upstream
[Architecture](https://compas.dev/compas_pb/latest/architecture/) doc.

---

## What is a domain package?

A domain package defines data types for a problem domain (e.g. robot task
planning, timber construction). It owns `.proto` files and publishes generated
bindings for each language. Examples:

| Package | Domain |
| --- | --- |
| `compas_pb` | COMPAS core types (`Point`, `Frame`, `Mesh`, ...) |
| `antikythera` | Robot task planning |
| `compas_timber` | Timber construction |

The domain package publishes generated C# bindings (as a NuGet package or
release asset). The runtime itself never learns about them: it only needs the
messages to be reachable, and the conversions to be registered.

---

## Step 1: Install the bindings

Install the domain package's generated C# bindings alongside `CompasPb`:

```
dotnet add package CompasPb
dotnet add package Antikythera.Data   # example domain package
```

---

## Step 2: Pack and unpack the generated messages

Generated protobuf messages need no conversion functions — they *are* the wire
type. Point the registry at the assembly holding them, using any type from it as
an anchor:

```csharp
using CompasPb;
using CompasPb.Data;
using Antikythera.Data;  // from the domain package's generated C# bindings

// At startup (e.g. Program.cs, Grasshopper plugin load, Unity Awake())
Registry.RegisterAssembly(typeof(ToolPathData).Assembly);

var serializer = new CompasPbSerializer();

var toolPath = new ToolPathData
{
    Name = "milling_path_01",
    ToolFrame = new FrameData
    {
        Point = new PointData { X = 0.0f, Y = 5.0f, Z = 0.0f },
        Xaxis = new VectorData { X = 1.0f, Y = 0.0f, Z = 0.0f },
        Yaxis = new VectorData { X = 0.0f, Y = 0.0f, Z = 1.0f },
    },
};

// Send to Python
byte[] bytes = serializer.Pack(toolPath);

// Receive from Python
ToolPathData? received = serializer.Unpack<ToolPathData>(bytes);
```

`RegisterAssembly` scans for `IMessage` types and keys each one by its
descriptor's full protobuf name. It is idempotent, so repeat calls are free.
`Registry.DiscoverLoadedAssemblies()` does the same for every assembly the
process has already loaded.

---

## Step 3: Register your own model types

Domain packages usually have model classes of their own — a Unity `Vector3`, a
Rhino `Point3d`, a hand-written `Plane` — that are not `IMessage`
implementations. Register a conversion function in each direction and the
runtime handles them like any other type:

```csharp
Registry.Register<Plane, FrameData>(
    plane => new FrameData
    {
        Point = new PointData { X = plane.Origin.X, Y = plane.Origin.Y, Z = plane.Origin.Z },
        Xaxis = new VectorData { X = plane.XAxis.X, Y = plane.XAxis.Y, Z = plane.XAxis.Z },
        Yaxis = new VectorData { X = plane.YAxis.X, Y = plane.YAxis.Y, Z = plane.YAxis.Z },
    },
    message => new Plane(
        new Point(message.Point.X, message.Point.Y, message.Point.Z),
        new Vector(message.Xaxis.X, message.Xaxis.Y, message.Xaxis.Z),
        new Vector(message.Yaxis.X, message.Yaxis.Y, message.Yaxis.Z))
);

// Pack/Unpack now work with Plane directly
byte[] bytes = serializer.Pack(myPlane);
var plane = (Plane)serializer.Unpack(bytes)!;
```

The serializer function returns the protobuf message; the runtime wraps it in
`Any`. The deserializer function receives the message already parsed. This
mirrors the Python `@pb_serializer` / `@pb_deserializer` pair, which is what
keeps a registration reading the same way in both languages.

Register the two halves separately with `Registry.RegisterSerializer<TObject,
TMessage>` and `Registry.RegisterDeserializer<TMessage, TObject>` when only one
direction is needed.

Serializer lookup walks the C# inheritance chain, so registering a base class
also covers everything derived from it — the same rule as Python's MRO walk.

---

## Step 4: Register without a startup call

Requiring every host application to call your package's registration code means
every host has to know about every package. Declare the registrar on your
assembly instead, and CompasPb invokes it the first time it is needed:

```csharp
[assembly: CompasPbRegistrations(typeof(AntikytheraConversions))]

public static class AntikytheraConversions
{
    public static void Register()
    {
        Registry.RegisterAssembly(typeof(ToolPathData).Assembly);
        Registry.Register<Plane, FrameData>(/* ... */);
    }
}
```

Each declared registrar runs at most once. Reading the attribute does not
enumerate your types, so discovery stays cheap; assemblies loaded later are
picked up by calling `Registry.DiscoverRegistrations()` again.

Keep the registrations themselves as explicit `Register<,>` calls. That keeps
the generic instantiations statically visible, so they survive IL2CPP and
trimming — only the call *into* `Register` is discovered reflectively. Under a
stripping linker, preserve the registrar type so its method is kept.

---

## Messages with `AnyData` fields

A `.proto` message can hold arbitrary compas_pb values through `AnyData` — the
built-in `MeshData.edge_keys`, `GraphData.node_keys` and
`AttributeColumn.values` all do. Fill and read those with the value-level pair
rather than reimplementing the dispatch:

```csharp
var mesh = new MeshData();
foreach (var edge in edges)
{
    mesh.EdgeKeys.Add(serializer.PackAsAnyData(edge.Key));
}

var key = serializer.UnpackAnyData(mesh.EdgeKeys[0]);
```

`Pack` / `Unpack` stay the entry points for a whole payload; these two are for a
single field inside a message you are already building.

---

## COMPAS types with no protobuf schema

A COMPAS type that has no `.proto` message still travels, through the fallback
envelope, as its COMPAS JSON dump:

```csharp
Registry.RegisterFallback<Widget>(
    "my_package/Widget",
    widget => new Dictionary<string, object>
    {
        ["data"] = new Dictionary<string, object> { ["name"] = widget.Name },
    },
    values =>
    {
        var data = (Dictionary<string, object?>)values["data"]!;
        return new Widget((string)data["name"]!);
    }
);
```

The runtime writes the registered `dtype` into the envelope. A fallback dtype
with no C# registration deliberately comes back as its dictionary rather than
throwing, so an unknown payload stays inspectable and forwardable.

---

## Summary

| Step | What | Where |
| --- | --- | --- |
| Install | Add the domain package's NuGet bindings | `dotnet add package` |
| Generated messages | `Registry.RegisterAssembly(typeof(T).Assembly)` | Registrar or startup |
| Model types | `Registry.Register<TObject, TMessage>(to, from)` | Registrar or startup |
| No schema | `Registry.RegisterFallback<TObject>(dtype, to, from)` | Registrar or startup |
| Discovery | `[assembly: CompasPbRegistrations(typeof(...))]` | Your package |
| Use | `serializer.Pack(obj)` / `serializer.Unpack<T>(bytes)` | Anywhere |
