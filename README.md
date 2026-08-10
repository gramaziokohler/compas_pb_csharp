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

Use `CompasPbSerializer` to pack and unpack data:

### Single object

```csharp
using CompasPb;
using CompasPb.Data;

var serializer = new CompasPbSerializer();

// Pack
var frame = new FrameData
{
    Name = "myFrame",
    Point = new PointData { X = 1.0f, Y = 2.0f, Z = 3.0f },
    Xaxis = new VectorData { X = 1.0f, Y = 0.0f, Z = 0.0f },
    Yaxis = new VectorData { X = 0.0f, Y = 1.0f, Z = 0.0f },
};

// Binary
byte[] bytes = serializer.Pack(frame);
FrameData? unpacked = serializer.Unpack<FrameData>(bytes);

// JSON
string json = serializer.PackAsJson(frame);
FrameData? fromJson = serializer.UnpackJson<FrameData>(json);

// Dynamic — when you don't know the type ahead of time
object? result = serializer.Unpack(bytes);
```

### Nested data structures

Supports arbitrarily nested lists and dictionaries:

```csharp
using CompasPb;
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

byte[] bytes   = serializer.Pack(data);
object? result = serializer.Unpack(bytes);
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
using CompasPb.Data;
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
