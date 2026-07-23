#include <stdint.h>

// .NET 10's browser workload pairs Emscripten 3.1.56 with LLVM 19. LLVM 19
// emits this newer wasm-SjLj bookkeeping ABI, while 3.1.56's compiler-rt
// predates the two definitions. Keep the compatibility boundary in CalcNeo;
// compiler-rt still supplies the actual __wasm_longjmp implementation.
typedef struct CalcNeoWasmJumpBuffer {
    void* function_invocation_id;
    uint32_t label;
} CalcNeoWasmJumpBuffer;

void __wasm_setjmp(void* environment, uint32_t label, void* function_invocation_id)
{
    CalcNeoWasmJumpBuffer* jump_buffer = environment;
    jump_buffer->function_invocation_id = function_invocation_id;
    jump_buffer->label = label;
}

uint32_t __wasm_setjmp_test(void* environment, void* function_invocation_id)
{
    const CalcNeoWasmJumpBuffer* jump_buffer = environment;
    return jump_buffer->function_invocation_id == function_invocation_id
        ? jump_buffer->label
        : 0;
}
