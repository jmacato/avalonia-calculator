#!/usr/bin/env bash
set -euo pipefail

repository_root="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"
skiasharp_root="${SKIASHARP_ROOT:?Set SKIASHARP_ROOT to a clean SkiaSharp v4.150.1 checkout.}"
skia_root="$skiasharp_root/externals/skia"
dawn_root="$skia_root/third_party/externals/dawn"
native_root="$repository_root/src/Calculator.Browser/Native"
bridge_root="$native_root/Graphite"
out_root="$skia_root/out/calcneo-wasm-graphite"
emdawn_build_root="$skia_root/out/calcneo-emdawnwebgpu"
browser_project="$repository_root/src/Calculator.Browser/Calculator.Browser.csproj"

expected_skiasharp_commit="c3e4f4c20e1f23ab74d31a8838a5bd6dc55365f2"
expected_skia_commit="0aa2d542e833ffd1d4d1b68a5152375e7f65ce11"
expected_emscripten_version="3.1.56"
expected_emscripten_revision="57b21b8fdcbe3ebb523178b79465254668eab408"

msbuild_property() {
  dotnet msbuild "$browser_project" \
    -nologo \
    "-getProperty:$1" \
    -property:Configuration=Release
}

verify_checkout() {
  local name="$1"
  local checkout="$2"
  local expected_commit="$3"
  local actual_commit

  actual_commit="$(git -C "$checkout" rev-parse HEAD)"
  if [[ "$actual_commit" != "$expected_commit" ]]; then
    echo "Expected $name $expected_commit; found $actual_commit." >&2
    exit 1
  fi
  if [[ -n "$(git -C "$checkout" status --porcelain --untracked-files=no)" ]]; then
    echo "$name has tracked modifications: $checkout" >&2
    exit 1
  fi
}

verify_checkout SkiaSharp "$skiasharp_root" "$expected_skiasharp_commit"
verify_checkout mono/skia "$skia_root" "$expected_skia_commit"

# Resolve the complete Emscripten installation from the same evaluated .NET
# project that performs the final native link. This keeps Skia, Emdawn, the
# bridge, Mono, and the generated JavaScript on one ABI without modifying any
# installed workload file.
dotnet_emscripten_tools="$(realpath "$(msbuild_property EmscriptenSdkToolsPath)")"
dotnet_node_tools="$(realpath "$(msbuild_property EmscriptenNodeToolsPath)")"
dotnet_python_tools="$(realpath "$(msbuild_property EmscriptenPythonToolsPath)")"
dotnet_cache_root="$(realpath "$(msbuild_property EmscriptenCacheSdkCacheDir)")"
emscripten_root="$dotnet_emscripten_tools/emscripten"
llvm_root="$dotnet_emscripten_tools/bin"
emcc="$emscripten_root/emcc"
emxx="$emscripten_root/em++"
emar="$emscripten_root/emar"
llvm_ar="$llvm_root/llvm-ar"
emscripten_node="$dotnet_node_tools/bin/node"
emscripten_python="$dotnet_python_tools/bin/python3"
workload_cache_root="$repository_root/build/emscripten-cache/$expected_emscripten_version"
dotnet_emsdk_layout="$repository_root/build/dotnet-emscripten-layout/$expected_emscripten_version"

for required_path in \
  "$emcc" \
  "$emxx" \
  "$emar" \
  "$llvm_ar" \
  "$emscripten_node" \
  "$emscripten_python"; do
  if [[ ! -x "$required_path" ]]; then
    echo "Required Emscripten tool does not exist: $required_path" >&2
    exit 1
  fi
done

actual_emscripten_version="$(tr -d '[:space:]\"' < "$emscripten_root/emscripten-version.txt")"
if [[ "$actual_emscripten_version" != "$expected_emscripten_version" ]]; then
  echo "Expected Emscripten $expected_emscripten_version; found $actual_emscripten_version." >&2
  exit 1
fi
actual_emscripten_revision="$(tr -d '[:space:]' < "$emscripten_root/emscripten-revision.txt")"
if [[ "$actual_emscripten_revision" != "$expected_emscripten_revision" ]]; then
  echo "Expected Emscripten revision $expected_emscripten_revision; found $actual_emscripten_revision." >&2
  exit 1
fi

