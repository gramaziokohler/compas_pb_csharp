# COMPAS_PB C# Wrapper

<p align="center">
    <a href="#"><img src="https://img.shields.io/badge/C%23-latest-239120.svg?logo=csharp" alt="C# latest"></a>
    <a href="https://www.nuget.org/packages/CompasPb"><img src="https://img.shields.io/badge/.NET-netstandard2.0%20%7C%20net48%20%7C%20net9.0-blue?logo=dotnet" alt="Target Frameworks"></a>
    <a href="https://github.com/gramaziokohler/compas_pb_csharp/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License MIT"></a>
    <a href="https://github.com/gramaziokohler/compas_pb_csharp/actions"><img src="https://github.com/gramaziokohler/compas_pb_csharp/actions/workflows/build.yml/badge.svg" alt="Build Status"></a>
    <a href="https://github.com/gramaziokohler/compas_pb_csharp/blob/main/docs/ARCHITECTURE.md"><img src="https://img.shields.io/badge/docs-runtime-brightgreen.svg" alt="Runtime Documentation"></a>
    <a href="https://compas.dev/mission-control/#compas_pb"><img src="https://compas.dev/badge.svg" alt="Made with COMPAS"></a>
</p>

A COMPAS_PB extension which lets you serialize and deserialize COMPAS `Data` types using protobuf in C#.

## Installation

`CompasPb` is distributed as a NuGet package. Version `1.0.0` contains the generated
C# bindings and runtime support for the `compas_pb` 1.2 wire format; consumers do
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

NuGet restores the two runtime dependencies, `Google.Protobuf` and `Newtonsoft.Json`,
transitively.

### Install in Unity

Unity projects install the package from
[OpenUPM](https://openupm.com/packages/dev.compas.compas-pb/) instead of NuGet:

```bash
openupm add dev.compas.compas-pb
```

Or add the scoped registry to `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["dev.compas"]
    }
  ],
  "dependencies": {
    "dev.compas.compas-pb": "1.0.0"
  }
}
```

The Unity package requires Unity 2021.3 or newer and pulls in `Newtonsoft.Json`
through the `com.unity.nuget.newtonsoft-json` dependency. See the
[package README](https://github.com/gramaziokohler/compas_pb_csharp/blob/main/upm/dev.compas.compas-pb/README.md) for details.

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
pinned `compas_pb` 1.2 release assets before the build.

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

`CompasPbSerializer` is the runtime's single entry point, in both directions and in both
formats. It lives in the `CompasPb` namespace; the generated message types it packs live in
`CompasPb.Data`, so most files need both `using` directives. The `Serializer` and
`Deserializer` helpers behind it are internal — callers never need to reach for them.

All protobuf message types in the assembly are registered automatically at startup, so no
manual type registration is needed for the built-in COMPAS geometry.

`PackAsAnyData` / `UnpackAnyData` sit one level below `Pack` / `Unpack`, for filling an
`AnyData` field inside a message you are building yourself — see
[the domain package guide](https://github.com/gramaziokohler/compas_pb_csharp/blob/main/docs/DOMAIN_PACKAGE.md).

### Single object

```csharp
using CompasPb;
using CompasPb.Data;

var serializer = new CompasPbSerializer();

var frame = new FrameData
{
    Name = "myFrame",
    Point = new PointData { X = 1.0f, Y = 2.0f, Z = 3.0f },
    Xaxis = new VectorData { X = 1.0f, Y = 0.0f, Z = 0.0f },
    Yaxis = new VectorData { X = 0.0f, Y = 1.0f, Z = 0.0f },
};

// Binary (protobuf)
byte[] bytes = serializer.Pack(frame);
FrameData? unpacked = serializer.Unpack<FrameData>(bytes);

// JSON
string json = serializer.PackAsJson(frame);
FrameData? fromJson = serializer.UnpackJson<FrameData>(json);

// Dynamic — when you don't know the type ahead of time
object? result = serializer.Unpack(bytes);
object? fromJsonDynamic = serializer.UnpackJson(json);
```

### Nested data structures

Supports arbitrarily nested lists and dictionaries:

```csharp
using System.Collections.Generic;
using CompasPb;
using CompasPb.Data;

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

// Works with both binary and JSON
byte[] bytes   = serializer.Pack(data);
object? result = serializer.Unpack(bytes);

string json       = serializer.PackAsJson(data);
object? fromJson  = serializer.UnpackJson(json);
```

### Dependency Injection

Register `ICompasPbSerializer` in your DI container:

```csharp
using CompasPb;
using CompasPb.Data;

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

### Register domain-model types

Packages outside this runtime can register conversion functions without changing their
model classes. Register each mapping once during application startup; the protobuf type is
identified by its descriptor's full name, so identically named messages in different
packages cannot collide.

`Registry`, `CompasPbRegistrations` and `ICompasFallback` are in `CompasPb.Data`,
alongside the message types — only the serializer moved to `CompasPb`.

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
enumerating your types, so discovery stays cheap. A referenced assembly is loaded by the runtime
only when the process first uses one of its types, so yours may well not be loaded when CompasPb
first sweeps — CompasPb watches for later loads and runs your registrar when your assembly
arrives. `Registry.DiscoverRegistrations()` forces a sweep by hand if you ever need one.

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

## Documentation

- [Architecture](https://github.com/gramaziokohler/compas_pb_csharp/blob/main/docs/ARCHITECTURE.md) — how this runtime implements the `compas_pb` contract.
- [Using CompasPb from a domain package](https://github.com/gramaziokohler/compas_pb_csharp/blob/main/docs/DOMAIN_PACKAGE.md) — shipping your own `.proto`
  types and registering them.
- [Development](https://github.com/gramaziokohler/compas_pb_csharp/blob/main/docs/DEVELOPMENT.md) — building, testing, and releasing this repository.

`compas_pb` is a cross-language format; Python is the authoritative implementation. For the
format itself, the runtime contract, and the wider ecosystem, see the upstream
[compas_pb documentation](https://compas.dev/compas_pb/latest/).

## Issue Tracker

If you find a bug or if you have a problem with running the code, please file an issue on the [Issue Tracker](https://github.com/gramaziokohler/compas_pb_csharp/issues).
