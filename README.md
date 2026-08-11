# COMPAS_PB C# Wrapper

<p align="center">
    <a href="#"><img src="https://img.shields.io/badge/C%23-12.0-239120.svg?logo=csharp" alt="C# 12.0"></a>
    <a href="https://www.nuget.org/packages/CompasPb"><img src="https://img.shields.io/badge/.net-netstandard2.0%20|%20net48%20|%20net9.0-blue?logo=dotnet" alt="Target Frameworks"></a>
    <a href="https://github.com/gramaziokohler/compas_pb_csharp/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License MIT"></a>
    <!-- <a href="https://github.com/gramaziokohler/compas_pb_csharp/actions"><img src="https://github.com/gramaziokohler/compas_pb_csharp/actions/workflows/build.yml/badge.svg" alt="Build Status"></a> -->
    <!-- <a href="https://gramaziokohler.github.io/compas_pb_csharp"><img src="https://img.shields.io/badge/docs-latest-brightgreen.svg" alt="Documentation"></a> -->
    <a href="https://compas.dev/mission-control/#compas_pb"><img src="https://compas.dev/badge.svg" alt="Made with COMPAS"></a>
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

Use `CompasPbSerializer` to pack and unpack data. All protobuf message types in the
assembly are registered automatically at startup — no manual type registration needed.

### Single object

```csharp
using CompasPb;

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
using CompasPb;
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

### HTTP transport

Use `CompasPbHttpClient` to send and receive data from a running `compas_pb` Python server:

```csharp
using CompasPb;
using CompasPb.Route;

var serializer = new CompasPbSerializer();
var client     = new CompasPbHttpClient("http://localhost:5000", serializer);

// Send — packs and POSTs to /receiver
await client.SendAsync(frame);

// Receive typed — GETs from /sender and unpacks
FrameData? result = await client.ReceiveAsync<FrameData>();

// Receive dynamic — when the type is unknown
object? dynamic = await client.ReceiveAsync();
```

## Documentation

For further "getting started" instructions, a tutorial, examples, and an API reference,
please check out the online documentation here: [compas_pb_csharp docs](https://gramaziokohler.github.io/compas_pb_csharp)

## Issue Tracker

If you find a bug or if you have a problem with running the code, please file an issue on the [Issue Tracker](https://github.com/gramaziokohler/compas_pb_csharp/issues).
