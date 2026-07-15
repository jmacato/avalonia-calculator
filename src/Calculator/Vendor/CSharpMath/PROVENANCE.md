# CSharpMath Avalonia

The source in this directory is derived from
[`Sylinko/CSharpMath.Avalonia`](https://github.com/Sylinko/CSharpMath.Avalonia)
at commit `ba73eb9b89662e2f9c039f8371f29bec05d13c48` (the source revision used by
`Sylinko.CSharpMath.Avalonia` 12.0.0).

Only the CSharpMath parser/typesetter, rendering layer, and Avalonia view were
retained. Tests, examples, the editor, text-layout support, and packaging files
were omitted because Calculator uses the read-only `MathView` only.

Calculator-specific changes:

- Replaced the bundled LayoutFarm Typography/OpenFont backend with Avalonia
  12's `GlyphTypeface`, glyph metrics, and `GlyphRun` APIs.
- Read the OpenType MATH data through the HarfBuzz library already shipped by
  Avalonia.Skia.
- Removed reflection over painter methods so trimming and NativeAOT do not need
  dynamic member preservation.
- Require the caller to supply an Avalonia `GlyphTypeface`; Calculator supplies
  its embedded Noto Sans Math face.

The upstream MIT license is reproduced in [LICENSE](LICENSE).
