# Development

## Prerequisites

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Python 3.12+](https://www.python.org/downloads/) (for fetching proto files)

## Build

```bash
dotnet build src/CompasPb/CompasPb.csproj --configuration Release
```

## Run tests

```bash
dotnet test test/CompasPb.Test.csproj --configuration Release --verbosity normal
```

## Run example

```bash
dotnet run --project example/UserCase/CompasPb.UserCase.csproj
```

## Fetch proto files

Proto-generated C# files are fetched from the upstream [compas_pb](https://github.com/gramaziokohler/compas_pb) Python package release:

```bash
python fetch_compas_pb.py
```

## Formatting

This project uses [CSharpier](https://csharpier.com/) for code formatting:

```bash
dotnet tool install -g csharpier
csharpier check .
csharpier format .
```

## Release

### How to release

1. Run the **bump** workflow manually (Actions > release > Run workflow), choose `patch`, `minor`, or `major`
2. The bump action creates a `release/x.y.z` branch with an updated `CHANGELOG.md`
3. Open a PR from `release/x.y.z` into `main`, review, and merge
4. On merge to main, CI checks the merge commit message for `release/` branch name
5. If detected, CI runs build + test, then:
   - Publishes platform artifacts (Windows, macOS)
   - Pushes NuGet package to nuget.org
   - Creates a git tag `vx.y.z`
   - Creates a GitHub Release with zipped artifacts

<!-- ### When release does NOT trigger

Any merge to main from a non-`release/` branch is ignored. The CI checks:

```
git log -1 --pretty=%s  →  "Merge pull request #N from org/release/x.y.z"
```

If the branch name doesn't start with `release/`, the workflow exits with `is_release=false`.

Examples:
- `chore/add-versioning` — skipped
- `fix/something` — skipped
- `refactor/deserializer` — skipped
- `release/1.0.0` — triggers release

### Pipeline

```
workflow_dispatch (bump)
    │
    ▼
release/x.y.z branch created
    │
    ▼
PR merged to main
    │
    ▼
check-release ── is merge from release/*?
    │                     │
    no → skip             yes
                          │
                          ▼
                  build-and-test (windows + macos)
                          │
                    ┌─────┴─────┐
                    ▼           ▼
                publish    nuget-publish
                    │           │
                    └─────┬─────┘
                          ▼
                  release (tag + GitHub Release)
``` -->

---
