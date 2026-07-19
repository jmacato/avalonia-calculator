// The .NET 10 browser runtime deliberately skips mono_wasm_enable_debugging()
// in its ENABLE_AOT branch even when WasmDebugLevel is non-zero. The debugger
// component is nevertheless linked when WasmDebugLevel is enabled. Initialize
// its shared debug level before Mono starts so diagnostic threaded AOT builds
// can use the browser soft-debugger proxy.

extern void mono_wasm_enable_debugging(int log_level);

__attribute__((constructor))
static void calc_enable_mono_aot_debugger(void)
{
    mono_wasm_enable_debugging(1);
}
