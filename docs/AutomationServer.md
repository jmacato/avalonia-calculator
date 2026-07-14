# Avalonia automation server

The Calculator app contains a loopback-only HTTP automation server for local macOS and Windows porting work. It operates on the real application window: tree queries walk the live Avalonia visual tree, pointer/key/text requests raise Avalonia routed events on the UI thread, and screenshots are rendered directly with `RenderTargetBitmap`.

The server is disabled unless both environment variables below are present:

```sh
CALCULATOR_AUTOMATION_PORT=5197 \
CALCULATOR_AUTOMATION_TOKEN=local-development-token \
dotnet run --project src/Calculator.Desktop/Calculator.Desktop.csproj -c Debug -r osx-arm64
```

Every request must send the token in `X-Calculator-Automation-Token`. The listener binds only to `127.0.0.1`.

Available endpoints:

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/health` | Process and listening-port status. |
| `GET` | `/tree` | Control type, name, automation metadata, bounds, effective visibility, and enabled state. |
| `GET` | `/render` | Direct PNG render of the current Avalonia window. |
| `POST` | `/events/click` | Click a visible control by XAML name or automation ID. |
| `POST` | `/events/pointer` | Raise `move`, `down`, `up`, or `click` at client coordinates. |
| `POST` | `/events/key` | Raise an Avalonia key-down/key-up pair at the focused element. |
| `POST` | `/events/text` | Raise an Avalonia text-input event at the focused element. |
| `POST` | `/window/size` | Resize the real desktop window for responsive-state tests. |

Examples:

```sh
token=local-development-token
curl -H "X-Calculator-Automation-Token: $token" \
  http://127.0.0.1:5197/tree

curl -H "X-Calculator-Automation-Token: $token" \
  -H 'Content-Type: application/json' \
  -d '{"target":"TogglePaneButton"}' \
  http://127.0.0.1:5197/events/click

curl -H "X-Calculator-Automation-Token: $token" \
  http://127.0.0.1:5197/render \
  --output calculator.png

curl -H "X-Calculator-Automation-Token: $token" \
  -H 'Content-Type: application/json' \
  -d '{"width":560,"height":700}' \
  http://127.0.0.1:5197/window/size
```

Animations are real application animations. A client that needs a settled visual baseline should wait for the relevant transition before requesting `/render`.
The render endpoint performs two discarded off-screen resource-prime passes, separated by real `TopLevel.RequestAnimationFrame` boundaries and bounded settle intervals, before returning its direct Avalonia bitmap. This keeps the first NativeAOT capture deterministic when Fluent materials and large control trees initialize lazily.
