using System.Buffers;
using System.Globalization;
using System.Text;

namespace MathComposer.Core;

/// <summary>Applies boundary-aware UnicodeMath symbol substitutions while typing.</summary>
internal static class MathAutoCorrect
{
    private static readonly Dictionary<string, string> Symbols =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Greek letters and variants.
            ["alpha"] = "α",
            ["Alpha"] = "Α",
            ["beta"] = "β",
            ["Beta"] = "Β",
            ["gamma"] = "γ",
            ["Gamma"] = "Γ",
            ["delta"] = "δ",
            ["Delta"] = "Δ",
            ["epsilon"] = "ε",
            ["Epsilon"] = "Ε",
            ["varepsilon"] = "ϵ",
            ["zeta"] = "ζ",
            ["Zeta"] = "Ζ",
            ["eta"] = "η",
            ["Eta"] = "Η",
            ["theta"] = "θ",
            ["Theta"] = "Θ",
            ["vartheta"] = "ϑ",
            ["iota"] = "ι",
            ["Iota"] = "Ι",
            ["kappa"] = "κ",
            ["Kappa"] = "Κ",
            ["varkappa"] = "ϰ",
            ["lambda"] = "λ",
            ["Lambda"] = "Λ",
            ["mu"] = "μ",
            ["Mu"] = "Μ",
            ["nu"] = "ν",
            ["Nu"] = "Ν",
            ["xi"] = "ξ",
            ["Xi"] = "Ξ",
            ["omicron"] = "ο",
            ["Omicron"] = "Ο",
            ["pi"] = "π",
            ["Pi"] = "Π",
            ["varpi"] = "ϖ",
            ["rho"] = "ρ",
            ["Rho"] = "Ρ",
            ["varrho"] = "ϱ",
            ["sigma"] = "σ",
            ["Sigma"] = "Σ",
            ["varsigma"] = "ς",
            ["tau"] = "τ",
            ["Tau"] = "Τ",
            ["upsilon"] = "υ",
            ["Upsilon"] = "Υ",
            ["phi"] = "φ",
            ["Phi"] = "Φ",
            ["varphi"] = "ϕ",
            ["chi"] = "χ",
            ["Chi"] = "Χ",
            ["psi"] = "ψ",
            ["Psi"] = "Ψ",
            ["omega"] = "ω",
            ["Omega"] = "Ω",

            // Constants, quantifiers, sets, and letter-like symbols.
            ["infty"] = "∞",
            ["infinity"] = "∞",
            ["forall"] = "∀",
            ["foreach"] = "∀",
            ["exists"] = "∃",
            ["forsome"] = "∃",
            ["nexists"] = "∄",
            ["emptyset"] = "∅",
            ["complement"] = "∁",
            ["partial"] = "∂",
            ["nabla"] = "∇",
            ["grad"] = "∇",
            ["aleph"] = "ℵ",
            ["beth"] = "ℶ",
            ["gimel"] = "ℷ",
            ["daleth"] = "ℸ",
            ["hbar"] = "ℏ",
            ["wp"] = "℘",
            ["powerset"] = "℘",
            ["Re"] = "ℜ",
            ["Im"] = "ℑ",
            ["doubleC"] = "ℂ",
            ["doubleE"] = "𝔼",
            ["doubleN"] = "ℕ",
            ["doubleP"] = "ℙ",
            ["doubleQ"] = "ℚ",
            ["doubleR"] = "ℝ",
            ["doubleZ"] = "ℤ",

            // Arithmetic, binary, and n-ary operators.
            ["pm"] = "±",
            ["mp"] = "∓",
            ["times"] = "×",
            ["div"] = "÷",
            ["ldiv"] = "∕",
            ["ast"] = "∗",
            ["bullet"] = "∙",
            ["cdot"] = "⋅",
            ["circ"] = "∘",
            ["comp"] = "∘",
            ["star"] = "⋆",
            ["diamond"] = "⋄",
            ["cap"] = "∩",
            ["intersection"] = "∩",
            ["cup"] = "∪",
            ["union"] = "∪",
            ["sqcap"] = "⊓",
            ["sqcup"] = "⊔",
            ["uplus"] = "⊎",
            ["wedge"] = "∧",
            ["land"] = "∧",
            ["and"] = "∧",
            ["vee"] = "∨",
            ["lor"] = "∨",
            ["or"] = "∨",
            ["nand"] = "⊼",
            ["nor"] = "⊽",
            ["xor"] = "⊕",
            ["xnor"] = "⊙",
            ["oplus"] = "⊕",
            ["ominus"] = "⊖",
            ["otimes"] = "⊗",
            ["oslash"] = "⊘",
            ["odot"] = "⊙",
            ["circledot"] = "⊙",
            ["oast"] = "⊛",
            ["ocirc"] = "⊚",
            ["odash"] = "⊝",
            ["boxplus"] = "⊞",
            ["boxminus"] = "⊟",
            ["boxtimes"] = "⊠",
            ["boxdot"] = "⊡",
            ["setminus"] = "∖",
            ["dagger"] = "†",
            ["ddag"] = "‡",
            ["wr"] = "≀",
            ["sum"] = "∑",
            ["prod"] = "∏",
            ["coprod"] = "∐",
            ["amalg"] = "∐",
            ["int"] = "∫",
            ["iint"] = "∬",
            ["iiint"] = "∭",
            ["oint"] = "∮",
            ["oiint"] = "∯",
            ["oiiint"] = "∰",
            ["bigcap"] = "⋂",
            ["bigcup"] = "⋃",
            ["bigwedge"] = "⋀",
            ["bigvee"] = "⋁",
            ["bigoplus"] = "⨁",
            ["bigotimes"] = "⨂",
            ["bigodot"] = "⨀",
            ["biguplus"] = "⨄",

