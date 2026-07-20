# Math Composer Specification

Version 1.0 (implementation contract)

## 1. Status and sources

This document defines observable behavior, public contracts, limits, and
acceptance criteria. It does not prescribe parser or renderer algorithms. The
implementation must be original and must follow `AGENTS.md`.

The following public standards are normative where this specification refers
to them:

- [UnicodeMath 3.3, Unicode Technical Note #28](https://www.unicode.org/notes/tn28/)
- [MathML Core](https://www.w3.org/TR/mathml-core/)
- [OpenType MATH table](https://learn.microsoft.com/en-us/typography/opentype/spec/math)

The requirements in this document take precedence for the app-owned subset,
canonicalization rules, editing behavior, limits, and public API. UnicodeMath
features not named here are not promised in version 1.

## 2. Product definition

Math Composer is an MIT-licensed equation editor written in C# and .NET with a
reusable Avalonia control. UnicodeMath is its primary authoring syntax. LaTeX
and Presentation MathML are interchange formats. Formula layout is rendered
directly through Avalonia/Skia using the vendored XCharter Math OpenType MATH
data.

The same editor, document model, parser, serializers, renderer, and demo view
must run in:

- one desktop application for macOS, Windows, and Linux; and
- an Avalonia Browser/WebAssembly application suitable for viewing and editing
  from a phone browser on the local network.

“Calculator-compatible” means the build-up observations and keypad vocabulary
defined here. It does not mean byte-for-byte RichEdit parity, graphing-engine
compatibility, or reproduction of unpublished behavior.

### 2.1 Version 1 exclusions

Version 1 does not evaluate or graph expressions. It does not support
user-defined macros, equation numbering, arbitrary styling or colors,
chemistry notation, diagrams, tensor prescripts, right-to-left math layout,
Content MathML, or automatic line wrapping. Hosting, HTTPS configuration, PWA
installation, offline mode, and physical-phone certification are also out of
scope.

## 3. Toolchain and repository shape

- Pin the .NET SDK to `10.0.204` in `global.json`.
- Pin Avalonia packages to `12.1.0`.
- Use central package management and committed NuGet lock files.
- Restore from `https://api.nuget.org/v3/index.json` only.
- Enable nullable reference types, deterministic builds, and warnings as
  errors for repository projects.
- Keep core document/editing/interchange code independent of Avalonia.

The solution contains these logical projects; exact assembly names may use the
`MathComposer` prefix:

1. Core library: document, parsers, serializers, editing operations, history.
2. Avalonia library: reusable control, layout engine, font reader, renderer.
3. Shared demo UI library.
4. Desktop host.
5. Browser/WASM host.
6. Core unit/property/fuzz tests.
7. Headless Avalonia interaction/layout/snapshot tests.
8. Browser Playwright tests.

No project may depend on another equation renderer or on browser-native MathML
for formula presentation.

## 4. Immutable document model

`MathDocument` is an immutable value whose root is a `MathRow`. Publicly
observable edits return a new document. Nodes and child collections are
immutable. Equality is structural; transient layout caches, diagnostics, caret
state, and history are not part of document equality.

The model must represent at least the following node forms:

| Form | Required data |
| --- | --- |
| Row | ordered child nodes |
| Text/symbol | non-empty Unicode scalar text and atom class |
| Fraction | numerator row and denominator row |
| Radical | radicand row and optional degree row |
| Script | base node, optional subscript row, optional superscript row |
| Under/over | base row plus optional below and above rows and construction kind |
| Accent | base row, accent kind, and under/over placement |
| Delimiter | body row, optional opening and closing symbols, scalable flag |
| Table | rectangular rows of cell rows and explicit layout kind |
| Spacing | a supported explicit mathematical spacing width |
| Error | source format, raw fragment, and recovery message/code |

Text/symbol atom classes distinguish identifier, number, operator, relation,
punctuation, and ordinary text. The classification guides spacing and does not
change the source characters. Under/over kinds cover n-ary limits, bars, and
braces. Accent kinds cover acute, grave, hat, check, breve, tilde, bar, dot,
double-dot, triple-dot, and vector arrow.

`MathTableKind` has `Matrix`, `Cases`, `Aligned`, and `Gathered`. Matrices,
piecewise cases, aligned equations, and gathered lines must not use unrelated
special-case node types. A matrix/aligned/cases table may contain multiple
columns; a gathered table has one column. Import pads short rows with empty
cells and reports a non-fatal diagnostic. Export is rectangular and stable.

Required empty child rows are editing placeholders. Placeholder adornments are
inferred from such empty slots and are not serialized as source text. An empty
top-level row is valid. All other node invariants are enforced at construction
time; invalid public construction fails with an argument exception rather than
creating a corrupt tree.

### 4.1 Paths, positions, and selections

A structural path is an immutable sequence of zero-based child indices from
the document root. A position contains a path and a UTF-16 character offset.
The offset addresses text within a text/symbol node; at a row path it addresses
the boundary between children. Offsets must fall on Unicode scalar boundaries.

`MathSelection` contains anchor and active positions. A collapsed selection is
a caret. Direction is retained. Public setters normalize invalid positions to
the nearest valid position in document order and raise a selection diagnostic;
they never leave the control with an invalid selection.

Structural edits remap both endpoints predictably:

- text inserted before an endpoint advances that endpoint;
- deletion collapses covered endpoints to the start of the deleted range;
- replacing a selected structure places the caret after the replacement; and
- inserting a template selects or enters its first required empty child.

### 4.2 Errors and diagnostics

Recoverable malformed or unsupported input becomes a visible `MathError` node.
Its raw fragment is retained exactly. UnicodeMath and LaTeX export emit that
raw fragment; MathML export emits an `merror` containing an `mtext` with escaped
raw text. Recovery should consume the smallest fragment that guarantees
forward progress and retain valid siblings on both sides.

Diagnostics contain a stable code, severity (`Info`, `Warning`, `Error`, or
`Fatal`), message, source format, and source span where applicable. Message
wording may improve without breaking compatibility; codes, severity, and spans
are the contract. Fatal limit violations return a recovered document
containing a visible error node and no partially trusted remainder.

## 5. Public API contract

Public names and signatures may add conventional event-argument or helper
types, but must expose the following semantics. All public APIs are documented.

```csharp
public enum MathTextFormat
{
    UnicodeMath,
    Latex,
    MathMl
}

public sealed record MathDocument;

public readonly record struct MathPosition(
    ImmutableArray<int> Path,
    int Offset);

public readonly record struct MathSelection(
    MathPosition Anchor,
    MathPosition Active)
{
    public bool IsCollapsed { get; }
}

public sealed record MathDiagnostic;

public sealed record MathParseResult(
    MathDocument Document,
    ImmutableArray<MathDiagnostic> Diagnostics)
{
    public bool HasFatalDiagnostics { get; }
}
```

The core library exposes deterministic parse/import and export services for all
three `MathTextFormat` values. The reusable Avalonia control exposes:

```csharp
public sealed class MathEditor : Avalonia.Controls.Control
{
    public MathDocument Document { get; set; }
    public MathSelection Selection { get; set; }
    public bool IsReadOnly { get; set; }
    public bool ShowPalette { get; set; }
    public double MathFontSize { get; set; }
    public Thickness Padding { get; set; }
    public int HistoryCapacity { get; set; }

    public MathParseResult Load(string text, MathTextFormat format);
    public string Export(MathTextFormat format);
    public void DeleteBackward();
    public void Clear();

    public event EventHandler<MathDocumentChangedEventArgs>? DocumentChanged;
    public event EventHandler<MathSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<MathDiagnosticsEventArgs>? DiagnosticsChanged;
    public event EventHandler<MathSubmittedEventArgs>? Submitted;
}

public sealed class MathDisplay : Avalonia.Controls.Control
{
    public string MathMl { get; set; }
    public MathDocument Document { get; set; }
    public double MathFontSize { get; set; }
    public IBrush Foreground { get; set; }
    public Thickness Padding { get; set; }
}
```

`Load` never throws for user input; it applies the recovered document,
diagnostics, selection, and history reset as one operation and returns the same
document and diagnostics. Programmer errors such as a null argument may throw.
`Export` is deterministic and culture-invariant.
`DeleteBackward` and `Clear` are read-only-aware, undoable editor commands.
`MathDisplay` is the non-interactive MathML renderer and uses the same parser,
font, layout engine, and renderer as `MathEditor`.

Document and selection properties support Avalonia binding. `MathFontSize`
must be finite and positive. `HistoryCapacity` is clamped to `0..10_000`; its
default is 100. Reducing it immediately discards the oldest excess history.
When `IsReadOnly` is true, navigation, selection, copy, and export remain
available while every document-changing command is disabled.

Both controls measure to their mathematical content plus padding. They have no
fixed minimum editor height or width. Taller structures such as fractions and
radicals therefore grow their host row, while ordinary expressions remain
vertically centered in any additional space supplied by the host.

The control publishes Avalonia commands for undo, redo, cut, copy, paste,
select-all, structural insertion (fraction, radical, scripts, delimiters,
matrix/cases/aligned/gathered, accent, under/over), build-up, and next/previous
placeholder navigation. Command enablement reflects read-only state, selection,
history, and clipboard availability.

Events are raised after state is internally consistent. A single user command
raises at most one document-change event and one selection-change event.
`Submitted` is raised after whole-document build-up on Enter or focus loss and
includes the canonical UnicodeMath text and current diagnostics.

## 6. UnicodeMath authoring profile

UnicodeMath 3.3 is the base syntax. This section fixes the accepted version 1
subset and its deterministic extensions/aliases. Source is normalized to NFC
except that raw error fragments retain their original code units.

### 6.1 Lexical rules

- Identifiers contain Unicode letters, letter numbers, combining marks, and
  connector punctuation. `π` and `e` are ordinary identifier atoms with
  palette labels as constants; `x` and `y` are conventional variables.
- Numbers contain Unicode decimal digits, one decimal separator, and optional
  scientific exponent `e` or `E` followed by an optional sign and digits.
- Operators and relations may be direct Unicode symbols or accepted control
  words. Arbitrary Unicode mathematical symbols are retained even when their
  semantics are only classified as an ordinary operator.
- ASCII space separates tokens during import/build-up. A spacing character
  explicitly listed in 6.5 creates a spacing node.
- A control word is `\` followed by one or more ASCII letters. Unknown control
  words are recoverable error fragments, not silently discarded identifiers.
- A token may not exceed 16 KiB of UTF-8 input. Source spans use UTF-16 offsets
  in the original .NET string.

### 6.2 EBNF

The grammar is descriptive EBNF. Longest-token matching applies. `implicit`
is a zero-width multiplication boundary allowed only between adjacent forms
that can end and begin an operand.

```ebnf
document        = row, end ;
row             = { relation } ;
relation        = sum, { relation-op, sum } ;
sum             = product, { add-op, product } ;
product         = fraction,
                  { (multiply-op, fraction) | (implicit, fraction) } ;
fraction        = scripted, { "/", scripted } ;
scripted        = primary, { script } ;
script          = ("_", operand) | ("^", operand) ;

primary         = number
                | identifier
                | symbol
                | group
                | delimited
                | radical
                | function-call
                | nary
                | accent-form
                | under-over-form
                | table-form
                | latex-frac-alias
                | error-fragment ;

operand         = primary | group ;
group           = "(", row, ")" | "{", row, "}" ;
delimited       = "\\left", delimiter, row, "\\right", delimiter ;
radical         = "√", "(", [ row, "&" ], row, ")" ;
function-call   = function-name, "(", [ argument-list ], ")" ;
argument-list   = row, { ",", row } ;
nary            = nary-symbol, [ script ], [ script ], operand ;
accent-form     = accent-name, "(", row, ")" ;
under-over-form = under-over-name, "(", row, ")" ;

table-form      = matrix-form | cases-form | eqarray-form | gathered-form ;
matrix-form     = "\\matrix(", table-body, ")" ;
cases-form      = "\\cases(", table-body, ")" ;
eqarray-form    = "\\eqarray(", table-body, ")" ;
gathered-form   = "\\gathered(", gathered-body, ")" ;
table-body      = row, { "&", row }, { "@", row, { "&", row } } ;
gathered-body   = row, { "@", row } ;

latex-frac-alias = "\\frac", "{", row, "}", "{", row, "}" ;
```

`/` associates left within an ungrouped run: `a/b/c` is `(a/b)/c`.
Scripts bind to the immediately preceding primary, and `_` and `^` in either
order merge into one script node. Repeating the same script kind starts a new
nested script and produces a warning. Implicit multiplication is structural;
canonical export does not insert a multiplication sign when juxtaposition is
unambiguous.

`&` and `@` act as table separators only inside the four table forms. Commas
act as argument separators only at the current function-call depth. Braces are
accepted grouping for the LaTeX-shaped overlap and direct editing, but
canonical UnicodeMath grouping uses parentheses.

### 6.3 Functions and Calculator-compatible aliases

Function calls render the function name upright and their argument as a
delimited operand. The accepted case-sensitive names are:

- `abs`, `floor`, `ceiling`, `ln`, `log`, `exp`;
- `sin`, `cos`, `tan`, `sec`, `csc`, `cot`;
- `asin`, `acos`, `atan`, `asec`, `acsc`, `acot`;
- `sinh`, `cosh`, `tanh`, `sech`, `csch`, `coth`; and
- `asinh`, `acosh`, `atanh`, `asech`, `acsch`, `acoth`.

`log(x)` is a common logarithm function call. `log(b,x)` creates a log with
base `b` applied to `x`. Import also accepts the current culture's list
separator as described in 6.6.

The structural aliases below are accepted in UnicodeMath input:

| Input alias | Structure |
| --- | --- |
| `sqrt(x)` | square radical of `x` |
| `cbrt(x)` | radical of `x` with degree `3` |
| `root(x,n)` | radical of `x` with degree `n` |
| `abs(x)` | scalable vertical delimiters |
| `floor(x)` | scalable floor delimiters |
| `ceiling(x)` | scalable ceiling delimiters |
| `\frac{a}{b}` | fraction; this is an overlap alias, not general LaTeX mode |

The palette exposes `x`, `y`, `π`, `e`, digits, decimal separator, `+`, `−`,
`×`, `÷`, `/`, `=`, `≠`, `<`, `>`, `≤`, `≥`, powers, square, reciprocal,
parentheses, the functions above, roots, and standard relations. Reciprocal
insertion creates exponent `−1`; square insertion creates exponent `2`.

Direct Unicode characters are preferred. Common Math AutoCorrect controls are
accepted at minimum for Greek letters, `\pi`, `\infty`, `\sqrt`, `\sum`,
`\prod`, `\int`, `\partial`, `\nabla`, `\times`, `\div`, `\pm`, `\mp`,
`\le`, `\ge`, `\ne`, `\approx`, `\equiv`, `\in`, `\notin`, `\subset`,
`\supset`, `\cup`, `\cap`, `\rightarrow`, `\leftarrow`, and `\leftrightarrow`.
The mapping is to the corresponding Unicode scalar before structural parsing.

Interactive typing applies the same case-sensitive symbol vocabulary with a
leading backslash, following the Microsoft Math AutoCorrect command list. The
backslash is optional for a complete bare name such as `pi`, `alpha`, `sum`, or
`subseteq`. A name is replaced only at a token boundary (operator,
punctuation, Space, Enter, or focus loss), so a longer identifier such as
`spin` is not partially rewritten. Supported commands cover the applicable
single-symbol entries in the Greek, set, logic, relation, binary/n-ary
operator, arrow, typography, and geometry groups. The reference vocabulary is
documented at
<https://support.microsoft.com/en-US/accessibility/word/quick-start-guide-to-math-autocorrect-commands-and-symbols>.

Longest-match keyboard ligatures are also replaced as one edit. These include
`<=` → `≤`, `>=` → `≥`, `!=` and `/=` → `≠`, `~=` → `≅`, `+-` → `±`,
`-+` → `∓`, `->` → `→`, `<-` → `←`, `<->` → `↔`, `=>` → `⇒`, and
`<=>` → `⇔`. ASCII hyphen-minus, asterisk, and apostrophe become the Unicode
minus, multiplication, and prime signs in a math zone. This behavior follows
Unicode Technical Report #25 section 4.3 and UnicodeMath 3.3 section 4.

### 6.4 Structural canonical forms

Canonical UnicodeMath output uses invariant ASCII punctuation, NFC text, and
the following forms:

- fractions use `/`, with the minimum parentheses needed to preserve the tree;
- subscript and superscript use `_` and `^`, in that order when both exist;
- square roots use `√(x)` and indexed roots use `√(n&x)`;
- scalable arbitrary delimiters use `\leftL body\rightR`, with direct delimiter
  characters substituted for `L` and `R`; abs/floor/ceiling use their direct
  delimiter characters;
- matrices, cases, and aligned equations use `\matrix(...)`, `\cases(...)`,
  and `\eqarray(...)` with `&` between cells and `@` between rows;
- gathered lines use `\gathered(...)` with `@` between lines;
- n-ary operators use the direct operator followed by lower/upper scripts and
  the operand;
- accents use `\hat`, `\check`, `\breve`, `\tilde`, `\bar`, `\dot`, `\ddot`,
  `\dddot`, or `\vec` followed by a parenthesized body;
- bars and braces use `\overbar`, `\underbar`, `\overbrace`, or `\underbrace`
  followed by a parenthesized body; optional annotations are scripts on that
  construct; and
- error nodes emit their retained raw fragments without repair.

Serialization uses no optional whitespace. Re-importing canonical output and
exporting it again must be byte-for-byte stable.

### 6.5 Explicit spacing

Input accepts U+2009 THIN SPACE, U+205F MEDIUM MATHEMATICAL SPACE, and U+2003 EM
SPACE as explicit spacing nodes. Canonical export uses those characters for
widths `3/18 em`, `4/18 em`, and `18/18 em`, respectively. The editor's second
Space after successful build-up inserts U+205F. Ordinary ASCII spaces that do
not trigger build-up separate tokens and are not preserved.

### 6.6 Localized punctuation

Canonical output always uses `.` as decimal separator and `,` as argument/list
separator. Import additionally accepts the current `CultureInfo` decimal and
list separators. In a decimal-comma culture, `;` is the function argument and
table list separator, so `root(x;3)` and `1,5` are unambiguous. Culture affects
token recognition only; the document and its export are culture-independent.

### 6.7 Build-up and keyboard behavior

- Adding `_` or `^` plus its operand builds a script as soon as the operand is
  complete. Completing a required template operand also builds immediately.
- Space builds the smallest pending UnicodeMath span containing the caret. If
  the immediately preceding Space command successfully built that same span,
  the next Space inserts explicit medium mathematical spacing.
- Enter and focus loss build and normalize the whole document, then raise
  submission. Enter is consumed by the editor unless a host command explicitly
  handles the submission event.
- Tab and Shift+Tab move to the next and previous inferred placeholder in
  document order. From the last/first placeholder they move after/before the
  containing structure, then allow normal focus traversal on the next press.
- Backspace at a structural boundary first selects the adjacent complex node.
  Invoking Backspace again without changing that selection deletes it. Typing
  replaces the selected complex node normally.
- Escape collapses a structural selection without deleting it.

Required black-box compatibility observations:

| Input/action | Required result |
| --- | --- |
| type `x^2` | script structure builds immediately |
| type `1/2`, press Space | fraction structure |
| type `sqrt(x)`, submit | square-radical structure |
| type `root(x,n)`, submit | indexed-radical structure |
| type `\frac{1}{2}`, build/submit | same fraction as `1/2` |

These observations define the compatibility claim; no implementation-internal
behavior is implied.

## 7. LaTeX interchange subset

LaTeX mode is a separate parser/serializer, except for the explicitly accepted
`\frac` alias in UnicodeMath mode. It supports every document node in section
4 using a safe, package-free mathematical subset:

- groups, Unicode/ASCII atoms, `+ - \times \div`, standard relations;
- `\frac`, `\sqrt` and `\sqrt[n]`, `_`, `^`;
- `\left`/`\right` delimiters, `\lvert`, `\rvert`, `\lfloor`, `\rfloor`,
  `\lceil`, `\rceil`;
- `\sum`, `\prod`, `\coprod`, `\int`, `\iint`, `\iiint`, and limits;
- the named functions in 6.3 with `\operatorname{...}` only when no standard
  command exists;
- standard accent commands, `\overline`, `\underline`, `\overbrace`, and
  `\underbrace`;
- `matrix`, `cases`, `aligned`, and `gathered` environments; and
- `\,`, `\:`, and `\quad` for the three explicit spacing widths.

Canonical LaTeX contains no preamble, dollar delimiters, or package commands.
It uses braces consistently, `\\` between table rows, and `&` between cells.
Direct Unicode characters without a safe standard command are retained as
Unicode. Comments, command definitions, arbitrary macros, includes, file I/O,
and package-dependent extensions are unsupported and preserved as visible
error nodes. Parser behavior is bounded by the common limits in section 9.

## 8. Presentation MathML interchange

Import accepts a safe Presentation MathML subset and common compatibility form
`mfenced`. It recognizes at least:

`math`, `mrow`, `mi`, `mn`, `mo`, `mtext`, `mspace`, `mfrac`, `msqrt`, `mroot`,
`msub`, `msup`, `msubsup`, `munder`, `mover`, `munderover`, `mtable`, `mtr`,
`mtd`, `menclose`, `merror`, `semantics`, and `annotation` only as constrained
below.

`semantics` imports its first Presentation MathML child. Plain-text annotation
may be inspected only as a fallback when there is no presentation child and
its declared encoding is an accepted UnicodeMath or LaTeX label. Other
annotations are ignored with a diagnostic. `mfenced` becomes a delimiter node;
its `open`, `close`, and `separators` values are treated as text, never markup.

Export emits MathML Core in the namespace
`http://www.w3.org/1998/Math/MathML`, rooted at `math`, with only necessary
attributes and invariant numeric values. Tables use `mtable`/`mtr`/`mtd` and a
repository-owned `data-math-composer-layout` value to preserve the four layout
kinds; import also infers cases from brace-fenced tables and otherwise defaults
to matrix with a diagnostic when kind cannot be known. Error nodes export as
`<merror><mtext>raw fragment</mtext></merror>` with normal XML escaping.

### 8.1 XML safety

MathML import must use a forward-only or DOM XML API configured to prohibit
DTD processing and external resolution. DTDs, entity declarations, processing
instructions, XInclude, XSLT, scripts, event-handler attributes, links,
external resources, and Content MathML are never executed or fetched. No
network or filesystem access may result from input. Unknown elements become a
visible error representing their complete bounded fragment; unknown attributes
are ignored with a diagnostic unless they create a security rejection.

## 9. Resource limits and recovery

The following limits apply before or during every import format and clipboard
paste:

| Limit | Value | Result when exceeded |
| --- | ---: | --- |
| UTF-8 input size | 1 MiB | fatal diagnostic and single error document |
| Document nodes | 50,000 | fatal diagnostic and single error document |
| Structural/XML depth | 256 | fatal diagnostic and single error document |
| Individual token | 16 KiB UTF-8 | fatal diagnostic and single error document |

Checks must avoid integer overflow and excessive allocation. XML limits count
elements and produced document nodes. Parser work must be bounded by a small
constant multiple of input length for ordinary input; adversarial tests must
not cause unbounded recursion, hangs, stack overflow, or exponential work.

Malformed input below a fatal limit produces diagnostics and the maximum safe
recovered document. Export of any recovered document remains deterministic and
must not throw.

## 10. Editing and history

Every document-changing operation is a pure core edit that accepts a document
and selection and returns a new document, selection, and diagnostics. The
control wraps those operations in bounded undo/redo history.

- One logical user command creates one undo unit. IME composition from start
  through commit is one unit. Continuous text insertion may coalesce until a
  navigation, selection, structural, clipboard, or build command occurs.
- Undo restores both document and selection; redo reverses that undo.
- A new edit after undo clears redo. Loading a document clears both stacks.
- `HistoryCapacity = 0` disables recording without disabling edits.
- Selection-only navigation is not added to document history.
- External replacement through the `Document` property creates one undo unit
  when the control is active, except initial binding and `Load`.

Keyboard navigation supports Unicode-scalar left/right movement, row-aware
up/down movement using preferred horizontal position, Home/End within the
current structural row, and platform-standard selection modifiers. Pointer hit
testing chooses the nearest legal caret stop. Drag selection retains anchor and
updates active position across structural boundaries. Touch dragging must not
require sub-pixel precision.

IME preedit text is a visual composition range and is not committed to the
immutable document until the platform commits it. Cancellation restores the
pre-composition state.

## 11. Clipboard

When the platform supports multiple flavors, copy writes all of:

1. MathML (`application/mathml+xml`),
2. LaTeX (`application/x-latex` or the platform's registered equivalent), and
3. canonical UnicodeMath as plain text.

Paste preference is valid safe MathML, then explicitly labeled LaTeX, then
plain-text UnicodeMath. A higher-priority flavor that is present but fatally
invalid is reported and skipped in favor of the next valid flavor. Unlabeled
plain text is never guessed as LaTeX.

The editor maintains an in-process clipboard fallback containing the same
three representations. It is used when browser permission/security prevents a
system read or write. The fallback is scoped to the running app instance and
cleared on reload; the demo visibly indicates fallback use without treating it
as an error.

## 12. OpenType MATH renderer

The renderer uses the exact vendored `assets/fonts/XCharter-Math.otf`, whose
SHA-256 is
`012428f9b13b307cc9dc71eb7525fac9eec1091f23368ae17bfec9da6841e228`.
It must verify or test this checksum and package the font as an Avalonia asset.

An original, bounds-checked OpenType reader parses the sfnt table directory,
`head`, `maxp`, `cmap`, horizontal metrics needed by rendering, glyph bounds,
and the MATH header and subtables. MATH support includes:

- all constants needed for scripts, limits, stacks, fractions, bars, radicals,
  and display operators;
- per-glyph italic corrections and top-accent attachments;
- extended-shape coverage and math kern information used by supported layout;
- horizontal and vertical size variants; and
- glyph assemblies, connector overlaps, and assembly italic corrections.

Every offset/count/length read is range checked. Invalid optional font data
falls back to documented conservative metrics with a diagnostic; a missing or
fundamentally invalid required font fails initialization visibly rather than
silently switching the whole formula to another math font.

Layout is a recursive box calculation in device-independent pixels. Font design
units are scaled from `unitsPerEm` and `MathFontSize`. Fractions, scripts,
radicals, delimiters, accents, n-ary limits, bars/braces, and table baselines use
the corresponding MATH metrics. Delimiters and accents choose the smallest
variant that meets the target or build an assembly with the specified connector
overlap. Ordinary text and genuinely missing glyphs may use platform fallback;
all available math glyphs use XCharter Math.

The render tree also produces legal caret stops and rectangles, selection
geometry, placeholder geometry, and a deterministic nearest-stop hit-test map.
Layout and hit testing use the same boxes. Pixel snapping is applied only at
draw time so it cannot change document geometry.

## 13. Demo and targets

The desktop and browser hosts use the same shared demo view. It includes the
editor, source-format selector, import/export areas, diagnostic display,
undo/redo and clipboard controls, structural palette, and representative sample
expressions. It performs no evaluation.

The Browser launch profile binds to `http://0.0.0.0:5237`. The README added
during implementation must explain how to determine the Mac's LAN IP and open
`http://<mac-lan-ip>:5237` from a phone on the same network.

At a narrow phone viewport:

- the rendered expression remains the primary, full-width region;
- the palette moves into a drawer;
- nonessential panels stack below the editor;
- interactive targets are at least 44 by 44 CSS pixels; and
- the page does not require horizontal viewport scrolling.

## 14. Verification and acceptance

All tests must be deterministic and runnable from repository commands. Random
or fuzz tests print/replay their seed on failure.

### 14.1 Core parser and serializer

- Golden tests cover `x^2`, `1/2`, `sqrt(x)`, `root(x,n)`, `\frac{1}{2}`,
  implicit multiplication, direct symbols, localized punctuation, every
  function family, matrices, cases, accents, n-ary expressions, aligned
  equations, and gathered lines.
- Every supported node round-trips structurally through UnicodeMath, LaTeX, and
  MathML. Canonical export is stable on a second parse/export.
- Malformed UnicodeMath/LaTeX retains visible raw fragments. Malformed/unknown
  MathML retains visible escaped content in an error node.
- Decimal-comma tests run under an explicit culture and do not depend on the
  machine culture.
- Fuzzed text and XML exercise truncation, bad nesting, surrogate edge cases,
  huge tokens, and all fatal limits.
- XML tests prove DTD/entity/external-resource processing is disabled by using
  a resolver that fails the test if invoked.

### 14.2 Editing and control interaction

Headless Avalonia tests cover pointer placement, drag selection, scalar-aware
keyboard navigation, placeholder traversal, IME commit/cancel, all build-up
triggers, history coalescing and capacity, two-stage complex-node deletion,
palette insertion, read-only command state, and clipboard flavor precedence and
fallback.

Event tests assert ordering, payloads, and the one-change-event-per-command
rule. Accessibility exposes an editable math control name/value and does not
hide errors or read-only state.

### 14.3 Font and layout

- Verify the embedded font SHA-256.
- Decode known invariants directly from its MATH table without copying literal
  metric tables into source.
- Test design-unit scaling and metrics for fractions, scripts, roots, limits,
  accents, and under/over constructs.
- Exercise both glyph-variant and glyph-assembly stretching.
- Verify caret rectangles and hit-test round trips at multiple sizes.
- Store deterministic headless snapshots for representative formulas and
  structural selections; intentional changes require review.
- Corrupt/truncated synthetic font inputs never read out of bounds or hang.

### 14.4 Browser and build matrix

Playwright runs against desktop and narrow-phone viewports and verifies WASM
startup, an editing/rendering smoke case, responsive drawer behavior, 44-pixel
touch targets, no horizontal overflow, no unexpected browser-console errors,
and internal clipboard fallback.

CI builds every project and target. Release acceptance additionally requires a
successful macOS desktop run and local browser run. Windows and Linux runtime
QA and physical-phone QA are explicitly deferred, though their projects must
compile.

### 14.5 Definition of done

Version 1 is complete only when all public APIs and supported nodes above are
implemented, all three canonical formats pass round-trip/recovery tests, the
custom MATH renderer is used on desktop and WASM, every target builds, the
specified automated suites pass, and macOS plus local-browser runtime
acceptance has been recorded. Passing parser tests alone is not completion.
