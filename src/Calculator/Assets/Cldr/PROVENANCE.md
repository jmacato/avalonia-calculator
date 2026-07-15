# Unicode CLDR currency and unit-display metadata

- Upstream: https://github.com/unicode-org/cldr
- Release: 48.2
- Git reference: `release-48-2`
- License: Unicode License v3 (`Unicode-3.0`), retained in `LICENSE`
- Inputs: `common/main/root.xml`, `common/main/en.xml`, the resolved
  `common/main/*.xml` inheritance chains for every shipped `Resources.*.resx`
  culture,
  `common/supplemental/supplementalData.xml`, and
  `common/supplemental/likelySubtags.xml`
- Generated output:
  `ViewModels/DataLoaders/CldrCurrencyData.g.cs`

The generated source header records the exact upstream URL and SHA-256 hash of
every input. Locale tables contain only currency display data and short-unit
placement/spacing metadata that differs from base English; the runtime
initializes only the selected table. Regenerate with:

```sh
dotnet run Tools/Cldr/GenerateCurrencyData.cs
```

Use `--check` for a non-mutating reproducibility check. If the CLDR release changes, update this record.
