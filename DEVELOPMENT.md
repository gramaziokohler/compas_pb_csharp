# Development Guide

## Versioning & Releases

This repo uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (`nbgv`) to stamp every build with a version derived from `version.json` and git history.

### Scheme

Standard SemVer: `MAJOR.MINOR.PATCH` (e.g. `0.1.0`, `0.2.0`, `1.0.0`).

### Version source

The version is read from `version.json`:

```json
{ "version": "1.0.0" }
```

The release-preparation workflow calculates the next version from the latest
semantic-version tag and the selected `patch`, `minor`, or `major` increment. It
uses `nbgv set-version` to update `version.json`; `nbgv` then stamps assemblies and
packages consistently from that version and the Git history.

---

## How to release

1. Add every user-visible change to `CHANGELOG.md` under `## [Unreleased]`.
2. Open **Actions → release → Run workflow** on the `main` branch.
3. Select the required `patch`, `minor`, or `major` increment.
4. The workflow creates and pushes `release/vX.Y.Z`, updates `version.json`, and
   moves the unreleased changelog entries under `## [X.Y.Z] - YYYY-MM-DD`.
5. Open a pull request from `release/vX.Y.Z` to `main` and review the version and
   changelog changes.
6. Merge the release pull request. The push to `main` validates, packages,
   publishes, tags, and creates the GitHub release automatically.

For the first stable release from `0.1.0`, select **major** to prepare `1.0.0`.

To prepare a release locally instead of using Actions:

```bash
dotnet tool install --global nbgv --version 3.9.50
bash ./bump.sh major
git push --set-upstream origin release/v1.0.0
```

---

## What CI does on merge

On every push to `main`, `release.yml` compares the first versioned changelog
section with `version.json` and the existing release tags. If the versions match
and neither `vX.Y.Z` nor the legacy `X.Y.Z` tag exists, it automatically:

1. Runs the format check, build, and tests on Windows and macOS.
2. Builds the platform release artifacts.
3. Packs and publishes `CompasPb.X.Y.Z.nupkg` and its symbol package to NuGet.org.
4. Creates and pushes tag `vX.Y.Z`.
5. Creates the GitHub release and attaches the platform artifacts.

Ordinary pushes do not publish when the changelog version differs from
`version.json` or the version already has a release tag.

### NuGet API key

The repository must have a GitHub Actions secret named `NUGET_API_KEY`:

1. Sign in to [nuget.org](https://www.nuget.org/) with the account or organization
   that should own `CompasPb`.
2. Open the account menu, select **API Keys**, and create a key with **Push** scope.
3. Restrict the package glob to `CompasPb` and choose an appropriate expiration.
4. Copy the key immediately; NuGet does not display it again.
5. In GitHub, open **Settings → Secrets and variables → Actions**, create a new
   repository secret named `NUGET_API_KEY`, and paste the key as its value.

Never commit the API key, paste it into an issue or workflow file, or print it in
CI output. Rotate the NuGet key and replace the GitHub secret before it expires.

---

## Quick reference

| Bump type | How |
|---|---|
| **patch** `1.0.0 → 1.0.1` | Run `release` on `main` with `patch` |
| **minor** `1.0.x → 1.1.0` | Run `release` on `main` with `minor` |
| **major** `0.x.x → 1.0.0` | Run `release` on `main` with `major` |

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
| `build.yml` | Push / PR to `main` | Format check, build, test |
| `release.yml` | Manual dispatch | Prepare and push a versioned release branch |
| `release.yml` | Push to `main` | Detect an untagged release, publish it, tag it, and create the GitHub Release |
