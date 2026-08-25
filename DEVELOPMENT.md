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

## The Unity package

`upm/dev.compas.compas-pb` is the Unity Package Manager distribution published
to [OpenUPM](https://openupm.com/). OpenUPM packs the tagged tree exactly as it is
committed and never runs a build, so the compiled assemblies under `Runtime/` are
tracked in git.

`build_upm.py` produces that folder:

```bash
python3 build_upm.py
```

It compiles `CompasPb.csproj` for `netstandard2.0` with `PublicRelease=true`, stages
`CompasPb.dll`, `CompasPb.xml`, and `Google.Protobuf.dll` into `Runtime/`, syncs the
package version from `version.json`, and writes
a `.meta` file for every packaged asset. GUIDs are derived from each asset's package
path and any GUID already committed is preserved, so regenerating the package never
invalidates references in consumer projects.

| Flag | Effect |
|---|---|
| `--no-build` | Stage the existing `Release/netstandard2.0` output instead of compiling |
| `--validate` | Fail if the staged layout is not publishable |

`bump.sh` runs `build_upm.py --validate` and commits `upm/` alongside `version.json`
and `CHANGELOG.md`, so a release branch always carries assemblies built from its own
version. The `check-release` job refuses to release when `package.json` and the
changelog version disagree, and the `upm` job in `build.yml` rebuilds and validates
the package on every push and pull request.

The changelog and license are not bundled. `changelogUrl` and `licensesUrl` in
`package.json` point at the repository copies instead, so there is nothing to keep
in sync. `Third Party Notices.md` does ship: the BSD-3-Clause terms of the
redistributed `Google.Protobuf` require the notice to accompany the binary.

### Which assemblies ship

Only `Google.Protobuf` is redistributed. `Newtonsoft.Json` is declared as the
`com.unity.nuget.newtonsoft-json` package dependency, and `System.Memory`,
`System.Buffers`, `System.Numerics.Vectors`, and
`System.Runtime.CompilerServices.Unsafe` are part of the Unity 2021.3+ class
libraries. Bundling any of them would produce duplicate-assembly errors. Adjust
`BUNDLED_PACKAGES` in `build_upm.py` if a new NuGet dependency has to travel with
the package, and record it in `Third Party Notices.md`.

### Submitting to OpenUPM

OpenUPM builds from this repository's release tags. Submit the package once at
[openupm.com/packages/add](https://openupm.com/packages/add/) with:

| Field | Value |
|---|---|
| Repository | `gramaziokohler/compas_pb_csharp` |
| Package name | `dev.compas.compas-pb` |
| Package folder | `upm/dev.compas.compas-pb` |
| Version tag prefix | none (tags are `vX.Y.Z`) |

After that, every `vX.Y.Z` tag pushed by `release.yml` is picked up automatically.
The scope `dev.compas` must be registered to this repository's maintainers.

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

### NuGet Trusted Publishing

NuGet publication uses GitHub OIDC and NuGet.org Trusted Publishing. The workflow
does not store a long-lived API key; `NuGet/login` exchanges the job's OIDC token
for a temporary NuGet API key immediately before the package push.

Configure the trusted publisher on NuGet.org:

1. Sign in to [nuget.org](https://www.nuget.org/) with the account that should own
   `CompasPb`, then open **Trusted Publishing** from the account menu.
2. Add a GitHub Actions policy owned by the appropriate NuGet user or organization.
3. Enter these case-insensitive policy values:

   | Policy field | Value |
   |---|---|
   | Repository owner | `gramaziokohler` |
   | Repository | `compas_pb_csharp` |
   | Workflow file | `release.yml` |
   | Environment | `release` |

   Enter only the workflow filename, not `.github/workflows/release.yml`.
4. In the GitHub repository, open **Settings → Secrets and variables → Actions →
   Variables** and create `NUGET_USER`. Set it to the NuGet.org profile name that
   authenticates the publication, not an email address. The policy itself may be
   owned by that user or by an organization they belong to.
5. Optionally configure protection rules or required reviewers for the GitHub
   `release` environment.

The `nuget-publish` job has only `contents: read` and `id-token: write` permissions.
NuGet.org validates the repository, workflow, environment, and policy owner before
issuing a temporary credential. No `NUGET_API_KEY` repository secret is required.

For the first publication, the `CompasPb` package ID must still be unowned on NuGet.org,
and the NuGet user identified by `NUGET_USER` must be the intended package owner. The
first successful push claims that ID. A subsequent owner or organization can be added in
the package's NuGet.org management page.

For some repositories, a new trusted-publishing policy is temporarily active for
seven days until its first successful publication establishes the immutable GitHub
repository and owner IDs. Restart that activation window in NuGet.org if it expires
before the release runs.

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
| `build.yml` | Push / PR to `main` | Build and validate the Unity package |
| `release.yml` | Manual dispatch | Prepare and push a versioned release branch |
| `release.yml` | Push to `main` | Detect an untagged release, publish it, tag it, and create the GitHub Release |
