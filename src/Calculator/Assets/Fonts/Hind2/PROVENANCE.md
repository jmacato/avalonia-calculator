# Hind2 provenance

- Upstream: <https://github.com/jmacato/Hind2-Font>
- Commit: `55039da6ad7acecadf9eb962a2bf2e689e649368`
- License: SIL Open Font License 1.1 (`LICENSE.txt`)
- Family: `Hind2`
- Included faces: Light, Regular, Medium, SemiBold, and Bold

The production font fallback is installed Segoe UI Variable, installed Segoe UI,
bundled Hind2, then the platform sans-serif fallback. Segoe files used by visual
tests are local-only and must never be placed in this directory or a release
archive.

## Windows 11 metric harmonization

The five bundled faces retain the Hind2 glyph outlines, family/name tables,
Unicode coverage, hinting, and OpenType shaping from the upstream commit. Their
numeric layout metrics are harmonized with the `Segoe UI Variable` Text instance
used by Windows 11 so the fallback produces the same line boxes and substantially
the same wrapping on macOS and WebAssembly:

- Reference system: Windows 11 24H2, build `26100.4349`
- Reference font: `C:\Windows\Fonts\SegUIVar.ttf`, version `2.03`
- Reference SHA-256: `27ca1ab4bd5ad3b0404ee6a9a03b143408dbd7e5a4c9e4b2054639c03fc0f682`
- Variation coordinates: `opsz=10.5` (Text), with `wght=300`, `400`, `500`,
  `600`, and `700` mapped to the corresponding Hind2 faces
- Transferred data: scaled `hhea`, `OS/2`, and `post` vertical metrics plus
  scaled advance widths for the 340 Unicode-mapped glyphs shared by both fonts
- Preserved data: all Hind2 outlines and side-bearing optical offsets, all
  shaping/positioning tables, names, coverage, hinting, and OFL embedding rights

Harmonized production-file SHA-256 values:

| Face | SHA-256 |
|---|---|
| Light | `dce263cf5877599cb1acdab9ced954fc783927277a93e68caed9be9b989d8876` |
| Regular | `3e1e61fef3fb8d608f415bc63bb5236687bba4c5c84f82e97029067c4f5b0101` |
| Medium | `84a9eb22ca7242cec7c61b1acd1070f4f378407f731a3910825bc66392123ffd` |
| SemiBold | `aa2e1b2a8d41d027a245b6e44449f57ba03aa1373e374ccf11bab60447491edf` |
| Bold | `bd11ea36f7c6692c1c78dd47d4a19c11ed197062598b942f45699c499593132a` |

The Segoe reference remains only at the git-ignored path
`.visual-tests/windows-reference/fonts/SegUIVar.ttf`; it is neither a build input
nor distributable. Reproduce or validate the numeric patch with
`Tools/Fonts/align_hind2_to_segoe.py`. The tool pins the exact local reference
hash and rejects any change to the upstream Hind2 outlines, cmap, names,
shaping/positioning tables, or hinting tables.
