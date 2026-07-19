import { readFileSync, writeFileSync } from 'node:fs';

const workerPath = process.argv[2];

if (!workerPath) {
    throw new Error('The generated pthread worker path is required.');
}

const marker = 'calc-managed-exception-v5';
const source = readFileSync(workerPath, 'utf8');

if (source.includes(marker)) {
    process.exit(0);
}

const handlerAssignment = 'self.onmessage=handleMessage;';
const handlerAssignmentOffset = source.lastIndexOf(handlerAssignment);
const patchedCatchOffset = source.lastIndexOf(
    'catch(ex){const diagnosticVersion="calc-managed-exception-',
    handlerAssignmentOffset);
const terminalCatchOffset = patchedCatchOffset >= 0
    ? patchedCatchOffset
    : source.lastIndexOf('catch(ex)', handlerAssignmentOffset);

if (terminalCatchOffset < 0 || handlerAssignmentOffset < 0) {
    throw new Error(`Could not identify the generated pthread terminal catch in ${workerPath}.`);
}

const diagnosticCatch = 'catch(ex){const diagnosticVersion="calc-managed-exception-v5";let detail="";let managedToken=false;const wasmException=typeof WebAssembly.Exception==="function"&&ex instanceof WebAssembly.Exception;const canDecode=wasmException&&typeof ex?.getArg==="function"&&typeof Module["getExceptionMessage"]==="function";if(canDecode){try{const decoded=Module["getExceptionMessage"](ex);if(decoded){detail=`${decoded[0]}: ${decoded[1]??""}`;managedToken=decoded[0]==="int*"}}catch(decodeError){detail=`native-exception-decode=${decodeError?.name??"Error"}: ${decodeError?.message??decodeError}`}}if(managedToken){try{const ptr=Module["_calc_browser_take_current_managed_exception"]?.();if(ptr){try{detail=Module["UTF8ToString"](ptr)}finally{Module["_free"](ptr)}}}catch(managedError){detail+=`; managed-exception-decode=${managedError?.name??"Error"}: ${managedError?.message??managedError}`}}if(!detail){const message=typeof ex?.message==="string"?ex.message:"";const stack=typeof ex?.stack==="string"?ex.stack:"";detail=`${diagnosticVersion}; kind=${Object.prototype.toString.call(ex)}; constructor=${ex?.constructor?.name??"unknown"}; wasmException=${wasmException}; getArg=${typeof ex?.getArg}; message=${message}; stack=${stack}`;}Module["__emscripten_thread_crashed"]?.();const wrapped=new Error(`worker exception: ${detail}`);wrapped.cause=ex;throw wrapped}';
const patched = source.slice(0, terminalCatchOffset)
    + diagnosticCatch
    + '}'
    + source.slice(handlerAssignmentOffset);

writeFileSync(workerPath, patched, 'utf8');
