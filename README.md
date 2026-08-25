# COMPAS_PB C# Wrapper

<p align="center">
</p>

<p align="center">
    <a href="#"><img src="https://img.shields.io/badge/C%23-latest-239120.svg?logo=csharp" alt="C# latest"></a>
    <a href="#"><img src="https://img.shields.io/badge/.NET-netstandard2.0%20%7C%20net48%20%7C%20net9.0-blue?logo=dotnet" alt="Target Frameworks"></a>
    <a href="https://github.com/gramaziokohler/compas_pb_csharp/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License MIT"></a>
    <a href="https://github.com/gramaziokohler/compas_pb_csharp/actions"><img src="https://github.com/gramaziokohler/compas_pb_csharp/actions/workflows/build.yml/badge.svg" alt="Build Status"></a>
    <a href="https://github.com/gramaziokohler/compas_pb_csharp/blob/main/ARCHITECTURE.md"><img src="https://img.shields.io/badge/docs-runtime-brightgreen.svg" alt="Runtime Documentation"></a>
</p>

A COMPAS_PB extension which lets you serialize and deserialize COMPAS `Data` types using protobuf in C#.

## Installation

`CompasPb` is distributed as a NuGet package. Version `1.0.0` contains the generated
C# bindings and runtime support for the `compas_pb` 1.1 wire format; consumers do
not need to generate protobuf sources or copy DLLs into their projects manually.

### Install from NuGet

From the directory containing your project file, use the .NET CLI:

```bash
dotnet add package CompasPb --version 1.0.0
```

Or use the NuGet Package Manager Console in Visual Studio:

```powershell
Install-Package CompasPb -Version 1.0.0
```

You can also add the package reference directly to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="CompasPb" Version="1.0.0" />
</ItemGroup>
```

Restore the project after editing the project file directly:

```bash
dotnet restore
```

NuGet restores `Google.Protobuf`, `Newtonsoft.Json`, and any framework-specific
dependencies transitively.

### Supported target frameworks

The package provides assemblies for:

| Target framework | Typical consumers |
| --- | --- |
| `netstandard2.0` | Unity, Grasshopper, and other .NET Standard-compatible applications |
| `net48` | Rhino and other .NET Framework 4.8 applications |
| `net9.0` | Current .NET applications and services |

Your application only needs to target one compatible framework. The .NET 9 SDK is
required to build this repository itself, but it is not required merely to consume
the `netstandard2.0` or `net48` package assets.

### Build from source

Building the repository requires Git, Python 3, and the .NET 9 SDK. The generated
C# protobuf files are intentionally not committed; the fetch script downloads the
pinned `compas_pb` 1.1 release assets before the build.

```bash
git clone https://github.com/gramaziokohler/compas_pb_csharp.git
cd compas_pb_csharp
python3 fetch_compas_pb.py
dotnet restore
dotnet build src/CompasPb/CompasPb.csproj --configuration Release
dotnet test test/CompasPb.Test.csproj --configuration Release
```

To create a local NuGet package from a release checkout:

```bash
dotnet pack src/CompasPb/CompasPb.csproj \
  --configuration Release \
  --output ./artifacts
```

Then add `./artifacts` as a package source, or reference it for a one-off install:

```bash
dotnet add path/to/YourProject.csproj package CompasPb \
  --version 1.0.0 \
  --source ./artifacts
```

## Usage

There are three ways to use this library depending on your context.

### Static API (Grasshopper / Unity / simple scripts)

The static `Serializer` and `Deserializer` classes work without any setup:

```csharp
using CompasPb.Data;

// Pack
var frame = new FrameData
{
    Name = "myFrame",
    Point = new PointData { X = 1.0f, Y = 2.0f, Z = 3.0f },
    Xaxis = new VectorData { X = 1.0f, Y = 0.0f, Z = 0.0f },
    Yaxis = new VectorData { X = 0.0f, Y = 1.0f, Z = 0.0f },
};

byte[] bytes = Serializer.PackAsBytes(Serializer.PackAsAnyData(frame));

// Unpack — returns object?, cast manually
AnyData anyData = Deserializer.UnpackBytes(bytes);
object? result  = Deserializer.UnpackAnyData(anyData);
var unpacked    = result as FrameData;
```

### Typed instance API (known type at compile time)

Use `CompasPbSerializer` directly for a cleaner typed experience — no casting needed:

```csharp
using CompasPb.Data;

var serializer = new CompasPbSerializer();

// Pack
byte[] bytes = serializer.Pack(frame);

