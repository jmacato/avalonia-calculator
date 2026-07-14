# Vendored dependencies

Calculator compiles these projects from source. If you replace them with binary package references, update this dependency record.

## FluentAvalonia

- Upstream: <https://github.com/amwx/FluentAvalonia>
- Commit: `a03fd9e02645c16ef0ebe7bb0266480eeb97871f`
- License: `FluentAvalonia/LICENSE` (MIT)
- Imported scope: the complete `src/FluentAvalonia` library

Local patches:

1. Retarget the library to `net10.0` and centrally pinned Avalonia `12.1.0`.
2. Use MicroCom `0.11.6`, required by Avalonia 12.1, and generate its WinRT
   interop source under `obj` so repeated builds do not duplicate a source item.
3. Replace two obsolete `GetTemplateChildren` calls with
   `GetTemplateDescendants`.
4. Omit the optional Avalonia DataGrid theme and dependency. DataGrid 12.1 emits
   IL2070/IL3050 from its reflection-based `TypeHelper`; Calculator does not use
   DataGrid and the NativeAOT release gate forbids suppressing those warnings.

## Avalonia.Labs.Lottie

- Upstream: <https://github.com/AvaloniaUI/Avalonia.Labs>
- Commit: `7d86b80d576b9c0052fda104303c1ec1acb74f53`
- License: `Avalonia.Labs.Lottie/LICENSE` (MIT)
- Imported scope: `src/Avalonia.Labs.Lottie`

Local patches retarget the project to `net10.0`, Avalonia `12.1.0`, and stable
SkiaSharp/Skottie `3.119.4`.

## Updating a snapshot

1. Clone upstream and check out the exact proposed commit.
2. Replace only the imported scope above; do not copy a nested `.git` directory.
3. Reapply each documented patch and update its commit and license.
4. Run warning-free Debug and Release builds.
5. Publish and launch NativeAOT for every currently tested target architecture.
6. If a dependency, scope, or patch changes, update this dependency record.