mkdir -p "$workload_cache_root" "$dotnet_emsdk_layout/upstream/emscripten"
rsync -a --ignore-existing "$dotnet_cache_root/" "$workload_cache_root/"
ln -sfn "$workload_cache_root" "$dotnet_emsdk_layout/upstream/emscripten/cache"

export DOTNET_EMSCRIPTEN_LLVM_ROOT="$llvm_root"
export DOTNET_EMSCRIPTEN_BINARYEN_ROOT="$dotnet_emscripten_tools"
export DOTNET_EMSCRIPTEN_NODE_JS="$emscripten_node"
export EMSDK_PYTHON="$emscripten_python"
export EM_CACHE="$workload_cache_root"
export EM_FROZEN_CACHE=0

sync_dependency() {
  local relative_path="$1"
  local repository="$2"
  local commit="$3"
  local checkout="$skia_root/$relative_path"

  if [[ ! -d "$checkout/.git" ]]; then
    git clone --quiet --depth 1 --no-checkout "$repository" "$checkout"
  fi
  if ! git -C "$checkout" cat-file -e "$commit^{commit}" 2>/dev/null; then
    git -C "$checkout" fetch --quiet --depth 1 "$repository" "$commit"
  fi
  git -C "$checkout" checkout --quiet --detach "$commit"
}

python3 "$skia_root/tools/git-sync-deps"

# mono/skia intentionally leaves optional Dawn dependencies out of its default
# sync. Fetch m150's exact revisions without editing DEPS or either source tree.
sync_dependency third_party/externals/dawn https://dawn.googlesource.com/dawn.git 620a520f5029e14b57a0b58096c022e339b1857b
sync_dependency third_party/externals/abseil-cpp https://chromium.googlesource.com/chromium/src/third_party/abseil-cpp 526428fa2488f4b10406f697108659bc1619a97b
sync_dependency third_party/externals/jinja2 https://chromium.googlesource.com/chromium/src/third_party/jinja2 c3027d884967773057bf74b957e3fea87e5df4d7
sync_dependency third_party/externals/markupsafe https://chromium.googlesource.com/chromium/src/third_party/markupsafe 4256084ae14175d38a3ff7d739dca83ae49ccec6
sync_dependency third_party/externals/egl-registry https://skia.googlesource.com/external/github.com/KhronosGroup/EGL-Registry b055c9b483e70ecd57b3cf7204db21f5a06f9ffe
sync_dependency third_party/externals/glslang https://chromium.googlesource.com/external/github.com/KhronosGroup/glslang 1d47ffa8ac4374a19b302021e216a20f22a3de92
sync_dependency third_party/externals/opengl-registry https://skia.googlesource.com/external/github.com/KhronosGroup/OpenGL-Registry 14b80ebeab022b2c78f84a573f01028c96075553
sync_dependency third_party/externals/spirv-headers https://skia.googlesource.com/external/github.com/KhronosGroup/SPIRV-Headers.git f88a2d766840fc825af1fc065977953ba1fa4a91
sync_dependency third_party/externals/spirv-tools https://skia.googlesource.com/external/github.com/KhronosGroup/SPIRV-Tools.git 7d8d9e58c384949f1615c069d4c9346bf51b9738
sync_dependency third_party/externals/swiftshader https://swiftshader.googlesource.com/SwiftShader 313545f85af72f954820e54f4110cda591a6cf7b
sync_dependency third_party/externals/vulkan-utility-libraries https://chromium.googlesource.com/external/github.com/KhronosGroup/Vulkan-Utility-Libraries 20fb10eb1ec08ccd5cacec32b7df1b0e99e48a0c
sync_dependency third_party/externals/webgpu-headers https://chromium.googlesource.com/external/github.com/webgpu-native/webgpu-headers 706853a9da45b8e89b7ea005aa267294d115f8ce

export PATH="$emscripten_root:$llvm_root:$dotnet_node_tools/bin:$dotnet_python_tools/bin:$PATH"

