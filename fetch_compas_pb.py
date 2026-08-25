"""
Fetch assets from compas_pb repository
"""

from urllib.parse import urlparse
import urllib.request
import urllib.error
import hashlib
import json
import zipfile
from pathlib import Path
import os


compas_pb_version = "1.1.0"
csharp_generator_version = "31.1"
csharp_asset_sha256 = "3062b96f213dcccabd9aa8888b85137d4e277be03ce6644879d45503cc9d5290"
repo_url = "https://github.com/compas-dev/compas_pb"


def _parse_repo_url(url: str):
    """Parse GitHub URL to extract owner and repo name."""
    parsed = urlparse(url)
    parts = parsed.path.strip("/").split("/")
    if len(parts) < 2:
        raise ValueError(f"Invalid repository URL: {url}")
    return parts[0], parts[1]


def _get_release_info(owner: str, repo: str, version: str):
    """Get release information from GitHub API."""
    if version == "latest":
        api_url = f"https://api.github.com/repos/{owner}/{repo}/releases/latest"
    else:
        api_url = (
            f"https://api.github.com/repos/{owner}/{repo}/releases/tags/v{version}"
        )

    github_token = os.getenv("GITHUB_TOKEN")
    req = urllib.request.Request(api_url)

    if github_token:
        req.add_header("Authorization", f"token {github_token}")

    try:
        with urllib.request.urlopen(req) as response:
            info = json.loads(response.read().decode())

        # Write version to file
        release_version = info["tag_name"].lstrip("v")
        output_dir = Path(".") / "resources"
        if not output_dir.exists():
            output_dir.mkdir(parents=True, exist_ok=True)
        _create_version_file(release_version, output_dir)

        return info
    except urllib.error.HTTPError as e:
        print(f"Error fetching release info: {e.code} - {e.reason}")
        raise


def _create_version_file(version: str, output_dir: Path):
    package_info = {
        "name": "compas_pb",
        "version": version,
    }
    version_file_path = Path(output_dir) / "COMPAS_PB_VERSION.json"
    with open(version_file_path, "w") as f:
        json.dump(package_info, f, indent=4)
        f.write("\n")


def _find_compas_asset(assets):
    """Find the exactly pinned generated C# asset."""
    asset_name = f"compas_pb-generated-csharp-{csharp_generator_version}.zip"
    return next((asset for asset in assets if asset["name"] == asset_name), None)


def _validate_digest(archive_path: Path):
    """Validate the archive against the checksum pinned in this repository."""
    actual = hashlib.sha256(archive_path.read_bytes()).hexdigest()
    if actual != csharp_asset_sha256:
        raise ValueError(
            f"Checksum mismatch for {archive_path.name}: "
            f"expected {csharp_asset_sha256}, got {actual}"
        )


def fetch_assets(output_dir):
    """
    Fetch the pinned compas_pb-generated-csharp asset from a pinned GitHub release.

    Args:
        output_dir: Directory to save and extract the asset
    """
    # Parse repository URL
    owner, repo = _parse_repo_url(repo_url)
    print(f"Fetching from: {owner}/{repo}")

    # Get release information
    print(f"Getting release info for version: {compas_pb_version}")
    release_data = _get_release_info(owner, repo, compas_pb_version)

    assets = release_data.get("assets", [])

    if not assets:
        print("No assets found in this release")
        return False

    compas_asset = _find_compas_asset(assets)

    if not compas_asset:
        print(
            "No pinned compas_pb-generated-csharp "
            f"{csharp_generator_version} ZIP file found"
        )
        print("Available assets:")
        for asset in assets:
            print(f"  - {asset['name']}")
        return False

    # Display asset information
    print(f"Found: {compas_asset['name']}")
    print(f"Generator version: {csharp_generator_version}")
    print(f"Size: {compas_asset['size'] / 1024 / 1024:.2f} MB")

    # Download the asset
    archive_path = output_dir / compas_asset["name"]
    print(f"Downloading to: {archive_path}")

    try:
        urllib.request.urlretrieve(compas_asset["browser_download_url"], archive_path)
        _validate_digest(archive_path)
        print("Download complete")
    except Exception as e:
        print(f"Download failed: {e}")
        return False

    print("Extracting...")
    try:
        with zipfile.ZipFile(archive_path, "r") as zip_ref:
            zip_ref.extractall(output_dir)
        print(f"Extracted to: {output_dir}")
    except zipfile.BadZipFile:
        print("Failed to extract: Not a valid ZIP file")
        return False

    print("Cleaning up...")
    archive_path.unlink()
    print("Temporary ZIP file removed")

    return True


if __name__ == "__main__":
    from pathlib import Path
    import shutil
    import tempfile

    output_dir = Path(".") / "src" / "CompasPb" / "Generated"
    with tempfile.TemporaryDirectory(prefix="compas_pb_csharp_") as temp_dir:
        staging_dir = Path(temp_dir)
        success = fetch_assets(output_dir=staging_dir)

        if success:
            if output_dir.exists():
                shutil.rmtree(output_dir)
            shutil.copytree(staging_dir, output_dir)

    if success:
        print("Asset fetch completed successfully!")
    else:
        print("Asset fetch failed")
