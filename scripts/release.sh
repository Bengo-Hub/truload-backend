#!/bin/bash

# TruLoad Release Script
# This script is run after a successful build and deployment to determine
# the next version tag, create it, and push it to the repository.

set -e

APP_NAME=$1
if [ -z "$APP_NAME" ]; then
  echo "Usage: ./release.sh <app-name>"
  exit 1
fi

echo "Starting release process for $APP_NAME..."

# Use pre-computed version from build pipeline if available (set by build.sh),
# otherwise compute from git tags (fallback for manual runs).
if [ -n "${APP_VERSION:-}" ]; then
  NEW_TAG="v${APP_VERSION}"
  echo "Using pre-computed version: $NEW_TAG"
else
  # Get the latest tag
  LATEST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "v1.0.0")
  echo "Latest tag: $LATEST_TAG"

  # Parse the version numbers
  VERSION=$(echo $LATEST_TAG | sed 's/v//')
  MAJOR=$(echo $VERSION | cut -d. -f1)
  MINOR=$(echo $VERSION | cut -d. -f2)
  PATCH=$(echo $VERSION | cut -d. -f3)

  # Increment patch version (default logic)
  NEW_PATCH=$((PATCH + 1))
  NEW_TAG="v$MAJOR.$MINOR.$NEW_PATCH"

  echo "New version: $NEW_TAG"
fi

# Tag the current commit
git config user.name "${GIT_USER:-TruLoad Bot}"
git config user.email "${GIT_EMAIL:-dev@truload.io}"

git tag -a "$NEW_TAG" -m "Release $NEW_TAG for $APP_NAME"
git push origin "$NEW_TAG"

echo "Successfully tagged and pushed $NEW_TAG"
echo "NEW_VERSION=$NEW_TAG" >> $GITHUB_ENV
