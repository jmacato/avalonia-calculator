# Math Composer provenance

- Source: `/Users/jumar/RiderProjects/math-composer`
- Base repository commit: `15da2ba`
- Snapshot date: 2026-07-20
- License: MIT; see `LICENSE`
- Included projects: `MathComposer.Core` and `MathComposer.Avalonia`
- Included third-party asset: XCharter Math 0.74 under the SIL Open Font License 1.1

This is a source snapshot of the local Math Composer working tree, including
the bindable `MathEditor.Padding` change made immediately before vendoring.
The public undoable `DeleteBackward` and `Clear` operations, natural editor
measurement, vertical content centering, Unicode digit-script parsing, and the
non-interactive `MathDisplay` control were added in the source repository and
vendored copy for the graphing integration.
Boundary-aware Math AutoCorrect typing, including UnicodeMath key ligatures and
the applicable Microsoft symbol-command vocabulary, is likewise maintained in
both copies.
Demo applications and test projects are intentionally omitted because CalcNeo
consumes only the reusable libraries. Build outputs, Git metadata, and package
lock files are also omitted. The vendored projects use CalcNeo's centrally selected Avalonia packages. The snapshot adaptation split helper types into separate files and divided larger methods into smaller methods. Existing implementation logic remained unchanged.

## Implementation history

The original handoff record dated 2026-07-18 describes the deletion of `/Users/jumar/RiderProjects/equation-editor` after checks found no local-only work. It records a new repository at `/Users/jumar/RiderProjects/math-composer`, with no transferred source, assets, or Git history. Its initial handoff contained an independent specification, working rules, licensing records, and a fresh XCharter Math download.

[SPEC.md](SPEC.md) contains the original behavior contract. This snapshot omits the upstream demos, tests, and standalone build instructions.
