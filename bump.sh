#!/bin/bash
# Usage: ./bump.sh [patch|minor|major]
# Mirrors bump-my-version behavior from compas_pb Python repo.
# Reads current version from latest git tag, bumps it,
# updates CHANGELOG.md and version.json, then commits and tags.

set -e

BUMP=${1:-patch}

if [[ "$BUMP" != "patch" && "$BUMP" != "minor" && "$BUMP" != "major" ]]; then
    echo "Usage: ./bump.sh [patch|minor|major]"
    exit 1
fi

# Read current version from latest git tag
CURRENT=$(git tag --list "v*" --sort=-version:refname | head -1)
if [[ -z "$CURRENT" ]]; then
    echo "No version tag found. Create an initial tag first: git tag v0.1.0"
    exit 1
fi

CURRENT="${CURRENT#v}"  # strip leading "v"
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT"

# Bump
case "$BUMP" in
    major) NEW="$((MAJOR + 1)).0.0" ;;
    minor) NEW="${MAJOR}.$((MINOR + 1)).0" ;;
    patch) NEW="${MAJOR}.${MINOR}.$((PATCH + 1))" ;;
esac

TAG="v$NEW"
DATE=$(date +%Y-%m-%d)

echo "Bumping $CURRENT → $NEW ($BUMP)"

# Update version.json
python3 -c "
import json
with open('version.json') as f:
    data = json.load(f)
data['version'] = '$NEW'
with open('version.json', 'w') as f:
    json.dump(data, f, indent=2)
    f.write('\n')
"

# Update CHANGELOG.md — replace ## [Unreleased] with versioned header
if ! grep -q "## \[Unreleased\]" CHANGELOG.md; then
    echo "Warning: No '## [Unreleased]' section found in CHANGELOG.md — skipping changelog update."
else
    sed -i.bak "s/## \[Unreleased\]/## [$NEW] - $DATE/" CHANGELOG.md
    rm -f CHANGELOG.md.bak
fi

# Commit and tag
git add version.json CHANGELOG.md
git commit -m "Bump version to $NEW"
git tag "$TAG"

echo ""
echo "Version bumped to $NEW and tagged as $TAG."
echo "Run: git push --follow-tags"
