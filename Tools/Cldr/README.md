# CLDR data generator

`GenerateCurrencyData.cs` downloads the official Unicode CLDR sources and
regenerates the checked-in currency metadata and unit display conventions used
by the application. The app does not access CLDR or GitHub at build time or
runtime.

The repository is pinned to CLDR 48.2:

```sh
dotnet run Tools/Cldr/GenerateCurrencyData.cs
```

Verify that the generated source is current without modifying it:

```sh
dotnet run Tools/Cldr/GenerateCurrencyData.cs -- --check
```

To inspect a newer stable CLDR release, use `--cldr-ref latest`. Before you commit the output, pin its release tag and version. Update the [CLDR provenance record](../../src/Calculator/Assets/Cldr/PROVENANCE.md).

The generator reads the checked-in `Resources.*.resx` catalogs to determine the
application’s shipped cultures. It resolves each culture’s CLDR inheritance
chain from CLDR’s own parent-locale and likely-subtag data, then emits only the
localized currency names, symbols, and short-unit placement/spacing values that
differ from base English. This preserves non-truncating parents such as
`en-GB` → `en-001` and `es-MX` → `es-419`, as well as script-specific parents.
Every fetched locale file and SHA-256 hash is recorded in the generated source.
Runtime lookup initializes only the selected locale’s table and falls back to
English for missing entries.

Unit layouts come from CLDR’s `unitLength type="short"` `other` patterns. The
generator validates its mappings against every member of `UnitConverterUnits`.
For Calculator-specific and whimsical units that CLDR does not define, the
checked-in mapping uses the nearest semantic CLDR pattern solely for
prefix/suffix placement and whether the number is separated from the localized
Calculator abbreviation. The abbreviation itself continues to come from the
existing RESX catalog.

Standard (non-cash) fraction digits come from
`common/supplemental/supplementalData.xml`. Cash rounding is intentionally not
used by the calculator's general currency converter.