            // Relations.
            ["ne"] = "≠",
            ["neq"] = "≠",
            ["le"] = "≤",
            ["leq"] = "≤",
            ["ge"] = "≥",
            ["geq"] = "≥",
            ["ll"] = "≪",
            ["muchless"] = "≪",
            ["gg"] = "≫",
            ["muchgreater"] = "≫",
            ["sim"] = "∼",
            ["simeq"] = "≃",
            ["approx"] = "≈",
            ["cong"] = "≅",
            ["equiv"] = "≡",
            ["propto"] = "∝",
            ["asymp"] = "≍",
            ["in"] = "∈",
            ["element"] = "∈",
            ["belongs"] = "∈",
            ["notin"] = "∉",
            ["ni"] = "∋",
            ["contains"] = "∋",
            ["owns"] = "∋",
            ["subset"] = "⊂",
            ["supset"] = "⊃",
            ["subseteq"] = "⊆",
            ["supseteq"] = "⊇",
            ["sqsubset"] = "⊏",
            ["sqsupset"] = "⊐",
            ["sqsubseteq"] = "⊑",
            ["sqsupseteq"] = "⊒",
            ["prec"] = "≺",
            ["succ"] = "≻",
            ["preceq"] = "≼",
            ["succeq"] = "≽",
            ["parallel"] = "∥",
            ["nparallel"] = "∦",
            ["notparallel"] = "∦",
            ["perp"] = "⊥",
            ["bot"] = "⊥",
            ["top"] = "⊤",
            ["vdash"] = "⊢",
            ["dashv"] = "⊣",
            ["models"] = "⊨",
            ["bowtie"] = "⋈",
            ["join"] = "⋈",
            ["therefore"] = "∴",
            ["because"] = "∵",

            // Arrows.
            ["leftarrow"] = "←",
            ["gets"] = "←",
            ["rightarrow"] = "→",
            ["uparrow"] = "↑",
            ["downarrow"] = "↓",
            ["leftrightarrow"] = "↔",
            ["updownarrow"] = "↕",
            ["Leftarrow"] = "⇐",
            ["Rightarrow"] = "⇒",
            ["Uparrow"] = "⇑",
            ["Downarrow"] = "⇓",
            ["Leftrightarrow"] = "⇔",
            ["Updownarrow"] = "⇕",
            ["implies"] = "→",
            ["implication"] = "→",
            ["Implies"] = "⇒",
            ["Implication"] = "⇒",
            ["biconditional"] = "↔",
            ["Biconditional"] = "⇔",
            ["longleftarrow"] = "⟵",
            ["longrightarrow"] = "⟶",
            ["longleftrightarrow"] = "⟷",
            ["Longleftarrow"] = "⟸",
            ["Longrightarrow"] = "⟹",
            ["Longleftrightarrow"] = "⟺",
            ["nearrow"] = "↗",
            ["nwarrow"] = "↖",
            ["searrow"] = "↘",
            ["swarrow"] = "↙",
            ["mapsto"] = "↦",
            ["hookleftarrow"] = "↩",
            ["hookrightarrow"] = "↪",
            ["leftharpoonup"] = "↼",
            ["leftharpoondown"] = "↽",
            ["rightharpoonup"] = "⇀",
            ["rightharpoondown"] = "⇁",
            ["leftrightharpoons"] = "⇋",
            ["rightleftharpoons"] = "⇌",

