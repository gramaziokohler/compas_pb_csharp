# Development Guide

## Versioning & Releases

This repo uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (nbgv) to stamp every build with a version derived from `version.json`.

### Scheme

Standard SemVer: `MAJOR.MINOR.PATCH` (e.g. `0.1.0`, `0.1.1`, `0.2.0`, `1.0.0`).

The full version is set explicitly in `version.json` (field `version`, e.g. `"0.1.0"`). Commits **do not** auto-bump the version — the version only changes when `version.json` is edited and merged. Releases are deliberate.

### Files

| File | Role |
|---|---|
| `version.json` | Source of truth. `"version": "0.1.0"` sets the full `MAJOR.MINOR.PATCH`. |
| `Directory.Build.props` | Adds `Nerdbank.GitVersioning` `PackageReference` to every project. |
| `Directory.Packages.props` | Pins `Nerdbank.GitVersioning` version (central package management). |
| `.github/workflows/release.yml` | Fires on `v*` tag push — builds, publishes, zips artifacts, creates GitHub Release. |

### Prerequisites

Install the nbgv CLI globally (once per machine):

```bash
dotnet tool install --global nbgv
```

Check the current version:

```bash
nbgv get-version
# Version:             0.1.0.nnnn
# NuGetPackageVersion: 0.1.0
```

---

## How to release

Every release is a two-step process: bump `version.json` in a PR, then tag after merge.

### Patch release (`0.1.0` → `0.1.1`)

1. Open a PR editing `version.json`:
   ```diff
   - "version": "0.1.0",
   + "version": "0.1.1",
   ```
2. Merge to `main`.
3. Tag and push:
   ```bash
   git checkout main && git pull
   git tag v0.1.1
   git push origin v0.1.1
   ```
   Pushing the tag triggers `release.yml`.

### Minor release (`0.1.x` → `0.2.0`)

Same flow: edit `version.json` to `"0.2.0"`, merge, tag `v0.2.0`, push tag.

### Major release (`0.x.x` → `1.0.0`)

Same flow: edit `version.json` to `"1.0.0"`, merge, tag `v1.0.0`, push tag.

### Quick reference

| Action | Command |
|---|---|
| Show current version | `nbgv get-version` |
| Show NuGet version only | `nbgv get-version -v NuGetPackageVersion` |
| Tag a release | `git tag v<version> && git push origin v<version>` |
| Bump version | Edit `version.json`, open PR, merge |

---

## Version properties during build

nbgv populates these MSBuild properties automatically (readable from any `.csproj`):

| Property | Example |
|---|---|
| `$(Version)` | `0.1.0` |
| `$(AssemblyVersion)` | `0.1.0.0` |
| `$(FileVersion)` | `0.1.0.nnnn` |
| `$(PackageVersion)` | `0.1.0` |
| `$(InformationalVersion)` | `0.1.0+abc1234` |

On non-public branches (anything other than `main` or a `v*` tag), the NuGet/Informational versions get a `-g<sha>` suffix (e.g. `0.1.0-gd9f645a810`). This is controlled by `publicReleaseRefSpec` in `version.json`.

From code:

```csharp
var info = typeof(CompasPb.Serializer).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
    .InformationalVersion;
```
