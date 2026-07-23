#!/usr/bin/env bash
set -euo pipefail

repository_root="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"
browser_project="$repository_root/src/Calculator.Browser/Calculator.Browser.csproj"
expected_emscripten_version="3.1.56"
expected_emscripten_revision="57b21b8fdcbe3ebb523178b79465254668eab408"

msbuild_property() {
  dotnet msbuild "$browser_project" \
    -nologo \
    "-getProperty:$1" \
    -property:Configuration=Release
}

actual_emscripten_version="$(msbuild_property EmscriptenVersion)"
if [[ "$actual_emscripten_version" != "$expected_emscripten_version" ]]; then
  echo "Expected Emscripten $expected_emscripten_version; found $actual_emscripten_version." >&2
  exit 1
fi
actual_emscripten_revision="$(msbuild_property EmscriptenRevision)"
if [[ "$actual_emscripten_revision" != "$expected_emscripten_revision" ]]; then
  echo "Expected Emscripten revision $expected_emscripten_revision; found $actual_emscripten_revision." >&2
  exit 1
fi

dotnet_cache_root="$(realpath "$(msbuild_property EmscriptenCacheSdkCacheDir)")"
wasm_cache_root="$repository_root/build/emscripten-cache/$expected_emscripten_version"
mkdir -p "$wasm_cache_root"
rsync -a --ignore-existing "$dotnet_cache_root/" "$wasm_cache_root/"

dotnet publish "$browser_project" \
  --configuration Release \
  -p:WasmCachePath="$wasm_cache_root" \
  "$@"
