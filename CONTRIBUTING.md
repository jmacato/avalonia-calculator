# Contributing to CalcNeo

Thanks for helping improve CalcNeo.

## Before making a change

- Search existing issues before opening a new one.
- Keep each change focused on one problem.
- Explain the behavior being changed and how you validated it.
- Include automated tests when practical and update
  [ManualTests.md](docs/ManualTests.md) when manual coverage is required.

## Development setup

See [WebAssembly.md](docs/WebAssembly.md) for browser setup and run instructions. The canonical repository build is:

```sh
dotnet build Calc.Managed.slnx -c Release -m:1
```

Release validation must retain the browser managed-AOT build. Do not replace it
with an interpreter-only WebAssembly build.

## Style

- Match the surrounding C# and XAML style.
- Prefer compiled Avalonia bindings with an explicit `x:DataType`.
- Keep shared UI and business logic in `src/Calculator`; platform projects should
  remain thin hosts.
- Preserve trimming and AOT compatibility. Avoid runtime reflection, dynamic
  activation, and reflection-backed serializers.
- Keep cross-thread coordination lock-free and bounded.
- Do not suppress analyzer diagnostics; fix the underlying issue.

## Testing

Build the full managed solution and run the tests relevant to the changed area.
For visual or interaction changes, exercise both the desktop app and the browser
host. Record any scenarios that cannot be automated in
[ManualTests.md](docs/ManualTests.md).

## Pull requests

Keep commits coherent and the branch buildable. A pull request should describe:

- the problem and intended behavior;
- the implementation and important tradeoffs;
- automated and manual validation;
- any known limitation or follow-up.

All contributions remain subject to the repository license and contributor
requirements configured by the hosting project.
