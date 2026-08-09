# Development Guide

## Versioning & Releases

This repo uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (`nbgv`) to stamp every build with a version derived from `version.json` and git history.

### Scheme

Standard SemVer: `MAJOR.MINOR.PATCH` (e.g. `0.1.0`, `0.2.0`, `1.0.0`).

### Version source

The version is read from `version.json`:

```json
{ "version": "0.1.0" }
```

`nbgv prepare-release` always does a **minor bump** on main automatically:

```
version.json = "0.1.0"
                  ↓
nbgv prepare-release
                  ↓
release/v0.1  →  stays at 0.1.0  ← becomes the release
main          →  bumps to 0.2     ← next dev cycle
```

---

## How to release

### Minor release — `0.1.x → 0.2.0` (default)

`nbgv prepare-release` handles this automatically.

1. Update `CHANGELOG.md` on `main` under `## [Unreleased]`
2. Run on `main`:
   ```bash
   nbgv prepare-release
   # creates release/v0.2, bumps main to 0.2
   ```
3. On the release branch, replace `## [Unreleased]` with the version:
   ```bash
   git checkout release/v0.2
   # edit CHANGELOG.md: ## [0.2.0] - 2026-08-09
   git add CHANGELOG.md && git commit -m "chore: update changelog for 0.2.0"
   ```
4. Open PR `release/v0.2` → `main` and merge → CI tags and publishes automatically

---

### Patch release — `0.1.0 → 0.1.1`

Edit `version.json` manually on the release branch before merging:

1. Update `CHANGELOG.md` on `main` under `## [Unreleased]`
2. Run on `main`:
   ```bash
   nbgv prepare-release
   # creates release/v0.1
   ```
3. On the release branch, bump the patch in `version.json`:
   ```bash
   git checkout release/v0.1
   # edit version.json: "version": "0.1.1"
   # edit CHANGELOG.md: ## [0.1.1] - 2026-08-09
   git add version.json CHANGELOG.md && git commit -m "chore: update changelog for 0.1.1"
   ```
4. Open PR `release/v0.1` → `main` and merge → CI tags `v0.1.1` and publishes

---

### Major release — `0.x.x → 1.0.0`

Edit `version.json` on `main` before running `nbgv prepare-release`:

1. Update `CHANGELOG.md` on `main` under `## [Unreleased]`
2. Edit `version.json` on `main` to the new major:
   ```bash
   # edit version.json: "version": "1.0.0"
   git add version.json && git commit -m "chore: bump major to 1.0.0"
   ```
3. Run on `main`:
   ```bash
   nbgv prepare-release
   # creates release/v1.0
   ```
4. On the release branch, update the changelog:
   ```bash
   git checkout release/v1.0
   # edit CHANGELOG.md: ## [1.0.0] - 2026-08-09
   git add CHANGELOG.md && git commit -m "chore: update changelog for 1.0.0"
   ```
5. Open PR `release/v1.0` → `main` and merge → CI tags `v1.0.0` and publishes

---

## What CI does on merge

When a `release/**` branch merges into `main`, `release.yml` automatically:

1. Checks `CHANGELOG.md` has a versioned `## [x.y.z]` entry — **fails** if only `## [Unreleased]` is present
2. Runs format check, build, and tests across `netstandard2.0`, `net48` (Windows only), `net9.0`
3. Publishes NuGet package to [nuget.org](https://www.nuget.org/packages/CompasPb) (requires `NUGET_API_KEY` secret)
4. Publishes self-contained binaries for Windows (`win-x64`) and macOS (`osx-x64`)
5. Creates tag `v{version}` from the changelog entry
6. Creates GitHub Release with zipped binaries and `CHANGELOG.md` as release notes

---

## NuGet publishing

### First-time setup

1. Create an API key on [nuget.org](https://www.nuget.org/account/apikeys) scoped to the `CompasPb` package
2. Add it to the repo: **Settings → Secrets and variables → Actions → New repository secret**
   - Name: `NUGET_API_KEY`
   - Value: the key from nuget.org

### What gets packed

`dotnet pack` produces two files in the `nupkg/` folder:

| File | Contents |
|---|---|
| `CompasPb.{version}.nupkg` | Library DLLs for `netstandard2.0`, `net48`, `net9.0` + README |
| `CompasPb.{version}.snupkg` | PDB symbols for source-stepping |

Both are pushed to nuget.org automatically on release merge.

### Local pack (dry-run)

```bash
cd src/CompasPb
dotnet pack --configuration Release -o /tmp/nupkg
# inspect contents
unzip -l /tmp/nupkg/CompasPb.*.nupkg
```

---

## Quick reference

| Bump type | How |
|---|---|
| **minor** `0.1 → 0.2` | `nbgv prepare-release` on main (automatic) |
| **patch** `0.1.0 → 0.1.1` | `nbgv prepare-release` + edit `version.json` on release branch |
| **major** `0.x → 1.0` | Edit `version.json` on main first, then `nbgv prepare-release` |

| Action | Command |
|---|---|
| Show current version | `nbgv get-version` |
| Show NuGet version | `nbgv get-version -v NuGetPackageVersion` |
| Install nbgv | `dotnet tool install --global nbgv` |

---

## Version properties during build

nbgv populates these MSBuild properties automatically:

| Property | Example |
|---|---|
| `$(Version)` | `0.2.0` |
| `$(AssemblyVersion)` | `0.2.0.0` |
| `$(FileVersion)` | `0.2.0` |
| `$(InformationalVersion)` | `0.2.0+abc1234` |

On non-release branches, versions get a `-g<sha>` suffix (e.g. `0.2.0-gd9f645a`), controlled by `publicReleaseRefSpec` in `version.json`.

---

## CI Workflows

| Workflow | Trigger | Purpose |
|---|---|---|
| `build.yml` | Push / PR to `main` | Format check, build all TFMs, test on `net9.0` |
| `release.yml` (manual dispatch) | Manual — choose bump type | Runs `nbgv prepare-release`, pushes `release/v*` branch |
| `release.yml` (push to `main`) | Merge of `release/**` → `main` | Checks changelog, builds, tests, packs NuGet, publishes binaries, tags, creates GitHub Release |