# Generate the browser WebGPU implementation with the workload compiler. Dawn
# m150 moved one Emscripten helper after 3.1.56; the generated Ninja file is
# redirected to its original workload location without changing Dawn or the
# installed SDK.
"$emscripten_root/emcmake" cmake --fresh \
  -S "$dawn_root" \
  -B "$emdawn_build_root" \
  -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DDAWN_FETCH_DEPENDENCIES=OFF \
  -DDAWN_BUILD_BENCHMARKS=OFF \
  -DDAWN_BUILD_FUZZERS=OFF \
  -DDAWN_BUILD_NODE_BINDINGS=OFF \
  -DDAWN_BUILD_PROTOBUF=OFF \
  -DDAWN_BUILD_SAMPLES=OFF \
  -DDAWN_BUILD_TESTS=OFF \
  -DDAWN_USE_GLFW=OFF \
  -DTINT_BUILD_BENCHMARKS=OFF \
  -DTINT_BUILD_CMD_TOOLS=OFF \
  -DTINT_BUILD_FUZZERS=OFF \
  -DTINT_BUILD_IR_BINARY=OFF \
  -DTINT_BUILD_TESTS=OFF \
  -DDAWN_ABSEIL_DIR="$skia_root/third_party/externals/abseil-cpp" \
  -DDAWN_EGL_REGISTRY_DIR="$skia_root/third_party/externals/egl-registry" \
  -DDAWN_EMDAWNWEBGPU_DIR="$dawn_root/third_party/emdawnwebgpu" \
  -DDAWN_GLSLANG_DIR="$skia_root/third_party/externals/glslang" \
  -DDAWN_JINJA2_DIR="$skia_root/third_party/externals/jinja2" \
  -DDAWN_MARKUPSAFE_DIR="$skia_root/third_party/externals/markupsafe" \
  -DDAWN_OPENGL_REGISTRY_DIR="$skia_root/third_party/externals/opengl-registry" \
  -DDAWN_SPIRV_HEADERS_DIR="$skia_root/third_party/externals/spirv-headers" \
  -DDAWN_SPIRV_TOOLS_DIR="$skia_root/third_party/externals/spirv-tools" \
  -DDAWN_SWIFTSHADER_DIR="$skia_root/third_party/externals/swiftshader" \
  -DDAWN_VULKAN_HEADERS_DIR="$skia_root/third_party/externals/vulkan-headers" \
  -DDAWN_VULKAN_UTILITY_LIBRARIES_DIR="$skia_root/third_party/externals/vulkan-utility-libraries" \
  -DDAWN_WEBGPU_HEADERS_DIR="$skia_root/third_party/externals/webgpu-headers"
"$emscripten_node" "$repository_root/eng/browser/patch-emdawn-dotnet-toolchain.mjs" \
  "$emdawn_build_root/build.ninja" \
  "$emscripten_root"
ninja -C "$emdawn_build_root" emdawnwebgpu_pkg

emdawn_package_root="$emdawn_build_root/emdawnwebgpu_pkg"
emdawn_cpp_include_root="$emdawn_package_root/webgpu_cpp/include"
emdawn_c_include_root="$emdawn_package_root/webgpu/include"

