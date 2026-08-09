# COMPAS_PB C# Wrapper

<p align="center">
</p>

<p align="center">
    <a href="#"><img src="https://img.shields.io/badge/C%23-12.0-239120.svg?logo=csharp" alt="C# 12.0"></a>
    <a href="#"><img src="https://img.shields.io/badge/target%20framework-net9.0-blue" alt="Target Framework"></a>
    <a href="https://github.com/gramaziokohler/compas_pb_csharp/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License MIT"></a>
    <a href="https://github.com/gramaziokohler/compas_pb/actions"><img src="https://github.com/gramaziokohler/compas_pb/actions/workflows/build.yml/badge.svg" alt="Build Status"></a>
    <a href="https://gramaziokohler.github.io/compas_pb_csharp"><img src="https://img.shields.io/badge/docs-latest-brightgreen.svg" alt="Documentation"></a>
</p>

A COMPAS_PB extension which lets you serialize and deserialize COMPAS `Data` types using protobuf in C#.

## Installation

Coming soon...

## Usage

There are three ways to use this library depending on your context.

### Option A — Static API (Grasshopper / Unity / simple scripts)

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

### Option B — Typed instance API (known type at compile time)

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

### Option C — Dependency Injection (ASP.NET / larger apps)

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
please check out the online documentation here: [compas_pb_csharp docs](https://gramaziokohler.github.io/compas_pb_csharp)

## Issue Tracker

If you find a bug or if you have a problem with running the code, please file an issue on the [Issue Tracker](https://github.com/gramaziokohler/compas_pb_csharp/issues).
