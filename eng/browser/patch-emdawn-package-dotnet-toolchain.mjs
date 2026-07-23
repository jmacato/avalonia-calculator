import { readFileSync, writeFileSync } from 'node:fs';

const [libraryPath] = process.argv.slice(2);
if (!libraryPath) {
    throw new Error('Expected the copied Emdawnwebgpu JavaScript library path.');
}

let source = readFileSync(libraryPath, 'utf8');

// Dawn m150 names stackSave/stackRestore as JavaScript library helpers. The
// Emscripten 3.1.56 workload exposes them as WebAssembly system exports, which
// use dependency names without the "$" prefix. Adapt only CalcNeo's copied
// package; Dawn's generated package and the installed workload remain intact.
const replacements = [
    ["'$stackSave'", "'stackSave'"],
    ["'$stackRestore'", "'stackRestore'"],
];

for (const [newerName, workloadName] of replacements) {
    if (source.includes(newerName)) {
        source = source.replaceAll(newerName, workloadName);
    } else if (!source.includes(workloadName)) {
        throw new Error('Could not find ' + newerName + ' in ' + libraryPath + '.');
    }
}

writeFileSync(libraryPath, source, 'utf8');
