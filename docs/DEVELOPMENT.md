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

[release-please](https://github.com/googleapis/release-please) owns the version. It
reads [Conventional Commits](https://www.conventionalcommits.org/) on `main`, works out
the next version, and keeps a release pull request up to date that writes it into
`version.json` and the Unity `package.json`. `nbgv` then stamps assemblies and packages
from `version.json` and the Git history, exactly as before — it no longer decides
*which* version, only how builds are stamped with it.

---

## How to release

1. Write commits on `main` using Conventional Commits. `feat:` gives a minor bump,
   `fix:` a patch, and a `!` suffix or a `BREAKING CHANGE:` footer gives a major.
   Anything else (`chore:`, `docs:`, `test:`, `ci:`) never triggers a release.

   The `pr-title` workflow enforces this on pull request titles, because a squash merge
   turns the title into the commit message on `main`. If you merge without squashing,
   the individual commit messages are what release-please reads, and those are not
   checked — write them in the same form.
2. release-please keeps a **`chore(main): release X.Y.Z`** pull request open, updating it
   as more commits land. Review it: it carries the version bump in `version.json` and
   `upm/dev.compas.compas-pb/package.json`, plus the generated `CHANGELOG.md` entry. Edit
   the changelog in that PR if you want to reword it.
3. Merge it. That single push to `main` tags `vX.Y.Z`, creates the GitHub release, and
   runs everything that publishes.

To force a specific version regardless of what the commits imply, put a
`Release-As: X.Y.Z` footer in a commit body:

```bash
git commit --allow-empty -m "chore: release 1.0.0" -m "Release-As: 1.0.0"
```

### Bootstrapping the first release-please release

`.release-please-manifest.json` records `0.1.0` as the last released version, and
`bootstrap-sha` points at that tag so the first release pull request does not sweep up
the entire history. `version.json` already reads `1.0.0` because that is the version in
development.

The commits leading to `1.0.0` predate Conventional Commits, so release-please cannot
infer that bump. Cut the first release with an explicit `Release-As: 1.0.0` commit as
shown above; everything after it is inferred normally.

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

It publishes `CompasPb.csproj` for `netstandard2.0` with `PublicRelease=true`, stages
`CompasPb.dll`, `CompasPb.xml`, and `Google.Protobuf.dll` into `Runtime/`, syncs the
package version from `version.json`, and writes
a `.meta` file for every packaged asset. GUIDs are derived from each asset's package
path and any GUID already committed is preserved, so regenerating the package never
invalidates references in consumer projects.

| Flag | Effect |
|---|---|
| `--no-build` | Stage the existing `Release/netstandard2.0` output instead of compiling |
| `--validate` | Fail if the staged layout is not publishable |

`Runtime/` is **not** committed. It is generated into an ignored folder, and the
`upm-publish` job copies the assembled package onto a dedicated `upm` branch and tags it
`upm/vX.Y.Z`. OpenUPM reads those tags, so the compiled assemblies never enter `main`'s
history. The `upm` job in `build.yml` rebuilds and validates the package on every push
and pull request.

`build_upm.py` checks that `package.json` and `version.json` agree rather than syncing
them, because release-please owns both.

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
`BUNDLED_ASSEMBLIES` in `build_upm.py` if a new NuGet dependency has to travel with the
package, and record it in `Third Party Notices.md`.

The dependency set comes from `dotnet publish`, so the SDK decides it rather than this
script reading NuGet's restore output. Every assembly in the publish output must be listed
in either `BUNDLED_ASSEMBLIES` or `UNITY_PROVIDED_ASSEMBLIES`; an unrecognised one fails the
build rather than being silently dropped from the package.

### Submitting to OpenUPM

OpenUPM packs the tree at a tag, so it reads the `upm/vX.Y.Z` tags rather than the
`vX.Y.Z` release tags — those point at `main`, which carries no compiled assemblies.
Submit the package once at [openupm.com/packages/add](https://openupm.com/packages/add/)
with:

| Field | Value |
|---|---|
| Repository | `gramaziokohler/compas_pb_csharp` |
| Package name | `dev.compas.compas-pb` |
| Package folder | repository root (the `upm` branch holds the package at its root) |
| Version tag prefix | `upm/` |

The prefix is a literal filter, so the `vX.Y.Z` release tags are ignored and only the
Unity tags trigger an OpenUPM build. After that, every `upm/vX.Y.Z` tag pushed by
`release.yml` is picked up automatically. The scope `dev.compas` must be registered to
this repository's maintainers.

---

## What CI does on merge

Every push to `main` runs `release.yml`, which starts with release-please. On an
ordinary push it only refreshes the release pull request and stops there — every other
job is gated on its `release_created` output.

On the push that merges a release pull request, release-please tags `vX.Y.Z` and creates
the GitHub release, and the remaining jobs run:

1. Format check, build, and tests on Windows and macOS.
2. Platform release artifacts for `win-x64` and `osx-x64`.
3. `CompasPb.X.Y.Z.nupkg` and its symbol package pushed to NuGet.org.
4. The Unity package built and pushed to the `upm` branch, tagged `upm/vX.Y.Z`.
5. The platform artifacts attached to the GitHub release.

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
| **patch** `1.0.0 → 1.0.1` | Land a `fix:` commit |
| **minor** `1.0.x → 1.1.0` | Land a `feat:` commit |
| **major** `0.x.x → 1.0.0` | Land a `feat!:` commit or a `BREAKING CHANGE:` footer |
| **exact** | Add a `Release-As: X.Y.Z` footer |

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
| `pr-title.yml` | Pull request opened / edited | Enforce a Conventional Commit pull request title |
| `release.yml` | Push to `main` | Maintain the release pull request; on merge, tag, publish to NuGet and the `upm` branch, and attach release assets |
