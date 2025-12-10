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

## Basic Usage

### (De)serialize to bytes

```cs
using CompasPB.Data

vector = VectorData(
        x = new PointData
        {
            X = 1.02F,
            Y = 2.02F,
            Z = 3.02F,
        },
        y = new VectorData
        {
            X = 1.02F,
            Y = 0.02F,
            Z = 0.02F,
        },
        z = new VectorData
        {
            X = 0.02F,
            Y = 1.02F,
            Z = 0.02F,
        },
)

var messageData = Serializer.PackAsAnyData(vector);
var packData = Serializer.PackAsBytes(messageData);

var unpackedData = Deserializer.UnpackBytes(response);
var unpackedDataType = Deserializer.GetType(unpackedData);
var data = Deserializer.UnpackAnyData(unpackedData, unpackedDataType);
```

### (De)serialize to file

Coming soon ...

### Serialization of arbitrarily nested data structures

```python
using CompasPB.Data
using System.Collection.Generic

var data = new Dictionary<string, object>
{
    ["center"] = new PointData
    {
        X = 1.02F,
        Y = 2.02F,
        Z = 3.02F,
    },

    ["outline"] = new List<object>
    {
        new PolylineData
        {
            Points = new List<PointData>
            {
                 new PointData { X = 0.0f, Y = 0.0f, Z = 0.0f },
                 new PointData { X = 10.0f, Y = 0.0f, Z = 0.0f }
            }
        }
        new PolylineData
        {
            Points = new List<PointData>
            {
                 new PointData { X = 0.0f, Y = 0.0f, Z = 0.0f },
                 new PointData { X = 10.0f, Y = 0.0f, Z = 0.0f }
            }
        }
    }
};

var messageData = Serializer.PackAsAnyData(data);
var packData = Serializer.PackAsBytes(messageData);

var unpackedData = Deserializer.UnpackBytes(response);
var unpackedDataType = Deserializer.GetType(unpackedData);
var data = Deserializer.UnpackAnyData(unpackedData, unpackedDataType);
```

## Documentation

For further "getting started" instructions, a tutorial, examples, and an API reference,
please check out the online documentation here: [compas_pb_csharp docs](https://gramaziokohler.github.io/compas_pb_csharp)

## Issue Tracker

If you find a bug or if you have a problem with running the code, please file an issue on the [Issue Tracker](https://github.com/gramaziokohler/compas_pb_csharp/issues).

