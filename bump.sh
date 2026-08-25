#!/bin/bash
# Usage: ./bump.sh [patch|minor|major]
# Creates a release branch from the latest release tag, updates version.json,
# and moves the Unreleased changelog entries under the new release version.

set -euo pipefail

BUMP=${1:-patch}
NBGV_COMMAND=${NBGV_COMMAND:-nbgv}

if [[ "$BUMP" != "patch" && "$BUMP" != "minor" && "$BUMP" != "major" ]]; then
    echo "Usage: ./bump.sh [patch|minor|major]"
    exit 1
fi

if [[ -n "${GITHUB_ACTIONS:-}" && "${GITHUB_REF_NAME:-}" != "main" ]]; then
    echo "ERROR: Run the release preparation workflow from the main branch."
    exit 1
fi

CURRENT_TAG=$(git tag --list | sed -nE 's/^v?([0-9]+\.[0-9]+\.[0-9]+)$/\1/p' | sort -V | tail -1)

if [[ -z "$CURRENT_TAG" ]]; then
    echo "ERROR: No semantic-version release tag found."
    exit 1
fi

IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_TAG"

case "$BUMP" in
    major) NEW_VERSION="$((MAJOR + 1)).0.0" ;;
    minor) NEW_VERSION="${MAJOR}.$((MINOR + 1)).0" ;;
    patch) NEW_VERSION="${MAJOR}.${MINOR}.$((PATCH + 1))" ;;
esac

RELEASE_BRANCH="release/v${NEW_VERSION}"
RELEASE_DATE=$(date +%Y-%m-%d)

if git show-ref --verify --quiet "refs/heads/${RELEASE_BRANCH}"; then
    echo "ERROR: Local branch ${RELEASE_BRANCH} already exists."
    exit 1
fi

if git ls-remote --exit-code --heads origin "$RELEASE_BRANCH" >/dev/null 2>&1; then
    echo "ERROR: Remote branch ${RELEASE_BRANCH} already exists."
    exit 1
fi

if ! grep -q '^## \[Unreleased\]$' CHANGELOG.md; then
    echo "ERROR: CHANGELOG.md has no [Unreleased] section."
    exit 1
fi

if grep -q "^## \[${NEW_VERSION}\]" CHANGELOG.md; then
    echo "ERROR: CHANGELOG.md already contains ${NEW_VERSION}."
    exit 1
fi

git switch -c "$RELEASE_BRANCH"
"$NBGV_COMMAND" set-version "$NEW_VERSION"

CHANGELOG_TMP=$(mktemp)
trap 'rm -f "$CHANGELOG_TMP"' EXIT

awk -v release_header="## [${NEW_VERSION}] - ${RELEASE_DATE}" '
    $0 == "## [Unreleased]" {
        print
        print ""
        print release_header
        next
    }
    { print }
' CHANGELOG.md > "$CHANGELOG_TMP"
mv "$CHANGELOG_TMP" CHANGELOG.md
trap - EXIT

# Restage the Unity package so the release tag carries assemblies built from
# this version. OpenUPM packs the tagged tree as-is; it never runs a build.
PYTHON_COMMAND=${PYTHON_COMMAND:-python3}
"$PYTHON_COMMAND" build_upm.py --validate

git add version.json CHANGELOG.md upm
git commit -m "Prepare release ${NEW_VERSION}"

echo "Prepared ${RELEASE_BRANCH} for CompasPb ${NEW_VERSION}."
