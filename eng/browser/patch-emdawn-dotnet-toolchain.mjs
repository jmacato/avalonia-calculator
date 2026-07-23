import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [ninjaPath, emscriptenRoot] = process.argv.slice(2);

if (!ninjaPath || !emscriptenRoot) {
    throw new Error('Expected the Emdawn build.ninja path and .NET Emscripten root.');
}

const currentHelper = join(emscriptenRoot, 'tools', 'gen_struct_info.py');
const workloadHelper = join(emscriptenRoot, 'tools', 'maint', 'gen_struct_info.py');

if (!existsSync(workloadHelper)) {
    throw new Error(`The .NET workload struct-info helper is missing: ${workloadHelper}`);
}

const source = readFileSync(ninjaPath, 'utf8');
if (!source.includes(currentHelper) && !source.includes(workloadHelper)) {
    throw new Error(`Could not find Dawn's Emscripten helper reference in ${ninjaPath}.`);
}

// CalcNeo and the .NET browser runtime are wasm32. Dawn's package generator
// embeds a second table for wasm64 consumers, but the workload deliberately
// does not ship a wasm64-capable Binaryen pipeline. Generate that unreachable
// branch with the same wasm32 ABI data instead of switching toolchains.
const patched = source
    .replaceAll(currentHelper, workloadHelper)
    .replaceAll(' --wasm64 ', ' ');

writeFileSync(ninjaPath, patched, 'utf8');
