#!/usr/bin/env python3
"""Align the combined browser currency font's line metrics with Hind2.

The currency symbol TextBlock uses NotoSans-Currency followed by Hind2 as a
fallback chain.  Both faces must expose the same vertical metrics or changing
between their glyphs moves the baseline in a bottom-aligned line box.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import struct
import sys
from pathlib import Path

from fontTools.misc.roundTools import otRound
from fontTools.ttLib import TTFont


USE_TYPO_METRICS = 1 << 7
MUTABLE_TABLES = {"head", "hhea", "OS/2"}
HHEA_FIELDS = ("ascent", "descent", "lineGap")
OS2_FIELDS = (
    "sTypoAscender",
    "sTypoDescender",
    "sTypoLineGap",
    "usWinAscent",
    "usWinDescent",
)


def parse_args() -> argparse.Namespace:
    repository = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--currency-font",
        type=Path,
        default=repository
        / "src/Calculator/Assets/Fonts/NotoSans/NotoSans-Currency.ttf",
        help="combined browser currency font",
    )
    parser.add_argument(
        "--reference",
        type=Path,
        default=repository / "src/Calculator/Assets/Fonts/Hind2/Hind2-Regular.ttf",
        help="fallback face whose vertical metrics define the shared line box",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="validate the committed font without rewriting it",
    )
    return parser.parse_args()


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


def scaled(value: int, source_upem: int, destination_upem: int) -> int:
    return otRound(value * destination_upem / source_upem)


def expected_metrics(currency: TTFont, reference: TTFont) -> dict[str, int]:
    source_upem = reference["head"].unitsPerEm
    destination_upem = currency["head"].unitsPerEm
    expected: dict[str, int] = {}

    for field in HHEA_FIELDS:
        expected[f"hhea.{field}"] = scaled(
            getattr(reference["hhea"], field), source_upem, destination_upem
        )
    for field in OS2_FIELDS:
        expected[f"OS/2.{field}"] = scaled(
            getattr(reference["OS/2"], field), source_upem, destination_upem
        )

    return expected


def validate_identity(currency: TTFont, reference: TTFont) -> None:
    currency_names = names(currency, 1) | names(currency, 16)
    reference_names = names(reference, 1) | names(reference, 16)
    if "Noto Sans" not in currency_names:
        raise ValueError(f"Unexpected currency font families: {sorted(currency_names)}")
    if "Hind2" not in reference_names:
        raise ValueError(f"Unexpected reference families: {sorted(reference_names)}")


def validate_metrics(
    currency: TTFont, reference: TTFont, expected: dict[str, int]
) -> None:
    tables = {"hhea": currency["hhea"], "OS/2": currency["OS/2"]}
    failures: list[str] = []
    for key, expected_value in expected.items():
        table_name, field = key.split(".", 1)
        actual = getattr(tables[table_name], field)
        if actual != expected_value:
            failures.append(f"{key}={actual}, expected {expected_value}")

    expected_use_typo = bool(reference["OS/2"].fsSelection & USE_TYPO_METRICS)
    actual_use_typo = bool(currency["OS/2"].fsSelection & USE_TYPO_METRICS)
    if actual_use_typo != expected_use_typo:
        failures.append(
            f"OS/2.fsSelection USE_TYPO_METRICS={actual_use_typo}, "
            f"expected {expected_use_typo}"
        )

    if failures:
        raise ValueError("currency metric mismatch:\n  " + "\n  ".join(failures))


def apply_metrics(
    currency: TTFont, reference: TTFont, expected: dict[str, int]
) -> int:
    tables = {"hhea": currency["hhea"], "OS/2": currency["OS/2"]}
    changed = 0
    for key, value in expected.items():
        table_name, field = key.split(".", 1)
        table = tables[table_name]
        if getattr(table, field) != value:
            setattr(table, field, value)
            changed += 1

    selection = currency["OS/2"].fsSelection
    if reference["OS/2"].fsSelection & USE_TYPO_METRICS:
        selection |= USE_TYPO_METRICS
    else:
        selection &= ~USE_TYPO_METRICS
    if currency["OS/2"].fsSelection != selection:
        currency["OS/2"].fsSelection = selection
        changed += 1

    return changed


def save_atomic(font: TTFont, path: Path) -> None:
    temporary = path.with_name(f".{path.name}.tmp")
    try:
        font["OS/2"].updateFirstAndLastCharIndex = lambda _font: None
        font.save(temporary, reorderTables=False)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def process(currency_path: Path, reference_path: Path, check: bool) -> str:
    if not currency_path.is_file():
        raise ValueError(f"Currency font does not exist: {currency_path}")
    if not reference_path.is_file():
        raise ValueError(f"Reference font does not exist: {reference_path}")
    if currency_path.resolve() == reference_path.resolve():
        raise ValueError("Currency and reference fonts must be different files")

    currency = TTFont(
        currency_path, lazy=True, recalcBBoxes=False, recalcTimestamp=False
    )
    reference = TTFont(
        reference_path, lazy=True, recalcBBoxes=False, recalcTimestamp=False
    )
    validate_identity(currency, reference)
    expected = expected_metrics(currency, reference)

    if check:
        validate_metrics(currency, reference, expected)
        return f"checked {currency_path.name}: vertical metrics match {reference_path.name}"

    preserved_before = preserved_tables_sha256(currency)
    changed = apply_metrics(currency, reference, expected)

    # Preserve the exact family and coverage bytes after identity validation.
    currency.tables.pop("name", None)
    save_atomic(currency, currency_path)

    written = TTFont(
        currency_path, lazy=True, recalcBBoxes=False, recalcTimestamp=False
    )
    validate_metrics(written, reference, expected)
    preserved_after = preserved_tables_sha256(written)
    if preserved_after != preserved_before:
        raise ValueError(
            "currency outlines, mappings, shaping, or hinting changed while "
            "aligning vertical metrics"
        )

    return f"updated {currency_path.name}: {changed} vertical fields changed"


def main() -> int:
    args = parse_args()
    try:
        print(process(args.currency_font, args.reference, args.check))
    except (KeyError, OSError, struct.error, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
