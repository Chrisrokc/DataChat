#!/usr/bin/env bash
# Publishes self-contained single-file builds of DataChat.Web for macOS and Linux hosts.
# Outputs land in build/out/<rid>/. Intended to be called from the repo root or the build/ dir.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$REPO_ROOT/src/Presentation/DataChat.Web/DataChat.Web.csproj"
OUT_BASE="$REPO_ROOT/build/out"

RIDS=("${@:-osx-arm64 osx-x64 win-x64}")
# If user passed nothing, treat the default string as a space-separated list
if [ "$#" -eq 0 ]; then
  RIDS=(osx-arm64 osx-x64 win-x64)
fi

echo "==> Publishing DataChat.Web for RIDs: ${RIDS[*]}"

for rid in "${RIDS[@]}"; do
  out_dir="$OUT_BASE/$rid"
  echo "--> $rid -> $out_dir"
  rm -rf "$out_dir"
  dotnet publish "$PROJECT" \
    -c Release \
    -r "$rid" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=embedded \
    -o "$out_dir"
done

echo "==> Done. Artifacts in $OUT_BASE/"
