using System.Collections.Immutable;

namespace MathComposer.Avalonia.OpenType;

/// <summary>Names the design-unit records in an OpenType MathConstants table.</summary>
public enum OpenTypeMathConstant
{
    /// <summary>Extra leading above mathematical layout.</summary>
    MathLeading,
    /// <summary>Vertical position of the mathematical axis.</summary>
    AxisHeight,
    /// <summary>Maximum base height for unflattened accents.</summary>
    AccentBaseHeight,
    /// <summary>Maximum base height for flattened accents.</summary>
    FlattenedAccentBaseHeight,
    /// <summary>Default downward subscript shift.</summary>
    SubscriptShiftDown,
    /// <summary>Maximum permitted subscript top position.</summary>
    SubscriptTopMax,
    /// <summary>Minimum baseline drop for a subscript.</summary>
    SubscriptBaselineDropMin,
    /// <summary>Default upward superscript shift.</summary>
    SuperscriptShiftUp,
    /// <summary>Upward superscript shift in cramped style.</summary>
    SuperscriptShiftUpCramped,
    /// <summary>Minimum permitted superscript bottom position.</summary>
    SuperscriptBottomMin,
    /// <summary>Maximum baseline drop for a superscript.</summary>
    SuperscriptBaselineDropMax,
    /// <summary>Minimum gap between simultaneous subscript and superscript.</summary>
    SubSuperscriptGapMin,
    /// <summary>Maximum superscript bottom when a subscript is present.</summary>
    SuperscriptBottomMaxWithSubscript,
    /// <summary>Horizontal space following a script.</summary>
    SpaceAfterScript,
    /// <summary>Minimum gap below an upper limit.</summary>
    UpperLimitGapMin,
    /// <summary>Minimum baseline rise for an upper limit.</summary>
    UpperLimitBaselineRiseMin,
    /// <summary>Minimum gap above a lower limit.</summary>
    LowerLimitGapMin,
    /// <summary>Minimum baseline drop for a lower limit.</summary>
    LowerLimitBaselineDropMin,
    /// <summary>Upward shift for the top of a stack.</summary>
    StackTopShiftUp,
    /// <summary>Display-style upward shift for the top of a stack.</summary>
    StackTopDisplayStyleShiftUp,
    /// <summary>Downward shift for the bottom of a stack.</summary>
    StackBottomShiftDown,
    /// <summary>Display-style downward shift for the bottom of a stack.</summary>
    StackBottomDisplayStyleShiftDown,
    /// <summary>Minimum gap between stack elements.</summary>
    StackGapMin,
    /// <summary>Display-style minimum gap between stack elements.</summary>
    StackDisplayStyleGapMin,
    /// <summary>Upward shift for a stretched stack top.</summary>
    StretchStackTopShiftUp,
    /// <summary>Downward shift for a stretched stack bottom.</summary>
    StretchStackBottomShiftDown,
    /// <summary>Minimum gap above a stretched stack operator.</summary>
    StretchStackGapAboveMin,
    /// <summary>Minimum gap below a stretched stack operator.</summary>
    StretchStackGapBelowMin,
    /// <summary>Upward shift for a fraction numerator.</summary>
    FractionNumeratorShiftUp,
    /// <summary>Display-style upward shift for a fraction numerator.</summary>
    FractionNumeratorDisplayStyleShiftUp,
    /// <summary>Downward shift for a fraction denominator.</summary>
    FractionDenominatorShiftDown,
    /// <summary>Display-style downward shift for a fraction denominator.</summary>
    FractionDenominatorDisplayStyleShiftDown,
    /// <summary>Minimum gap below a fraction numerator.</summary>
    FractionNumeratorGapMin,
    /// <summary>Display-style minimum gap below a fraction numerator.</summary>
    FractionNumeratorDisplayStyleGapMin,
    /// <summary>Thickness of a fraction rule.</summary>
    FractionRuleThickness,
    /// <summary>Minimum gap above a fraction denominator.</summary>
    FractionDenominatorGapMin,
    /// <summary>Display-style minimum gap above a fraction denominator.</summary>
    FractionDenominatorDisplayStyleGapMin,
    /// <summary>Horizontal gap beside a skewed fraction slash.</summary>
    SkewedFractionHorizontalGap,
    /// <summary>Vertical gap beside a skewed fraction slash.</summary>
    SkewedFractionVerticalGap,
    /// <summary>Gap between a base and an overbar.</summary>
    OverbarVerticalGap,
    /// <summary>Thickness of an overbar rule.</summary>
    OverbarRuleThickness,
    /// <summary>Extra ascender reserved above an overbar.</summary>
    OverbarExtraAscender,
    /// <summary>Gap between a base and an underbar.</summary>
    UnderbarVerticalGap,
    /// <summary>Thickness of an underbar rule.</summary>
    UnderbarRuleThickness,
    /// <summary>Extra descender reserved below an underbar.</summary>
    UnderbarExtraDescender,
    /// <summary>Gap between a radicand and radical rule.</summary>
    RadicalVerticalGap,
    /// <summary>Display-style gap between a radicand and radical rule.</summary>
    RadicalDisplayStyleVerticalGap,
    /// <summary>Thickness of a radical rule.</summary>
    RadicalRuleThickness,
    /// <summary>Extra ascender reserved above a radical.</summary>
    RadicalExtraAscender,
    /// <summary>Horizontal kern before a radical degree.</summary>
    RadicalKernBeforeDegree,
    /// <summary>Horizontal kern after a radical degree.</summary>
    RadicalKernAfterDegree
}
