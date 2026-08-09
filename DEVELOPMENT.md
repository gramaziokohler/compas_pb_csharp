# Development Guide

## Versioning & Releases

This repo uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (`nbgv`) to stamp every build with a version derived from `version.json` and git history.

### Scheme

Standard SemVer: `MAJOR.MINOR.PATCH` (e.g. `0.1.0`, `0.2.0`, `1.0.0`).

---

## How to release

The release process mirrors the Python `compas_pb` repo:

### 1. Update the changelog

In `CHANGELOG.md`, write your changes under `## [Unreleased]`:

```markdown
## [Unreleased]

### Features
- Add something new

### Bug Fixes
- Fix something broken
```

### 2. Create a release branch with nbgv

Install nbgv once (if not already):

```bash
dotnet tool install --global nbgv
```

Then prepare the release:

```bash
# on main
nbgv prepare-release
```

This creates a `release/v{major}.{minor}` branch and bumps `version.json` on `main` to the next dev version.

### 3. Update the changelog header on the release branch

Switch to the release branch and replace `## [Unreleased]` with the version and date:

```bash
git checkout release/v0.2
```

Edit `CHANGELOG.md`:
```markdown
## [0.2.0] - 2026-08-09   ← replace [Unreleased] with this
```

Commit:
```bash
git add CHANGELOG.md
git commit -m "chore: update changelog for 0.2.0"
```

### 4. Open a PR and merge into main

Open a PR from `release/v0.2` → `main`. When merged, CI detects the release merge automatically:

- Checks `CHANGELOG.md` has a versioned `## [x.y.z]` entry — fails if only `## [Unreleased]` is present
- Runs build, format check, and tests
- Creates tag `v0.2.0`
- Publishes artifacts and creates the GitHub Release

---

## Quick reference

| Action | Command |
|---|---|
| Show current version | `nbgv get-version` |
| Show NuGet version | `nbgv get-version -v NuGetPackageVersion` |
| Prepare a release | `nbgv prepare-release` |

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
| `build.yml` | Push / PR to `main` | Format check, build, test |
| `release.yml` | Push to `main` (from `release/**` merge) | Tag, publish artifacts, create GitHub Release |
