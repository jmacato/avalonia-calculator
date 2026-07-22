# WebAssembly host

The Avalonia application follows the default cross-platform template layout:

- `src/Calculator` is the shared application project. It owns the original ported XAML, controls, ViewModels, engine integration, resources, and `App`.
- `src/Calculator.Desktop` is the thin macOS/Windows desktop entry point and owns the local HTTP automation startup.
- `src/Calculator.Browser` is the thin WebAssembly entry point and web host.

No browser-specific copy of the application UI or ViewModels exists. The browser lifetime displays the same original `MainPage` used by `MainWindow`.

Run the development host with:

```sh
dotnet run --project src/Calculator.Browser/Calculator.Browser.csproj \
  --configuration Debug
```

Use `--configuration Release` for the AOT build. Both configurations launch the
same telemetry-capable HTTPS host on `https://0.0.0.0:5221`; no separate publish
or server command is required. Open `https://localhost:5221` from the same machine.

Build without starting the host with:

```sh
dotnet build src/Calculator.Browser/Calculator.Browser.csproj -c Debug
```

## XAML event handlers under full AOT

Code-behind methods referenced by an Avalonia XAML event attribute must be
instance methods, even when the handler does not otherwise use instance state.
Do not declare these handlers `static`.

With the .NET 10 browser full-AOT toolchain, Avalonia's compiled XAML can bind a
static code-behind handler through an instance-shaped delegate. Mono then selects
the four-slot instance `EventHandler` call path while the static method has a
three-slot Wasm function signature. Invoking the event traps with `function
signature mismatch` instead of producing a managed exception.

This was reproduced by opening the currency converter and selecting its source
unit. `UnitConverter.OnValueSelected(object, EventArgs)` occupied a three-slot
function-table entry while Mono invoked it through a four-slot call site. Making
the handler an instance method produced the matching four-slot ABI and allowed
the recorded interaction to complete on the stock .NET 10.0.8 runtime. The same
change was applied to every other static XAML event handler found in Calculator.
