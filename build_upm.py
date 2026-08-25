"""
Build the Unity Package Manager (UPM) distribution of CompasPb.

Compiles the netstandard2.0 assembly, stages it together with its bundled
third-party dependency into upm/dev.compas.compas-pb/Runtime, syncs the
package version from the repository, and writes the Unity
.meta files that an immutable registry package must ship with.

Usage:
    python3 build_upm.py [--no-build] [--validate]
"""

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent
PACKAGE_NAME = "dev.compas.compas-pb"
PACKAGE_DIR = REPO_ROOT / "upm" / PACKAGE_NAME
RUNTIME_DIR = PACKAGE_DIR / "Runtime"
PROJECT_FILE = REPO_ROOT / "src" / "CompasPb" / "CompasPb.csproj"
ASSETS_FILE = REPO_ROOT / "src" / "CompasPb" / "obj" / "project.assets.json"
TARGET_FRAMEWORK = "netstandard2.0"
BUILD_OUTPUT = REPO_ROOT / "src" / "CompasPb" / "bin" / "Release" / TARGET_FRAMEWORK

# Assemblies copied out of the NuGet graph into the Unity package. Everything
# else CompasPb depends on is provided by Unity itself: Newtonsoft.Json comes
# from com.unity.nuget.newtonsoft-json, and System.Memory / System.Buffers /
# System.Numerics.Vectors / System.Runtime.CompilerServices.Unsafe are part of
# the Unity 2021.3+ class libraries. Shipping those would duplicate assemblies.
BUNDLED_PACKAGES = ["Google.Protobuf"]

# Unity importer used per file extension when generating .meta files.
IMPORTERS = {
    ".dll": "PluginImporter",
    ".xml": "TextScriptImporter",
    ".json": "TextScriptImporter",
    ".md": "DefaultImporter",
}


def run(command, **kwargs):
    print(f"$ {' '.join(str(part) for part in command)}")
    subprocess.run(command, check=True, cwd=REPO_ROOT, **kwargs)


def read_version() -> str:
    """Return the release version from version.json, without any prerelease tag."""
    version = json.loads((REPO_ROOT / "version.json").read_text())["version"]
    return version.split("-")[0]


def ensure_generated_sources():
    """Download the pinned compas_pb C# bindings when they are not present."""
    generated = REPO_ROOT / "src" / "CompasPb" / "Generated"
    if generated.is_dir() and any(generated.glob("*.cs")):
        return
    run([sys.executable, "fetch_compas_pb.py"])


def build_assembly():
    ensure_generated_sources()
    run(
        [
            "dotnet",
            "build",
            str(PROJECT_FILE),
            "--configuration",
            "Release",
            "--framework",
            TARGET_FRAMEWORK,
            # Matches the NuGet pack step so the staged assembly carries the
            # release version rather than a branch-local prerelease suffix.
            "-p:PublicRelease=true",
        ]
    )


def resolve_bundled_assemblies() -> list[Path]:
    """Resolve the NuGet dependency assemblies to bundle from the restore graph."""
    if not ASSETS_FILE.is_file():
        raise SystemExit(
            f"ERROR: {ASSETS_FILE.relative_to(REPO_ROOT)} is missing. "
            "Run 'dotnet restore' first."
        )

    assets = json.loads(ASSETS_FILE.read_text())
    target = assets["targets"][".NETStandard,Version=v2.0"]
    folders = [Path(folder) for folder in assets["packageFolders"]]

    resolved = []
    for name in BUNDLED_PACKAGES:
        entry = next(
            (key for key in target if key.split("/")[0].lower() == name.lower()), None
        )
        if entry is None:
            raise SystemExit(f"ERROR: {name} is not part of the restore graph.")

        library = assets["libraries"][entry]
        runtime_items = [
            item for item in target[entry].get("runtime", {}) if not item.endswith("_._")
        ]
        if not runtime_items:
            raise SystemExit(f"ERROR: {entry} contributes no runtime assembly.")

        for item in runtime_items:
            candidates = [folder / library["path"] / item for folder in folders]
            match = next((path for path in candidates if path.is_file()), None)
            if match is None:
                raise SystemExit(f"ERROR: could not locate {item} for {entry}.")
            resolved.append(match)

    return resolved


def stage_runtime() -> list[str]:
    RUNTIME_DIR.mkdir(parents=True, exist_ok=True)

    sources = [BUILD_OUTPUT / "CompasPb.dll", BUILD_OUTPUT / "CompasPb.xml"]
    missing = [path for path in sources if not path.is_file()]
    if missing:
        raise SystemExit(
            "ERROR: missing build output: "
            + ", ".join(str(path.relative_to(REPO_ROOT)) for path in missing)
            + ". Build the project first, or drop --no-build."
        )

    sources += resolve_bundled_assemblies()
    expected = {path.name for path in sources}

    for path in sources:
        shutil.copy2(path, RUNTIME_DIR / path.name)
        print(f"staged Runtime/{path.name}")

    for stale in RUNTIME_DIR.iterdir():
        if stale.name.endswith(".meta"):
            continue
        if stale.name not in expected:
            stale.unlink()
            (RUNTIME_DIR / f"{stale.name}.meta").unlink(missing_ok=True)
            print(f"removed stale Runtime/{stale.name}")

    return sorted(expected)


def sync_package_json(version: str):
    manifest_path = PACKAGE_DIR / "package.json"
    manifest = json.loads(manifest_path.read_text())
    if manifest["version"] != version:
        manifest["version"] = version
        manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
        print(f"set package.json version to {version}")


def stable_guid(relative_path: str) -> str:
    """Derive a deterministic GUID so regenerating metas never breaks references."""
    return hashlib.md5(f"{PACKAGE_NAME}/{relative_path}".encode()).hexdigest()


