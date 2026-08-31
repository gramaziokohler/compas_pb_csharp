# COMPAS Protobuf for Unity

`dev.compas.compas-pb` is the Unity Package Manager distribution of
[CompasPb](https://github.com/gramaziokohler/compas_pb_csharp). It lets Unity
projects serialize and deserialize COMPAS `Data` types with Protocol Buffers,
so geometry and data structures can be exchanged with COMPAS applications
written in Python.

The package ships the `netstandard2.0` build of `CompasPb.dll` together with the
`compas_pb` 1.2 wire-format bindings. No protobuf sources have to be generated
and no DLLs have to be copied into `Assets/` by hand.

## Requirements

- Unity 2021.3 or newer
- Either API Compatibility Level — the package ships a `netstandard2.0`
  assembly, which loads under both `.NET Standard 2.1` (the default) and
  `.NET Framework`

`Newtonsoft.Json` is required and is pulled in automatically through the
[`com.unity.nuget.newtonsoft-json`](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)
dependency. If your project already vendors a copy of `Newtonsoft.Json.dll`
under `Assets/`, remove it — two copies of the assembly will not load together.

## Installation

### OpenUPM (recommended)

Using the [openupm-cli](https://openupm.com/docs/getting-started.html):

```bash
openupm add dev.compas.compas-pb
```

Or add the scoped registry manually in `Packages/manifest.json`:

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

### Git URL

Unity can also install the package straight from the repository. Use the `upm`
branch tags — the package sits at the root of that branch, with the compiled
assemblies committed:

```
https://github.com/gramaziokohler/compas_pb_csharp.git#upm/v1.0.0
```

Add it through **Window → Package Manager → + → Add package from git URL**.

Pin the `#upm/v<version>` tag. Without it Unity tracks the default branch, and
`main` does not carry the built assemblies — `Runtime/` is generated at release
time, so a package installed from `main` or from a `v<version>` release tag has
no `CompasPb.dll` in it.

## Usage

```csharp
using CompasPb;
using CompasPb.Data;
using UnityEngine;

public class CompasPbExample : MonoBehaviour
{
    void Start()
    {
        var serializer = new CompasPbSerializer();

        var frame = new FrameData
        {
            Name = "myFrame",
            Point = new PointData { X = 1.0f, Y = 2.0f, Z = 3.0f },
            Xaxis = new VectorData { X = 1.0f, Y = 0.0f, Z = 0.0f },
            Yaxis = new VectorData { X = 0.0f, Y = 1.0f, Z = 0.0f },
        };

        byte[] bytes = serializer.Pack(frame);
        FrameData? unpacked = serializer.Unpack<FrameData>(bytes);

        Debug.Log(unpacked?.Name);
    }
}
```

Binary and JSON packing, downstream type registration through `Registry`, and
COMPAS JSON-dump fallbacks all work exactly as they do outside Unity.
`CompasPbSerializer` and `ICompasPbSerializer` live in the `CompasPb` namespace;
the generated message types live in `CompasPb.Data`. See the
[repository README](https://github.com/gramaziokohler/compas_pb_csharp/blob/main/README.md)
and the [runtime architecture notes](https://github.com/gramaziokohler/compas_pb_csharp/blob/main/docs/ARCHITECTURE.md)
for the full API.

### IL2CPP and code stripping

`Registry.DiscoverLoadedAssemblies()` and `Registry.RegisterAssembly(...)` rely
on reflection over generated protobuf message types. Under IL2CPP with managed
stripping enabled, preserve the bundled assemblies by adding a `link.xml` to
your project:

```xml
<linker>
  <assembly fullname="CompasPb" preserve="all" />
  <assembly fullname="Google.Protobuf" preserve="all" />
</linker>
```

If your own assembly declares `[assembly: CompasPbRegistrations(typeof(...))]`,
preserve the registrar type as well — it is only ever reached by reflection, so
the linker has no reason to keep it:

```xml
<linker>
  <assembly fullname="MyGame">
    <type fullname="MyGame.Data.MyConversions" preserve="all" />
  </assembly>
</linker>
```

## Issue tracker

Report problems on the
[issue tracker](https://github.com/gramaziokohler/compas_pb_csharp/issues).