            // Roots, typography, and geometry.
            ["sqrt"] = "√",
            ["cbrt"] = "∛",
            ["qdrt"] = "∜",
            ["deg"] = "°",
            ["degree"] = "°",
            ["angle"] = "∠",
            ["rightangle"] = "∟",
            ["triangle"] = "△",
            ["circle"] = "◯",
            ["mid"] = "∣",
            ["divide"] = "∣",
            ["nmid"] = "∤",
            ["notdivide"] = "∤",
            ["vdots"] = "⋮",
            ["cdots"] = "⋯",
            ["rddots"] = "⋰",
            ["ddots"] = "⋱",
            ["prime"] = "′",
            ["dprime"] = "″",
            ["doubleprime"] = "″",
            ["tprime"] = "‴",
            ["tripleprime"] = "‴",
            ["qprime"] = "⁗",
            ["quadprime"] = "⁗"
        };

    private static readonly KeyValuePair<string, string>[] KeySequences =
    [
        new("<=>", "⇔"), new("<->", "↔"),
        new("≤>", "⇔"), new("←>", "↔"),
        new("<=", "≤"), new(">=", "≥"), new("!=", "≠"), new("/=", "≠"),
        new("~=", "≅"), new("+-", "±"), new("-+", "∓"),
        new("−+", "∓"), new("−>", "→"), new("−|", "⊣"),
        new("=>", "⇒"), new("->", "→"), new("<-", "←"),
        new("<<", "≪"), new(">>", "≫"), new("|-", "⊢"), new("-|", "⊣"),
        new("|=", "⊨"), new("||", "∥"), new("&&", "∧"),
        new(":=", "≔"), new("=:", "≕"), new("...", "⋯"),
        new("-", "−"), new("*", "×"), new("'", "′")
    ];

    private static readonly HashSet<string> RelationSymbols =
    [
        "=", "≠", "<", ">", "≤", "≥", "≪", "≫", "≮", "≯", "≰", "≱",
        "∼", "≃", "≈", "≅", "≡", "≢", "∝", "≍",
        "∈", "∉", "∋", "∌", "⊂", "⊃", "⊆", "⊇", "⊏", "⊐", "⊑", "⊒",
        "≺", "≻", "≼", "≽", "∥", "∦", "⊥", "⊢", "⊣", "⊨", "⋈",
        "∣", "∤", "≔", "≕"
    ];

    public static bool TryGetSymbol(string controlWord, out string symbol) =>
        Symbols.TryGetValue(controlWord, out symbol!);

    public static string Substitute(string source, bool completeTrailingWord)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new StringBuilder(source.Length);
        int position = 0;
        while (position < source.Length)
        {
            if (TryAppendKeySequence(source, ref position, result))
            {
                continue;
            }

            if (source[position] == '\\' &&
                TryAppendControlWord(source, ref position, result, completeTrailingWord))
            {
                continue;
            }

            if (IsAsciiLetter(source[position]) &&
                TryAppendBareWord(source, ref position, result, completeTrailingWord))
            {
                continue;
            }

            Rune.DecodeFromUtf16(source.AsSpan(position), out Rune rune, out int consumed);
            result.Append(rune.ToString());
            position += consumed;
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    public static MathAtomClass Classify(Rune rune)
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark or
            UnicodeCategory.ConnectorPunctuation)
        {
            return MathAtomClass.Identifier;
        }

        if (category == UnicodeCategory.DecimalDigitNumber || rune.Value == '.')
        {
            return MathAtomClass.Number;
        }

        string value = rune.ToString();
        if (RelationSymbols.Contains(value))
        {
            return MathAtomClass.Relation;
        }

        if (value is "," or ";" or ":")
        {
            return MathAtomClass.Punctuation;
        }

        return Rune.IsWhiteSpace(rune) ? MathAtomClass.OrdinaryText : MathAtomClass.Operator;
    }

    private static bool TryAppendKeySequence(
        string source,
        ref int position,
        StringBuilder result)
    {
        foreach ((string sequence, string symbol) in KeySequences)
        {
            if (source.AsSpan(position).StartsWith(sequence, StringComparison.Ordinal))
            {
                result.Append(symbol);
                position += sequence.Length;
                return true;
            }
        }

        return false;
    }

    private static bool TryAppendControlWord(
        string source,
        ref int position,
        StringBuilder result,
        bool completeTrailingWord)
    {
        int wordStart = position + 1;
        int wordEnd = wordStart;
        while (wordEnd < source.Length && IsAsciiLetter(source[wordEnd]))
        {
            wordEnd++;
        }

        if (wordEnd == wordStart)
        {
            return false;
        }

        bool complete = wordEnd < source.Length || completeTrailingWord;
        string word = source[wordStart..wordEnd];
        if (complete && Symbols.TryGetValue(word, out string? symbol))
        {
            result.Append(symbol);
        }
        else
        {
            result.Append(source, position, wordEnd - position);
        }

        position = wordEnd;
        return true;
    }

    private static bool TryAppendBareWord(
        string source,
        ref int position,
        StringBuilder result,
        bool completeTrailingWord)
    {
        int wordEnd = position + 1;
        while (wordEnd < source.Length && IsAsciiLetter(source[wordEnd]))
        {
            wordEnd++;
        }

        bool complete = wordEnd < source.Length || completeTrailingWord;
        string word = source[position..wordEnd];
        if (complete && Symbols.TryGetValue(word, out string? symbol))
        {
            result.Append(symbol);
        }
        else
        {
            result.Append(word);
        }

        position = wordEnd;
        return true;
    }

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