def meta_body(path: Path, guid: str) -> str:
    if path.is_dir():
        return (
            f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
            "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n"
        )

    if path.name == "package.json":
        return (
            f"fileFormatVersion: 2\nguid: {guid}\n"
            "PackageManifestImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n"
        )

    if path.suffix == ".dll":
        # Enabled everywhere except the editor-only and WSA slots, matching what
        # Unity writes for a managed plugin imported with default settings.
        return (
            f"fileFormatVersion: 2\nguid: {guid}\n"
            "PluginImporter:\n"
            "  externalObjects: {}\n"
            "  serializedVersion: 2\n"
            "  iconMap: {}\n"
            "  executionOrder: {}\n"
            "  defineConstraints: []\n"
            "  isPreloaded: 0\n"
            "  isOverridable: 0\n"
            "  isExplicitlyReferenced: 0\n"
            "  validateReferences: 1\n"
            "  platformData:\n"
            "  - first:\n"
            "      Any: \n"
            "    second:\n"
            "      enabled: 1\n"
            "      settings: {}\n"
            "  - first:\n"
            "      Editor: Editor\n"
            "    second:\n"
            "      enabled: 0\n"
            "      settings:\n"
            "        DefaultValueInitialized: true\n"
            "  - first:\n"
            "      Windows Store Apps: WindowsStoreApps\n"
            "    second:\n"
            "      enabled: 0\n"
            "      settings:\n"
            "        CPU: AnyCPU\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )

    importer = IMPORTERS.get(path.suffix, "DefaultImporter")
    return (
        f"fileFormatVersion: 2\nguid: {guid}\n"
        f"{importer}:\n  externalObjects: {{}}\n  userData: \n"
        "  assetBundleName: \n  assetBundleVariant: \n"
    )


def existing_guid(meta_path: Path) -> str | None:
    """Return the GUID recorded in a .meta file, ignoring malformed ones."""
    if not meta_path.is_file():
        return None
    for line in meta_path.read_text().splitlines():
        if line.startswith("guid:"):
            guid = line.split(":", 1)[1].strip()
            if len(guid) == 32 and all(c in "0123456789abcdef" for c in guid):
                return guid
    return None


def write_metas():
    """Write a .meta for every packaged asset, keeping any GUID already assigned."""
    for path in sorted(PACKAGE_DIR.rglob("*")):
        if path.name.endswith(".meta"):
            continue

        relative = path.relative_to(PACKAGE_DIR).as_posix()
        meta_path = path.with_name(f"{path.name}.meta")
        guid = existing_guid(meta_path) or stable_guid(relative)
        body = meta_body(path, guid)

        if not meta_path.is_file() or meta_path.read_text() != body:
            meta_path.write_text(body)
            print(f"wrote {relative}.meta")

    for meta_path in sorted(PACKAGE_DIR.rglob("*.meta")):
        if not meta_path.with_name(meta_path.name[: -len(".meta")]).exists():
            meta_path.unlink()
            print(f"removed orphaned {meta_path.relative_to(PACKAGE_DIR).as_posix()}")


def validate(version: str, staged: list[str]) -> list[str]:
    """Check the invariants OpenUPM and the Unity Package Manager rely on."""
    problems = []
    manifest = json.loads((PACKAGE_DIR / "package.json").read_text())

    if manifest["name"] != PACKAGE_NAME:
        problems.append(
            f"package.json name {manifest['name']!r} does not match the "
            f"folder name {PACKAGE_NAME!r}"
        )
    if manifest["version"] != version:
        problems.append(
            f"package.json version {manifest['version']} does not match "
            f"version.json {version}"
        )

    # The changelog and license are served through changelogUrl / licensesUrl
    # rather than bundled. Third Party Notices.md must ship: the BSD-3-Clause
    # terms of the redistributed Google.Protobuf require the notice to travel
    # with the binary.
    required_files = ["README.md", "Third Party Notices.md"]
    for required in required_files:
        if not (PACKAGE_DIR / required).is_file():
            problems.append(f"missing {required}")

    for assembly in ["CompasPb.dll", "Google.Protobuf.dll"]:
        if assembly not in staged:
            problems.append(f"Runtime/{assembly} was not staged")

    guids = {}
    for path in sorted(PACKAGE_DIR.rglob("*")):
        relative = path.relative_to(PACKAGE_DIR).as_posix()
        if path.name.endswith(".meta"):
            if not path.with_name(path.name[: -len(".meta")]).exists():
                problems.append(f"orphaned meta file {relative}")
            continue

        guid = existing_guid(path.with_name(f"{path.name}.meta"))
        if guid is None:
            problems.append(f"{relative} has no .meta file carrying a valid guid")
        elif guid in guids:
            problems.append(f"{relative} reuses the guid of {guids[guid]}")
        else:
            guids[guid] = relative

    return problems


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--no-build",
        action="store_true",
        help="stage the existing Release build instead of compiling first",
    )
    parser.add_argument(
        "--validate",
        action="store_true",
        help="verify the staged package layout before publishing",
    )
    args = parser.parse_args()

    if not args.no_build:
        build_assembly()

    version = read_version()
    staged = stage_runtime()
    sync_package_json(version)
    write_metas()

    print(f"\n{PACKAGE_NAME} {version} staged in {PACKAGE_DIR.relative_to(REPO_ROOT)}")
    print("Runtime: " + ", ".join(staged))

    if args.validate:
        problems = validate(version, staged)
        if problems:
            print("\nERROR: the UPM package is not publishable:")
            for problem in problems:
                print(f"  - {problem}")
            return 1
        print("UPM package layout is valid.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
