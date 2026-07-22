# Noto currency fallback provenance

- Upstream: <https://github.com/notofonts/noto-fonts>
- Revision: `ffebf8c1ee449e544955a7e813c54f9b73848eac`
- License: SIL Open Font License 1.1 (`LICENSE.txt`)
- Tooling: fontTools 4.63.0

`NotoSans-Currency.ttf` is embedded only in the browser-flavored Calculator
assembly. It combines small subsets from six Noto families into one font. The
subsets contain only currency-symbol characters needed by the live Frankfurter
catalog and localized variants in CalcNeo's shipped CLDR tables. OpenType
layout closure retains contextual glyphs and shaping tables; unrelated Unicode
mappings and outlines are removed.

| Source family | Upstream source | Upstream SHA-256 | Included Unicode mappings | Intermediate subset SHA-256 |
|---|---|---|---|---|
| Noto Sans | `hinted/ttf/NotoSans/NotoSans-Regular.ttf` | `b85c38ecea8a7cfb39c24e395a4007474fa5a4fc864f6ee33309eb4948d232d5` | `0405, 041A-041C, 0421-0422, 0434-0435, 043D, 0449, 20A1, 20A6, 20A9-20AB, 20AD-20AE, 20B1-20B2, 20B4-20B5, 20B8, 20BC, 20BE` | `a15bf6506bc6433baae68776cd59625a2c0571a918394bbbc68bff53929d7092` |
| Noto Sans Arabic | `hinted/ttf/NotoSansArabic/NotoSansArabic-Regular.ttf` | `ceea25b464a656dc3b26849bab9356740401af62aedf1bfa8b7f0d9b75925b1b` | `002E, 060B, 0623, 0625, 0627-0628, 062A, 062C, 062F, 0631, 0633, 0639, 0641-0646, 064A, 06A9, 06CC, 200F, FDFC` | `025439ed9fdaeb94ab9ba07b8e097a48dc9317e6f496a8e4f5589ef381f67c00` |
| Noto Sans Armenian | `hinted/ttf/NotoSansArmenian/NotoSansArmenian-Regular.ttf` | `c3332abfe298018517d7f5b687a9c0f5c92f163ea9258f23934eaa7a9378f40e` | `058F` | `239d044d881b092a474a4862a1bbcaf399ceda64da4b553bb5f7b5c1a9332862` |
| Noto Sans Bengali | `hinted/ttf/NotoSansBengali/NotoSansBengali-Regular.ttf` | `6300c5370cd688b0641343de4c786de6d412bb6c578d129dae75e93a0322dcab` | `09F3` | `3448112cf16cdcbb893bb3069d4ff9e08cdecb1c6f35f475440165add244a649` |
| Noto Sans Khmer | `hinted/ttf/NotoSansKhmer/NotoSansKhmer-Regular.ttf` | `5c1ec068352f1e1fe8e7a3218230360d054e104d377ef40c0423b17c34c3c259` | `1791, 17A1, 17B8, 17BC, 17DB` | `d2fd2013213681951e0e460283099546bb47f817adfda92f126936f0967ae5db` |
| Noto Sans Thai | `hinted/ttf/NotoSansThai/NotoSansThai-Regular.ttf` | `404ddfb5ed0aaa6b6ec8a85700d682978992062d67da93903967b56cbd9a4acc` | `0E3F` | `503c817c8bfa8f1f6b1d35c8c7d29e4484573d7145d5b0092180c089035b268b` |

Each intermediate subset was generated with the table's Unicode list and
these common fontTools options:

```text
pyftsubset <source> --output-file=<subset-file> --unicodes=<list> \
  --layout-features='*' --glyph-names --symbol-cmap --legacy-cmap \
  --notdef-glyph --notdef-outline --recommended-glyphs \
  --name-IDs='*' --name-languages='*' --name-legacy --drop-tables+=DSIG
```

The intermediate subsets were combined in table order with:

```text
pyftmerge --output-file=NotoSans-Currency.ttf <subset-files-in-table-order>
```

After merging, the composite's `hhea` and corresponding `OS/2` vertical
metrics are aligned with bundled `Hind2-Regular.ttf`, the next face in the
browser currency fallback chain:

```text
python Tools/Fonts/align_currency_to_hind2.py
```

This post-processing is necessary because `pyftmerge` otherwise selects the
Noto Sans Arabic ascent and descent (`1374` and `-738`) for the whole composite.
Avalonia uses those global metrics to position the baseline even when a symbol
falls through to Hind2. The alignment changes only global vertical metrics and
the required `head` checksum; glyph outlines, mappings, advances, shaping,
positioning, names, and hinting remain unchanged.

The composite retains the `Noto Sans` family name and the complete GSUB/GPOS
tables needed for Arabic and Indic shaping. Its SHA-256 is
`2e81350b619afd4a52f50e43949fe214d112692a64a5b85f6484c603f9498c34`.
