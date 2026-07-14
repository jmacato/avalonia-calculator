# Repository agent instructions

- For quick C# probes or throwaway runners, use single-file .NET with `dotnet run file.cs`.
- Port the existing WinUI implementation in place. Copy and translate the existing ViewModels, XAML views, controls, styles, and supporting code while preserving their structure and behavior.
- Do not invent replacement ViewModels, XAML views, controls, or parallel application architecture when a WinUI implementation already exists in this repository.
- Treat the existing WinUI files as the authoritative implementation and parity reference. Make the minimum framework-specific changes required for Avalonia and cross-platform services.
- Do not use shortcuts, placeholders, simplified substitutes, or generic framework controls when the original implementation has a custom control, panel, template selector, style, animation, layout algorithm, or interaction model. Port that original implementation in place before treating the feature as complete.
- Before changing a view, trace every referenced original control, resource dictionary, template, selector, converter, and code-behind dependency and port those dependencies as well. Do not silently drop them or replace them with superficially similar Avalonia behavior.
- If an earlier port introduced a shortcut, remove it and restore the original structure and logic from the WinUI source; do not layer another exception on top.
