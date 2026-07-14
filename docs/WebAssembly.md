# WebAssembly host

The Avalonia application follows the default cross-platform template layout:

- `src/Calculator` is the shared application project. It owns the original ported XAML, controls, ViewModels, engine integration, resources, and `App`.
- `src/Calculator.Desktop` is the thin macOS/Windows desktop entry point and owns the local HTTP automation startup.
- `src/Calculator.Browser` is the thin WebAssembly entry point and web host.

No browser-specific copy of the application UI or ViewModels exists. The browser lifetime displays the same original `MainPage` used by `MainWindow`.

Run the development host with:

```sh
dotnet run --project src/Calculator.Browser/Calculator.Browser.csproj \
  --launch-profile Calculator.Browser
```

The launch profile listens on `http://0.0.0.0:5221`. Open `http://localhost:5221` from the same machine.

Build without starting the host with:

```sh
dotnet build src/Calculator.Browser/Calculator.Browser.csproj -c Debug
```
