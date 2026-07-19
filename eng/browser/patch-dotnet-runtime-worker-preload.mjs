import { readFileSync, writeFileSync } from 'node:fs';
import { brotliCompressSync, brotliDecompressSync, constants } from 'node:zlib';

const runtimePath = process.argv[2];

if (!runtimePath) {
    throw new Error('The generated dotnet runtime module path is required.');
}

const marker = 'calc-sequential-pthread-preload-v1';
const isBrotliSidecar = runtimePath.endsWith('.br');
const input = readFileSync(runtimePath);
const source = isBrotliSidecar
    ? brotliDecompressSync(input).toString('utf8')
    : input.toString('utf8');

if (source.includes(marker)) {
    process.exit(0);
}

const preloadPattern = /const ([A-Za-z_$][\w$]*)=([A-Za-z_$][\w$]*)\(\);if\(\1\.length>0\)\{const ([A-Za-z_$][\w$]*)=\1\.map\(([A-Za-z_$][\w$]*)\);await Promise\.all\(\3\)\}else ([A-Za-z_$][\w$]*)\("No workers in the pthread pool, please validate the pthreadPoolInitialSize"\)/g;
const matches = [...source.matchAll(preloadPattern)];

if (matches.length !== 1) {
    throw new Error(`Expected one pthread preload block in ${runtimePath}, found ${matches.length}.`);
}

const [block, workers, getWorkers, , loadWorker, warn] = matches[0];
const replacement = `const ${workers}=${getWorkers}();if(${workers}.length>0){/*${marker}*/for(const worker of ${workers})await ${loadWorker}(worker)}else ${warn}("No workers in the pthread pool, please validate the pthreadPoolInitialSize")`;
const patched = source.replace(block, replacement);

if (isBrotliSidecar) {
    writeFileSync(runtimePath, brotliCompressSync(Buffer.from(patched), {
        params: {
            [constants.BROTLI_PARAM_MODE]: constants.BROTLI_MODE_TEXT,
            [constants.BROTLI_PARAM_QUALITY]: 11,
        },
    }));
} else {
    writeFileSync(runtimePath, patched, 'utf8');
}
