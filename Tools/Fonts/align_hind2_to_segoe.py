#!/usr/bin/env python3
"""Harmonize the bundled Hind2 metrics with Windows 11 Segoe UI Variable Text.

The Segoe reference is a local-only file copied from the Windows 11 visual-test
VM.  This tool transfers numeric metrics only.  It never copies Segoe glyph
outlines, OpenType layout tables, hinting, names, or other font programs.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import struct
import sys
from collections import defaultdict
from pathlib import Path

from fontTools.misc.roundTools import otRound
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont


REFERENCE_SHA256 = "27ca1ab4bd5ad3b0404ee6a9a03b143408dbd7e5a4c9e4b2054639c03fc0f682"
REFERENCE_FAMILY = "Segoe UI Variable"
REFERENCE_VERSION = "Version 2.03"
REFERENCE_OPTICAL_SIZE = 10.5
USE_TYPO_METRICS = 1 << 7

FACE_WEIGHTS = {
    "Hind2-Light.ttf": 300,
    "Hind2-Regular.ttf": 400,
    "Hind2-Medium.ttf": 500,
    "Hind2-SemiBold.ttf": 600,
    "Hind2-Bold.ttf": 700,
}

# Only these five tables may change.  The `head` checksum adjustment changes
# whenever any table changes; its outline bounds and units-per-em stay intact.
MUTABLE_TABLES = {"head", "hhea", "hmtx", "OS/2", "post"}

# SHA-256 over every preserved raw table from upstream 55039da (the values are
# unchanged from b7d33b1). This catches any accidental change to outlines,
# cmap, names, shaping, positioning, or hinting.
UPSTREAM_PRESERVED_TABLES_SHA256 = {
    "Hind2-Light.ttf": "efc3f0f3a2b4141f5db30d67c79022067e11fc6000ddd1639273f179da0cb9f4",
    "Hind2-Regular.ttf": "d380bdfb5a0e089566dc2c11d32c8cf682ddf3e96472b890219386e8d790b35e",
    "Hind2-Medium.ttf": "7fabc1faeb23edbf43bf7ae89e8243d9d7c2b5a152a971927f06901c94b96989",
    "Hind2-SemiBold.ttf": "f7d5fab4832aedf18ea8d6f0f527b37417f9f7ab7e95acc55d60f12f19e935c9",
    "Hind2-Bold.ttf": "94e8cb749364cc39f4218d410cb9ad7c128c9eac81fc86dd2ffcefb6bd7aadcd",
}

HHEA_FIELDS = ("ascent", "descent", "lineGap")
OS2_FIELDS = (
    "xAvgCharWidth",
    "ySubscriptXSize",
    "ySubscriptYSize",
    "ySubscriptXOffset",
    "ySubscriptYOffset",
    "ySuperscriptXSize",
    "ySuperscriptYSize",
    "ySuperscriptXOffset",
    "ySuperscriptYOffset",
    "yStrikeoutSize",
    "yStrikeoutPosition",
    "sTypoAscender",
    "sTypoDescender",
    "sTypoLineGap",
    "usWinAscent",
    "usWinDescent",
    "sxHeight",
    "sCapHeight",
)
POST_FIELDS = ("underlinePosition", "underlineThickness")


def parse_args() -> argparse.Namespace:
    repository = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--reference",
        type=Path,
        default=repository / ".visual-tests/windows-reference/fonts/SegUIVar.ttf",
        help="local Windows 11 SegUIVar.ttf reference",
    )
    parser.add_argument(
        "--hind-directory",
        type=Path,
        default=repository / "src/Calculator/Assets/Fonts/Hind2",
        help="directory containing the five bundled Hind2 faces",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="validate the committed fonts without rewriting them",
    )
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def raw_table(font: TTFont, tag: str) -> bytes:
    entry = font.reader.tables[tag]
    stream = font.reader.file
    position = stream.tell()
    try:
        stream.seek(entry.offset)
        return stream.read(entry.length)
    finally:
        stream.seek(position)


def preserved_tables_sha256(font: TTFont) -> str:
    digest = hashlib.sha256()
    for tag in sorted(font.reader.tables):
        if tag in MUTABLE_TABLES:
            continue
        data = raw_table(font, tag)
        digest.update(tag.encode("ascii"))
        digest.update(struct.pack(">I", len(data)))
        digest.update(data)
    return digest.hexdigest()


def names(font: TTFont, name_id: int) -> set[str]:
    return {
        record.toUnicode()
        for record in font["name"].names
        if record.nameID == name_id
    }


def validate_reference(path: Path) -> None:
    if not path.is_file():
        raise ValueError(f"Segoe reference does not exist: {path}")
    actual_hash = sha256(path)
    if actual_hash != REFERENCE_SHA256:
        raise ValueError(
            "Segoe reference does not match the Windows 11 fixture "
            f"({actual_hash}, expected {REFERENCE_SHA256})"
        )

    font = TTFont(path, lazy=True)
    if REFERENCE_FAMILY not in names(font, 1):
        raise ValueError(f"Unexpected Segoe family names: {sorted(names(font, 1))}")
    if REFERENCE_VERSION not in names(font, 5):
        raise ValueError(f"Unexpected Segoe version names: {sorted(names(font, 5))}")

    axes = {axis.axisTag: axis for axis in font["fvar"].axes}
    if set(axes) != {"wght", "opsz"}:
        raise ValueError(f"Unexpected Segoe variation axes: {sorted(axes)}")
    if axes["opsz"].defaultValue != REFERENCE_OPTICAL_SIZE:
        raise ValueError(
            f"Unexpected Segoe Text optical size: {axes['opsz'].defaultValue}"
        )


def instantiate_reference(path: Path, weight: int) -> TTFont:
    font = TTFont(path, recalcBBoxes=False, recalcTimestamp=False)

    # The Windows font has variable layout lookups that fontTools 4.63 cannot
    # fully instantiate.  They are irrelevant here: only cmap, HVAR/MVAR,
    # hmtx, and global numeric metrics are read from this temporary instance.
    for tag in ("GDEF", "GPOS", "GSUB"):
        if tag in font:
            del font[tag]

    return instantiateVariableFont(
        font,
        {"wght": weight, "opsz": REFERENCE_OPTICAL_SIZE},
        inplace=False,
    )


def scaled(value: int, source_upem: int, destination_upem: int) -> int:
    return otRound(value * destination_upem / source_upem)


def expected_global_metrics(reference: TTFont, hind: TTFont) -> dict[str, int]:
    source_upem = reference["head"].unitsPerEm
    destination_upem = hind["head"].unitsPerEm
    expected: dict[str, int] = {}

    for field in HHEA_FIELDS:
        expected[f"hhea.{field}"] = scaled(
            getattr(reference["hhea"], field), source_upem, destination_upem
        )
    for field in OS2_FIELDS:
        expected[f"OS/2.{field}"] = scaled(
            getattr(reference["OS/2"], field), source_upem, destination_upem
        )
    for field in POST_FIELDS:
        expected[f"post.{field}"] = scaled(
            getattr(reference["post"], field), source_upem, destination_upem
        )

    return expected


def apply_global_metrics(
    hind: TTFont, reference: TTFont, expected: dict[str, int]
) -> None:
    tables = {"hhea": hind["hhea"], "OS/2": hind["OS/2"], "post": hind["post"]}
    for key, value in expected.items():
        table_name, field = key.split(".", 1)
        setattr(tables[table_name], field, value)

    selection = hind["OS/2"].fsSelection
    if reference["OS/2"].fsSelection & USE_TYPO_METRICS:
        selection |= USE_TYPO_METRICS
    else:
        selection &= ~USE_TYPO_METRICS
    hind["OS/2"].fsSelection = selection


def expected_advances(reference: TTFont, hind: TTFont) -> dict[str, int]:
    source_cmap = reference.getBestCmap()
    hind_cmap = hind.getBestCmap()
    source_metrics = reference["hmtx"].metrics
    source_upem = reference["head"].unitsPerEm
    destination_upem = hind["head"].unitsPerEm
    candidates: defaultdict[str, set[int]] = defaultdict(set)

    for code_point in sorted(set(source_cmap).intersection(hind_cmap)):
        source_glyph = source_cmap[code_point]
        hind_glyph = hind_cmap[code_point]
        candidates[hind_glyph].add(
            scaled(
                source_metrics[source_glyph][0], source_upem, destination_upem
            )
        )

    conflicts = {glyph: values for glyph, values in candidates.items() if len(values) != 1}
    if conflicts:
        details = ", ".join(
            f"{glyph}={sorted(values)}" for glyph, values in sorted(conflicts.items())
        )
        raise ValueError(f"Shared Hind glyphs have conflicting Segoe advances: {details}")

    return {glyph: next(iter(values)) for glyph, values in candidates.items()}


def apply_advances(hind: TTFont, expected: dict[str, int]) -> int:
    changed = 0
    metrics = hind["hmtx"].metrics
    for glyph, target_advance in expected.items():
        current_advance, current_lsb = metrics[glyph]
        if current_advance == target_advance:
            continue

        # Split the width delta across the two side bearings.  This preserves
        # the Hind outline's existing optical offset instead of adopting a
        # Segoe bearing that was designed for a different contour.
        target_lsb = current_lsb + otRound((target_advance - current_advance) / 2)
        metrics[glyph] = (target_advance, target_lsb)
        changed += 1
    return changed


def glyf_bounds_widths(hind: TTFont) -> dict[str, int]:
    glyph_count = hind["maxp"].numGlyphs
    loca_data = raw_table(hind, "loca")
    if hind["head"].indexToLocFormat == 0:
        locations = [
            value * 2
            for value in struct.unpack(
                f">{glyph_count + 1}H", loca_data[: (glyph_count + 1) * 2]
            )
        ]
    else:
        locations = list(
            struct.unpack(
                f">{glyph_count + 1}I", loca_data[: (glyph_count + 1) * 4]
            )
        )

    glyf_data = raw_table(hind, "glyf")
    widths: dict[str, int] = {}
    for index, glyph in enumerate(hind.getGlyphOrder()):
        start = locations[index]
        end = locations[index + 1]
        if end - start < 10:
            continue
        contour_count, _x_min, _y_min, x_max, _y_max = struct.unpack_from(
            ">hhhhh", glyf_data, start
        )
        if contour_count == 0:
            continue
        x_min = struct.unpack_from(">h", glyf_data, start + 2)[0]
        widths[glyph] = x_max - x_min
    return widths


def recalculate_hhea_extents(hind: TTFont) -> None:
    metrics = hind["hmtx"].metrics
    widths = glyf_bounds_widths(hind)
    hhea = hind["hhea"]
    hhea.advanceWidthMax = max(advance for advance, _lsb in metrics.values())
    hhea.minLeftSideBearing = min(metrics[glyph][1] for glyph in widths)
    hhea.minRightSideBearing = min(
        metrics[glyph][0] - metrics[glyph][1] - width
        for glyph, width in widths.items()
    )
    hhea.xMaxExtent = max(
        metrics[glyph][1] + width for glyph, width in widths.items()
    )


def validate_hind_identity(path: Path, font: TTFont) -> None:
    expected_hash = UPSTREAM_PRESERVED_TABLES_SHA256[path.name]
    actual_hash = preserved_tables_sha256(font)
    if actual_hash != expected_hash:
        raise ValueError(
            f"{path.name} preserved tables changed "
            f"({actual_hash}, expected {expected_hash})"
        )
    if "Hind2" not in names(font, 1) and "Hind2" not in names(font, 16):
        raise ValueError(f"Unexpected family names in {path.name}")


def validate_metrics(
    path: Path,
    hind: TTFont,
    reference: TTFont,
    globals_expected: dict[str, int],
    advances_expected: dict[str, int],
) -> None:
    tables = {"hhea": hind["hhea"], "OS/2": hind["OS/2"], "post": hind["post"]}
    failures: list[str] = []
    for key, expected in globals_expected.items():
        table_name, field = key.split(".", 1)
        actual = getattr(tables[table_name], field)
        if actual != expected:
            failures.append(f"{key}={actual}, expected {expected}")

    expected_use_typo = bool(reference["OS/2"].fsSelection & USE_TYPO_METRICS)
    actual_use_typo = bool(hind["OS/2"].fsSelection & USE_TYPO_METRICS)
    if actual_use_typo != expected_use_typo:
        failures.append(
            f"OS/2.fsSelection USE_TYPO_METRICS={actual_use_typo}, "
            f"expected {expected_use_typo}"
        )

    metrics = hind["hmtx"].metrics
    for glyph, expected in advances_expected.items():
        actual = metrics[glyph][0]
        if actual != expected:
            failures.append(f"hmtx[{glyph}]={actual}, expected {expected}")

    if failures:
        preview = "\n  ".join(failures[:20])
        remainder = len(failures) - 20
        suffix = f"\n  ... and {remainder} more" if remainder > 0 else ""
        raise ValueError(f"{path.name} metric mismatch:\n  {preview}{suffix}")


def save_atomic(font: TTFont, path: Path) -> None:
    temporary = path.with_name(f".{path.name}.tmp")
    try:
        # OS/2.compile normally reloads and normalizes cmap solely to recompute
        # first/last character indexes. Coverage is unchanged, so retain the
        # upstream indexes and allow the still-unloaded cmap to be copied raw.
        font["OS/2"].updateFirstAndLastCharIndex = lambda _font: None
        font.save(temporary, reorderTables=False)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def process_face(path: Path, reference_path: Path, weight: int, check: bool) -> str:
    hind = TTFont(path, lazy=True, recalcBBoxes=False, recalcTimestamp=False)
    validate_hind_identity(path, hind)
    if hind["OS/2"].usWeightClass != weight:
        raise ValueError(
            f"{path.name} has weight {hind['OS/2'].usWeightClass}, expected {weight}"
        )

    reference = instantiate_reference(reference_path, weight)
    globals_expected = expected_global_metrics(reference, hind)
    advances_expected = expected_advances(reference, hind)

    if check:
        validate_metrics(path, hind, reference, globals_expected, advances_expected)
        return f"checked {path.name}: {len(advances_expected)} shared glyph advances"

    apply_global_metrics(hind, reference, globals_expected)
    changed = apply_advances(hind, advances_expected)
    recalculate_hhea_extents(hind)

    # `name` and `cmap` were loaded for validation and Unicode matching. Drop
    # their parsed table objects so TTFont copies the exact upstream bytes rather
    # than normalizing equivalent records during save.
    hind.tables.pop("name", None)
    hind.tables.pop("cmap", None)
    save_atomic(hind, path)

    written = TTFont(path, lazy=True, recalcBBoxes=False, recalcTimestamp=False)
    validate_hind_identity(path, written)
    validate_metrics(path, written, reference, globals_expected, advances_expected)
    return (
        f"updated {path.name}: {changed} advances changed, "
        f"{len(advances_expected)} shared glyphs checked"
    )


def main() -> int:
    args = parse_args()
    try:
        validate_reference(args.reference)
        for file_name, weight in FACE_WEIGHTS.items():
            path = args.hind_directory / file_name
            if not path.is_file():
                raise ValueError(f"Missing Hind face: {path}")
            print(process_face(path, args.reference, weight, args.check))
    except (KeyError, OSError, struct.error, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
