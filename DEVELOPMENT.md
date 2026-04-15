# Development Guide

## Versioning & Releases

This repo uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (nbgv) to stamp every build with a version derived from `version.json` + git history.

### Scheme

Standard SemVer: `MAJOR.MINOR.PATCH` (e.g. `0.1.0`, `0.1.1`, `0.2.0`, `1.0.0`).

- `MAJOR.MINOR` — set in `version.json` (field `version`, e.g. `"0.1"`).
- `PATCH` — auto-advanced by **version height** (commits on `main` since the `version` field was last changed).

So on `main`: `0.1.0` → `0.1.1` → `0.1.2` → … each merge bumps patch automatically. No action needed.

### Files

| File | Role |
|---|---|
| `version.json` | Source of truth. `"version": "0.1"` sets `MAJOR.MINOR`. |
| `Directory.Build.props` | Adds `Nerdbank.GitVersioning` `PackageReference` to every project. |
| `Directory.Packages.props` | Pins `Nerdbank.GitVersioning` version (central package management). |
| `.github/workflows/release.yml` | Fires on `v*` tag push — builds, publishes, zips artifacts, creates GitHub Release. |

### Prerequisites

Install the nbgv CLI globally (once per machine):

```bash
dotnet tool install --global nbgv
```

Check what the current version is:

```bash
nbgv get-version
# Version:             0.1.3.xxxx
# NuGetPackageVersion: 0.1.3
```

---

## How to release

### Patch release (`0.1.x`)

Patch increments happen automatically on every merge to `main`. You only tag when you want to ship an official release — you don't have to tag every merge.

```bash
git checkout main && git pull

# Verify the version you're about to tag
nbgv get-version

# Create the tag at HEAD (nbgv picks the right name, e.g. v0.1.3)
nbgv tag

# Push the tag — this triggers release.yml
git push origin v0.1.3
```

### Minor release (`0.1.x` → `0.2.0`)

1. Open a PR editing `version.json`:
   ```diff
   - "version": "0.1",
   + "version": "0.2",
   ```
2. Merge to `main`. Patch resets to `0`, so `main` is now at `0.2.0`.
3. Tag and push:
   ```bash
   git checkout main && git pull
   nbgv tag                 # creates v0.2.0
   git push origin v0.2.0
   ```

### Major release (`0.x` → `1.0.0`)

Same flow as minor: edit `version.json` → `"1.0"`, merge, `nbgv tag`, push tag.

### Quick reference

| Action | Command |
|---|---|
| Show current version | `nbgv get-version` |
| Show NuGet version only | `nbgv get-version -v NuGetPackageVersion` |
| Create release tag at HEAD | `nbgv tag` |
| Push tag (fires release workflow) | `git push origin <tag>` |
| Bump minor/major | Edit `version.json`, merge PR |

---

## Version properties during build

nbgv populates these MSBuild properties automatically (readable from any `.csproj`):

| Property | Example |
|---|---|
| `$(Version)` | `0.1.3` |
| `$(AssemblyVersion)` | `0.1.3.0` |
| `$(FileVersion)` | `0.1.3.nnnn` |
| `$(PackageVersion)` | `0.1.3` |
| `$(InformationalVersion)` | `0.1.3+abc1234` |

On non-public branches (anything other than `main` or a `v*` tag), the NuGet/Informational versions get a `-g<sha>` suffix (e.g. `0.1.3-g19a641360c`). This is controlled by `publicReleaseRefSpec` in `version.json`.

From code:

```csharp
var info = typeof(CompasPb.Serializer).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
    .InformationalVersion;
```
