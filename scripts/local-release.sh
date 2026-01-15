#!/bin/bash
# Local release script — mirrors the CI release workflow when GitHub Actions credits are unavailable.
# Usage: ./scripts/local-release.sh 1.0.0
#
# Prerequisites:
#   - smctl configured and authenticated (smctl healthcheck shows 2FA)
#   - signtool in PATH
#   - vpk installed (dotnet tool install -g vpk)
#   - gh CLI authenticated (gh auth status)

set -e

VERSION="$1"
if [[ -z "$VERSION" ]] || [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Usage: $0 <version>  (e.g., $0 1.0.0)"
    exit 1
fi

TAG="v$VERSION"
DATE=$(date +%Y-%m-%d)
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

echo "=== Releasing Codeagogo $VERSION ==="

# 1. Check tag doesn't exist
if git rev-parse "$TAG" >/dev/null 2>&1; then
    echo "Error: Tag $TAG already exists"
    exit 1
fi

# 2. Update CHANGELOG.md
echo "Updating CHANGELOG.md..."
sed -i "s/^## \[Unreleased\]/## [Unreleased]\n\n## [$VERSION] - $DATE/" CHANGELOG.md

# 3. Update version in project file
echo "Updating version to $VERSION..."
sed -i "s|<Version>.*</Version>|<Version>$VERSION</Version>|" src/Codeagogo/Codeagogo.csproj

# 4. Commit and tag
echo "Committing release..."
git add CHANGELOG.md src/Codeagogo/Codeagogo.csproj
git commit -m "release: v$VERSION"
git tag "$TAG"

# 5. Build
echo "Building..."
dotnet restore Codeagogo.sln
dotnet build Codeagogo.sln -c Release --no-restore -p:Version="$VERSION"

# 6. Test
echo "Running tests..."
dotnet test Codeagogo.sln -c Release --no-build --filter "Category!=E2E&Category!=Integration"

# 7. Publish
echo "Publishing self-contained..."
dotnet publish src/Codeagogo/Codeagogo.csproj \
    -c Release -r win-x64 --self-contained true \
    -p:Version="$VERSION" -o publish

# 8. Sign (if smctl available)
if command -v smctl &>/dev/null; then
    echo "Signing executable..."
    smctl sign --keypair-alias key_1490215935 --input "publish/Codeagogo.exe" || echo "Warning: signing failed"
else
    echo "Warning: smctl not found, skipping signing"
fi

# 9. Package with Velopack
echo "Packaging with Velopack..."
vpk pack \
    --packId Codeagogo \
    --packVersion "$VERSION" \
    --packDir publish \
    --mainExe Codeagogo.exe \
    --outputDir releases

# 10. Push
echo "Pushing to GitHub..."
git push origin main --tags

# 11. Extract release notes
BODY=$(awk "/^## \[$VERSION\]/{flag=1; next} /^## \[/{flag=0} flag" CHANGELOG.md)
echo "$BODY" > release-notes.md

# 12. Create GitHub Release
echo "Creating GitHub Release..."
"/c/Program Files/GitHub CLI/gh.exe" release create "$TAG" \
    --repo aehrc/codeagogo-win \
    --title "Codeagogo $VERSION" \
    --notes-file release-notes.md \
    releases/*

# Cleanup
rm -f release-notes.md
rm -rf publish

echo ""
echo "=== Release $VERSION complete ==="
echo "GitHub Release: https://github.com/aehrc/codeagogo-win/releases/tag/$TAG"
