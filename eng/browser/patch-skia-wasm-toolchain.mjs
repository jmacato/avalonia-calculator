import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const args = process.argv.slice(2);

if (args[0] === '--emit-stub') {
    const [, llvmAr, archivePath, depfilePath] = args;
    mkdirSync(dirname(archivePath), { recursive: true });
    mkdirSync(dirname(depfilePath), { recursive: true });
    const result = spawnSync(llvmAr, ['crs', archivePath], { stdio: 'inherit' });
    if (result.status !== 0) {
        process.exit(result.status ?? 1);
    }
    writeFileSync(depfilePath, archivePath + ':\n', 'utf8');
    process.exit(0);
}

const [
    ninjaPath,
    nodePath,
    llvmAr,
    archivePath,
    depfilePath,
    compatibilityIncludeRoot,
    emdawnCppIncludeRoot,
    emdawnCIncludeRoot,
    dotnetEmscriptenRoot,
    skiaEmscriptenRoot,
] = args;
if (!ninjaPath ||
    !nodePath ||
    !llvmAr ||
    !archivePath ||
    !depfilePath ||
    !compatibilityIncludeRoot ||
    !emdawnCppIncludeRoot ||
    !emdawnCIncludeRoot ||
    !dotnetEmscriptenRoot ||
    !skiaEmscriptenRoot) {
    throw new Error(
        'Expected toolchain.ninja, node, llvm-ar, archive, depfile, compatibility, Emdawnwebgpu includes, and Emscripten roots.');
}

const quote = value => "'" + value.replaceAll("'", "'\\''") + "'";
const source = readFileSync(ninjaPath, 'utf8');
const ruleMarker = 'rule __third_party_dawn_dawn_cmake___gn_toolchain_wasm__rule';
const ruleOffset = source.indexOf(ruleMarker);
if (ruleOffset < 0) {
    throw new Error('Could not find the wasm Dawn CMake rule in ' + ninjaPath + '.');
}

const commandPrefix = '  command = ';
const commandOffset = source.indexOf(commandPrefix, ruleOffset + ruleMarker.length);
const commandEnd = source.indexOf('\n', commandOffset);
if (commandOffset < 0 || commandEnd < 0) {
    throw new Error('Could not find the wasm Dawn CMake command in ' + ninjaPath + '.');
}

const currentCommand = source.slice(commandOffset + commandPrefix.length, commandEnd);
if (!currentCommand.includes('third_party/dawn/build_dawn.py') &&
    !currentCommand.includes('--emit-stub')) {
    throw new Error('Unexpected wasm Dawn CMake command in ' + ninjaPath + '.');
}

const scriptPath = fileURLToPath(import.meta.url);
const replacementCommand = [
    quote(nodePath),
    quote(scriptPath),
    '--emit-stub',
    quote(llvmAr),
    quote(archivePath),
    quote(depfilePath),
].join(' ');
let patched = source.slice(0, commandOffset + commandPrefix.length) +
    replacementCommand + source.slice(commandEnd);

// The generic Dawn target puts its native forwarding headers ahead of
// Emdawnwebgpu. Browser wasm needs Emdawnwebgpu's WebGPU ABI instead.
// Injecting those package headers before target include directories keeps this
// a toolchain choice and leaves both the Skia and Dawn source trees pristine.
const includeMarker = '${defines} ${include_dirs}';
const preferredIncludes =
    '${defines} -I' + quote(compatibilityIncludeRoot) +
    ' -I' + quote(emdawnCppIncludeRoot) +
    ' -I' + quote(emdawnCIncludeRoot) + ' ${include_dirs}';
if (!patched.includes(preferredIncludes)) {
    if (!patched.includes(includeMarker)) {
        throw new Error('Could not find compiler include ordering in ' + ninjaPath + '.');
    }
    patched = patched.replaceAll(includeMarker, preferredIncludes);
}

// Skia's wasm toolchain constructs compiler paths from skia_emsdk_dir instead
// of honoring the generic cc/cxx/ar GN arguments. Keep its sysroot-only layout
// but run the compiler and archiver directly from the evaluated .NET workload.
for (const tool of ['emcc', 'em++', 'emar']) {
    const skiaTool = join(skiaEmscriptenRoot, tool);
    const dotnetTool = join(dotnetEmscriptenRoot, tool);
    if (!patched.includes(skiaTool) && !patched.includes(dotnetTool)) {
        throw new Error(`Could not find Skia's ${tool} command in ${ninjaPath}.`);
    }
    patched = patched.replaceAll(skiaTool, dotnetTool);
}

const graphiteNinjaPath = join(dirname(ninjaPath), 'obj', 'graphite.ninja');
let graphiteNinja = readFileSync(graphiteNinjaPath, 'utf8');
const compatibilityHeader = join(
    compatibilityIncludeRoot,
    'skia_emdawnwebgpu_compat.h');
const compatibilityFlag = '-include' + quote(compatibilityHeader);
if (!graphiteNinja.includes(compatibilityFlag)) {
    const cxxFlagsMarker = 'cflags_cc = ';
    const cxxFlagsOffset = graphiteNinja.indexOf(cxxFlagsMarker);
    const cxxFlagsEnd = graphiteNinja.indexOf('\n', cxxFlagsOffset);
    if (cxxFlagsOffset < 0 || cxxFlagsEnd < 0) {
        throw new Error('Could not find Graphite C++ flags in ' + graphiteNinjaPath + '.');
    }
    graphiteNinja = graphiteNinja.slice(0, cxxFlagsEnd) + ' ' +
        compatibilityFlag + graphiteNinja.slice(cxxFlagsEnd);
    writeFileSync(graphiteNinjaPath, graphiteNinja, 'utf8');
}

writeFileSync(ninjaPath, patched, 'utf8');
