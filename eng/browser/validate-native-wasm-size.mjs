import { statSync } from 'node:fs';

const wasmPath = process.argv[2];
const maximumBytes = Number(process.argv[3]);

if (!wasmPath) {
    throw new Error('The native WebAssembly path is required.');
}

if (!Number.isSafeInteger(maximumBytes) || maximumBytes <= 0) {
    throw new Error(`The native WebAssembly byte budget is invalid: ${process.argv[3] ?? ''}`);
}

const actualBytes = statSync(wasmPath).size;
const mebibytes = bytes => (bytes / (1024 * 1024)).toFixed(2);

console.info(
    `Calculator browser native WebAssembly: ${mebibytes(actualBytes)} MiB `
    + `(budget ${mebibytes(maximumBytes)} MiB).`);

if (actualBytes > maximumBytes) {
    throw new Error(
        `Profile-guided AOT exceeded its WebKit memory guardrail: `
        + `${actualBytes} bytes > ${maximumBytes} bytes.`);
}