gn_args="target_os=\"wasm\" target_cpu=\"wasm\"
is_static_skiasharp=true is_official_build=true skia_enable_tools=false
skia_enable_fontmgr_custom_directory=false skia_enable_fontmgr_custom_empty=false
skia_enable_fontmgr_custom_embedded=true skia_enable_fontmgr_empty=false
skia_enable_ganesh=true skia_gl_standard=\"webgl\" skia_enable_pdf=true
skia_use_dng_sdk=false skia_use_webgl=true skia_use_fontconfig=false
skia_use_freetype=true skia_use_harfbuzz=false skia_use_icu=false
skia_use_piex=false skia_use_expat=true skia_use_libwebp_encode=true
skia_use_system_expat=false skia_use_system_freetype2=false
skia_use_system_libjpeg_turbo=false skia_use_system_libpng=false
skia_use_system_libwebp=false skia_use_system_zlib=false
skia_use_vulkan=false skia_use_wuffs=true skia_enable_skottie=true
skia_enable_graphite=true skia_use_dawn=true skia_use_webgpu=true
skia_emsdk_dir=\"$dotnet_emsdk_layout\"
cc=\"$emcc\" cxx=\"$emxx\" ar=\"$emar\"
extra_cflags=[\"-DSKIA_C_DLL\",\"-DSK_AVOID_SLOW_RASTER_PIPELINE_BLURS\",\"-DXML_POOR_ENTROPY\",\"-DSK_DISABLE_AAA\",\"-DGR_GL_CHECK_ALLOC_WITH_GET_ERROR=0\",\"-msimd128\",\"-pthread\",\"-fwasm-exceptions\"]
extra_cflags_cc=[\"-frtti\",\"-msimd128\",\"-pthread\",\"-fwasm-exceptions\"]"

(
  cd "$skia_root"
  "$skia_root/bin/gn" gen \
    "$out_root" \
    --script-executable="$(command -v python3)" \
    --args="$gn_args"
)

# Skia's generic Dawn GN edge builds Dawn Native, which is deliberately absent
# in a browser. Replace that generated output with an empty archive and select
# the Emdawn headers plus CalcNeo's narrow API adapter in ignored Ninja output.
"$emscripten_node" "$repository_root/eng/browser/patch-skia-wasm-toolchain.mjs" \
  "$out_root/toolchain.ninja" \
  "$emscripten_node" \
  "$llvm_ar" \
  "$out_root/libdawn_combined.a" \
  "$out_root/gen/third_party/dawn/libdawn_combined.a.d" \
  "$bridge_root/compat" \
  "$emdawn_cpp_include_root" \
  "$emdawn_c_include_root" \
  "$emscripten_root" \
  "$dotnet_emsdk_layout/upstream/emscripten"
ninja -j "${CALCNEO_NATIVE_JOBS:-6}" -C "$out_root" SkiaSharp

merge_root="$(mktemp -d "${TMPDIR:-/tmp}/calcneo-skia-merge.XXXXXX")"
bridge_build_root="$(mktemp -d "${TMPDIR:-/tmp}/calcneo-skia-bridge.XXXXXX")"
cleanup() {
  rm -rf "$merge_root" "$bridge_build_root"
}
trap cleanup EXIT

for archive in "$out_root"/*.wasm.a; do
  "$llvm_ar" x "$archive" --output "$merge_root"
done

embedded_font_cpp="$merge_root/NotoMono-Regular.ttf.cpp"
python3 "$skia_root/tools/embed_resources.py" \
  --name SK_EMBEDDED_FONTS \
  --input "$skia_root/modules/canvaskit/fonts/NotoMono-Regular.ttf" \
  --output "$embedded_font_cpp" \
  --align 4
"$emxx" -std=c++20 -O3 -pthread -msimd128 -fwasm-exceptions -I"$skia_root" \
  -c "$embedded_font_cpp" -o "$merge_root/NotoMono-Regular.ttf.o"

stock_archive="$merge_root/libSkiaSharp.a"
"$llvm_ar" crs "$stock_archive" "$merge_root"/*.o
mv "$stock_archive" "$native_root/libSkiaSharp.a"

"$emxx" -std=c++20 -O3 -pthread -msimd128 -fwasm-exceptions -frtti -fno-exceptions \
  -DSKIA_C_DLL -DSK_GRAPHITE -DSK_DAWN \
  -I"$emdawn_cpp_include_root" \
  -I"$emdawn_c_include_root" \
  -I"$skia_root" \
  -I"$bridge_root" \
  -c "$bridge_root/sk_graphite_webgpu.cpp" \
  -o "$bridge_build_root/sk_graphite_webgpu.o"
bridge_archive="$bridge_build_root/libCalcNeoGraphiteWebGpu.a"
"$llvm_ar" crs "$bridge_archive" "$bridge_build_root/sk_graphite_webgpu.o"
mv "$bridge_archive" "$native_root/libCalcNeoGraphiteWebGpu.a"

emdawn_destination="$native_root/EmdawnWebGpu"
mkdir -p "$emdawn_destination"
rsync -a --delete --exclude='__pycache__/' \
  "$emdawn_package_root/" "$emdawn_destination/"
"$emscripten_node" "$repository_root/eng/browser/patch-emdawn-package-dotnet-toolchain.mjs" \
  "$emdawn_destination/webgpu/src/library_webgpu.js"

verify_checkout SkiaSharp "$skiasharp_root" "$expected_skiasharp_commit"
verify_checkout mono/skia "$skia_root" "$expected_skia_commit"

echo "Built pristine m150 browser inputs:"
echo "  $native_root/libSkiaSharp.a"
echo "  $native_root/libCalcNeoGraphiteWebGpu.a"
echo "  $emdawn_destination/emdawnwebgpu.port.py"
