# Using compas_pb_csharp as a Domain Package

This guide explains how to consume domain package types from C# using
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
release asset). The C# consumer installs the bindings and registers the
assembly with the runtime.

---

## Step 1: Install the bindings

Install the domain package's generated C# bindings alongside `CompasPb`:

```
dotnet add package CompasPb
dotnet add package Antikythera.Data   # example domain package
```

---

## Step 2: Register the assembly at startup

Call `RegisterAssembly` once at startup, passing any type from the domain
package's assembly as an anchor:

```csharp
using CompasPb.Data;
using Antikythera.Data;  // from the domain package's generated C# bindings

// At startup (e.g. Program.cs, Grasshopper plugin load, Unity Awake())
Registry.RegisterAssembly(typeof(ToolPathData).Assembly);
```

This scans the assembly for all `IMessage` types, registers them by both
simple and full protobuf name, and rebuilds internal caches. Safe to call
multiple times (idempotent).

Optionally mark the assembly with `[CompasPbRegistration]` as a convention
for future auto-discovery:

```csharp
[assembly: CompasPb.CompasPbRegistration]
```

---

## Step 3: Pack and Unpack

Once registered, `Pack`/`Unpack` work for the domain package's types:

```csharp
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

// Send to Python
byte[] bytes = serializer.Pack(toolPath);

// Receive from Python
var received = serializer.Unpack<ToolPathData>(bytes);
```

---

## Converter functions (optional)

If the domain has its own model types that are not `IMessage` implementations
(e.g. Unity's `Vector3`, Rhino's `Point3d`), register converter functions so
`Pack`/`Unpack` work with them directly:

```csharp
// Serializer: domain type -> Any
Registry.RegisterSerializer<Plane>(plane =>
    Any.Pack(new FrameData
    {
        Point = new PointData { X = plane.Origin.X, Y = plane.Origin.Y, Z = plane.Origin.Z },
        Xaxis = new VectorData { X = plane.XAxis.X, Y = plane.XAxis.Y, Z = plane.XAxis.Z },
        Yaxis = new VectorData { X = plane.YAxis.X, Y = plane.YAxis.Y, Z = plane.YAxis.Z },
    }));

// Deserializer: Any -> domain type (keyed by full protobuf name)
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
byte[] bytes = serializer.Pack(myPlane);
var plane = (Plane)serializer.Unpack(bytes);
```

To remove a converter (e.g. in tests):

```csharp
Registry.UnregisterSerializer<Plane>();
Registry.UnregisterDeserializer("compas_pb.data.FrameData");
```

---

## Summary

| Step | What | Where |
| --- | --- | --- |
| Install | Add the domain package's NuGet bindings | `dotnet add package` |
| Register | `Registry.RegisterAssembly(typeof(T).Assembly)` | Startup code |
| Use | `serializer.Pack(obj)` / `serializer.Unpack<T>(bytes)` | Anywhere |
| Convert (optional) | `RegisterSerializer<T>` / `RegisterDeserializer` | Startup code |