// Unpack — typed, no cast
FrameData? unpacked = serializer.Unpack<FrameData>(bytes);

// Unpack — dynamic, when you don't know the type ahead of time
object? result = serializer.Unpack(bytes);
```

### Dependency Injection

Register `ICompasPbSerializer` in your DI container:

```csharp
// Program.cs / Startup.cs
services.AddSingleton<ICompasPbSerializer, CompasPbSerializer>();

// In any service or controller
public class RobotService(ICompasPbSerializer serializer)
{
    public FrameData? GetFrame(byte[] bytes)
        => serializer.Unpack<FrameData>(bytes);

    public byte[] SendFrame(FrameData frame)
        => serializer.Pack(frame);
}
```

### Nested data structures

All three options support arbitrarily nested lists and dictionaries:

```csharp
using CompasPb.Data;
using System.Collections.Generic;

var serializer = new CompasPbSerializer();

var data = new Dictionary<string, object>
{
    ["center"] = new PointData { X = 1.0f, Y = 2.0f, Z = 3.0f },
    ["points"] = new List<object>
    {
        new PointData { X = 0.0f, Y = 0.0f, Z = 0.0f },
        new PointData { X = 10.0f, Y = 0.0f, Z = 0.0f },
    },
};

byte[] bytes  = serializer.Pack(data);
object? result = serializer.Unpack(bytes);
```

### Register domain-model types

Packages outside this runtime can register conversion functions without changing their
model classes. Register each mapping once during application startup; the protobuf type is
identified by its descriptor's full name, so identically named messages in different
packages cannot collide.

```csharp
Registry.Register<Widget, WidgetData>(
    widget => new WidgetData { Name = widget.Name },
    message => new Widget(message.Name)
);

byte[] bytes = serializer.Pack(new Widget("A"));
Widget widget = (Widget)serializer.Unpack(bytes)!;
```

Serializer lookup follows the C# inheritance chain, so a mapping registered for a base
class also handles derived instances. Generated protobuf messages can be registered for
identity deserialization with `Registry.RegisterAssembly(typeof(WidgetData).Assembly)`;
`Registry.DiscoverLoadedAssemblies()` scans all assemblies already loaded by the process.

#### Register automatically from your own package

Calling `Registry.Register` from application startup means every host has to know about every
package. To make a package's conversions apply just by being referenced, put the registrations
in a static method and point at it from an assembly-level attribute:

```csharp
[assembly: CompasPbRegistrations(typeof(WidgetConversions))]

public static class WidgetConversions
{
    public static void Register()
    {
        Registry.Register<Widget, WidgetData>(
            widget => new WidgetData { Name = widget.Name },
            message => new Widget(message.Name)
        );
    }
}
```

CompasPb invokes each declared registrar at most once, and reads the attribute without
enumerating your types, so discovery stays cheap. Assemblies loaded later are picked up by
calling `Registry.DiscoverRegistrations()` again.

The registrations themselves stay explicit `Register<,>` calls, so the generic instantiations
remain statically visible and survive IL2CPP or trimming; only the call into `Register` is
discovered. Under a stripping linker, preserve the registrar type so its method is kept.

For a COMPAS type that has no protobuf schema, register its COMPAS JSON-dump fallback in
both directions:

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

The runtime writes the registered `dtype` into the fallback envelope. If a fallback dtype
has no C# registration, deserialization deliberately returns its JSON-dump dictionary so
the payload remains inspectable. The older `ICompasFallback` interface remains supported
for existing model classes.

### HTTP transport

Use `CompasPbHttpClient` to send and receive data from a running `compas_pb` Python server:

```csharp
using CompasPb.Data;
using CompasPb.Route;

var serializer = new CompasPbSerializer();
var client     = new CompasPbHttpClient("http://localhost:5000", serializer);

// Send — packs and POSTs to /receiver
await client.SendAsync(frame);

// Receive typed — GETs from /sender and unpacks
FrameData? result = await client.ReceiveAsync<FrameData>();

// Receive dynamic — when the type is unknown
object? result = await client.ReceiveAsync();
```

## Documentation

For further "getting started" instructions, a tutorial, examples, and an API reference,
see the [runtime architecture](https://github.com/gramaziokohler/compas_pb_csharp/blob/main/ARCHITECTURE.md)
and the upstream
[compas_pb documentation](https://compas.dev/compas_pb/latest/).

## Issue Tracker

If you find a bug or if you have a problem with running the code, please file an issue on the [Issue Tracker](https://github.com/gramaziokohler/compas_pb_csharp/issues).
