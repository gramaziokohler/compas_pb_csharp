"""
Fetch assets from compas_pb repository
"""

from urllib.parse import urlparse
import urllib.request
import urllib.error
import json
import zipfile
import re
from pathlib import Path


compas_pb_version = "latest"
repo_url = "https://github.com/gramaziokohler/compas_pb"


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

    try:
        with urllib.request.urlopen(api_url) as response:
            info = json.loads(response.read().decode())
        # Write version to file
        release_version = info["tag_name"].lstrip("v")
        _create_version_file(release_version, Path(".") / "Resources")

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


def _find_compas_asset(assets):
    """Find the compas_pb-generated-csharp ZIP asset."""
    pattern = r"compas_pb-generated-csharp-(\d+\.\d+)\.zip"

    compas_asset = None
    latest_version = None

    for asset in assets:
        match = re.search(pattern, asset["name"])
        if match:
            version = float(match.group(1))
            if latest_version is None or version > latest_version:
                latest_version = version
                compas_asset = asset

    return compas_asset, latest_version


def fetch_assets(output_dir):
    """
    Fetch the latest compas_pb-generated-csharp asset from GitHub releases.

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

    compas_asset, version = _find_compas_asset(assets)

    if not compas_asset:
        print("No compas_pb-generated-csharp ZIP file found")
        print("Available assets:")
        for asset in assets:
            print(f"  - {asset['name']}")
        return False

    # Display asset information
    print(f"Found: {compas_asset['name']}")
    print(f"Version: {version}")
    print(f"Size: {compas_asset['size'] / 1024 / 1024:.2f} MB")

    # Download the asset
    archive_path = output_dir / compas_asset["name"]
    print(f"Downloading to: {archive_path}")

    try:
        urllib.request.urlretrieve(compas_asset["browser_download_url"], archive_path)
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

    output_dir = Path(".") / "src" / "CompasPb" / "Generated"
    if output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    success = fetch_assets(output_dir=output_dir)

    if success:
        print("Asset fetch completed successfully!")
    else:
        print("Asset fetch failed")
