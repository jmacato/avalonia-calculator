using System.Collections.Generic;
using System.Linq;
using CSharpMath.Display;
using CSharpMath.Display.FrontEnd;
using HarfBuzzSharp;

namespace CSharpMath.Rendering.BackEnd;

public sealed class MathTable : FontMathTable<MathFontSet, Glyph>
{
    private MathTable()
    {
    }

    public static MathTable Instance { get; } = new();

    private static float ReadConstant(OpenTypeMathConstant constant, MathFontSet fonts) =>
        fonts.MathTypeface.GetConstant(constant) * fonts.ScaleFor(fonts.MathTypeface);

    protected override short ScriptPercentScaleDown(MathFontSet fonts) =>
        checked((short)fonts.MathTypeface.GetConstant(OpenTypeMathConstant.ScriptPercentScaleDown));

    protected override short ScriptScriptPercentScaleDown(MathFontSet fonts) =>
        checked((short)fonts.MathTypeface.GetConstant(OpenTypeMathConstant.ScriptScriptPercentScaleDown));

    public override float AxisHeight(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.AxisHeight, fonts);

    public override float FractionDenomDisplayStyleGapMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionDenomDisplayStyleGapMin, fonts);

    public override float FractionDenominatorDisplayStyleShiftDown(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionDenominatorDisplayStyleShiftDown, fonts);

    public override float FractionDenominatorGapMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionDenominatorGapMin, fonts);

    public override float FractionDenominatorShiftDown(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionDenominatorShiftDown, fonts);

    public override float FractionNumDisplayStyleGapMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionNumDisplayStyleGapMin, fonts);

    public override float FractionNumeratorDisplayStyleShiftUp(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionNumeratorDisplayStyleShiftUp, fonts);

    public override float FractionNumeratorGapMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionNumeratorGapMin, fonts);

    public override float FractionNumeratorShiftUp(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionNumeratorShiftUp, fonts);

    public override float FractionRuleThickness(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionRuleThickness, fonts);

    private static (IEnumerable<Glyph> Variants, int Count) GetVariants(
        Glyph glyph,
        Direction direction)
    {
        OpenTypeMathGlyphVariant[] variants = glyph.Typeface.GetVariants(glyph.GlyphId, direction);
        return variants.Length == 0
            ? (new[] { glyph }, 1)
            : (variants.Select(variant =>
                new Glyph(glyph.Typeface, checked((ushort)variant.Glyph))), variants.Length);
    }

    public override (IEnumerable<Glyph> variants, int count) GetHorizontalVariantsForGlyph(Glyph rawGlyph) =>
        GetVariants(rawGlyph, Direction.LeftToRight);

    public override (IEnumerable<Glyph> variants, int count) GetVerticalVariantsForGlyph(Glyph rawGlyph) =>
        GetVariants(rawGlyph, Direction.TopToBottom);

    public override float GetItalicCorrection(MathFontSet fonts, Glyph glyph) =>
        glyph.Typeface.GetItalicCorrection(glyph.GlyphId) * fonts.ScaleFor(glyph.Typeface);

    public override Glyph GetLargerGlyph(MathFontSet fonts, Glyph glyph)
    {
        foreach (OpenTypeMathGlyphVariant variant in
                 glyph.Typeface.GetVariants(glyph.GlyphId, Direction.TopToBottom))
        {
            ushort variantId = checked((ushort)variant.Glyph);
            if (variantId != glyph.GlyphId)
            {
                return new Glyph(glyph.Typeface, variantId);
            }
        }

        return glyph;
    }

    public override IEnumerable<GlyphPart<Glyph>>? GetVerticalGlyphAssembly(Glyph rawGlyph, MathFontSet fonts)
    {
        OpenTypeMathGlyphPart[] parts =
            rawGlyph.Typeface.GetAssembly(rawGlyph.GlyphId, Direction.TopToBottom);
        if (parts.Length == 0)
        {
            return null;
        }

        float scale = fonts.ScaleFor(rawGlyph.Typeface);
        return parts.Select(part => new GlyphPart<Glyph>(
            new Glyph(rawGlyph.Typeface, checked((ushort)part.Glyph)),
            part.FullAdvance * scale,
            part.StartConnectorLength * scale,
            part.EndConnectorLength * scale,
            (part.Flags & OpenTypeMathGlyphPartFlags.Extender) != 0));
    }

    public override float LowerLimitBaselineDropMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.LowerLimitBaselineDropMin, fonts);

    public override float LowerLimitGapMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.LowerLimitGapMin, fonts);

    public override float MinConnectorOverlap(MathFontSet fonts) =>
        fonts.MathTypeface.GetMinConnectorOverlap(Direction.TopToBottom) *
        fonts.ScaleFor(fonts.MathTypeface);

    protected override short RadicalDegreeBottomRaisePercent(MathFontSet fonts) =>
        checked((short)fonts.MathTypeface.GetConstant(OpenTypeMathConstant.RadicalDegreeBottomRaisePercent));

    public override float RadicalDisplayStyleVerticalGap(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalDisplayStyleVerticalGap, fonts);

    public override float RadicalExtraAscender(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalExtraAscender, fonts);

    public override float RadicalKernAfterDegree(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalKernAfterDegree, fonts);

    public override float RadicalKernBeforeDegree(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalKernBeforeDegree, fonts);

    public override float RadicalRuleThickness(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalRuleThickness, fonts);

    public override float RadicalVerticalGap(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalVerticalGap, fonts);

    public override float SpaceAfterScript(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SpaceAfterScript, fonts);

    public override float StackBottomDisplayStyleShiftDown(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.StackBottomDisplayStyleShiftDown, fonts);

    public override float StackBottomShiftDown(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.StackBottomShiftDown, fonts);

    public override float StackDisplayStyleGapMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.StackDisplayStyleGapMin, fonts);

    public override float StackGapMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.StackGapMin, fonts);

    public override float StackTopDisplayStyleShiftUp(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.StackTopDisplayStyleShiftUp, fonts);

    public override float StackTopShiftUp(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.StackTopShiftUp, fonts);

    public override float SubscriptBaselineDropMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SubscriptBaselineDropMin, fonts);

    public override float SubscriptShiftDown(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SubscriptShiftDown, fonts);

    public override float SubscriptTopMax(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SubscriptTopMax, fonts);

    public override float SubSuperscriptGapMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SubSuperscriptGapMin, fonts);

    public override float SuperscriptBaselineDropMax(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptBaselineDropMax, fonts);

    public override float SuperscriptBottomMaxWithSubscript(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptBottomMaxWithSubscript, fonts);

    public override float SuperscriptBottomMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptBottomMin, fonts);

    public override float SuperscriptShiftUp(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptShiftUp, fonts);

    public override float SuperscriptShiftUpCramped(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptShiftUpCramped, fonts);

    public override float UpperLimitBaselineRiseMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.UpperLimitBaselineRiseMin, fonts);

    public override float UpperLimitGapMin(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.UpperLimitGapMin, fonts);

    public override float UnderbarVerticalGap(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.UnderbarVerticalGap, fonts);

    public override float AccentBaseHeight(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.AccentBaseHeight, fonts);

    public override float GetTopAccentAdjustment(MathFontSet fonts, Glyph glyph)
    {
        int attachment = glyph.Typeface.GetTopAccentAttachment(glyph.GlyphId);
        if (attachment == 0)
        {
            attachment = checked((int)(glyph.Typeface.GetAdvance(glyph.GlyphId) / 2));
        }

        return attachment * fonts.ScaleFor(glyph.Typeface);
    }

    public override float UnderbarRuleThickness(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.UnderbarRuleThickness, fonts);

    public override float OverbarVerticalGap(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.OverbarVerticalGap, fonts);

    public override float OverbarRuleThickness(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.OverbarRuleThickness, fonts);

    public override float OverbarExtraAscender(MathFontSet fonts) =>
        ReadConstant(OpenTypeMathConstant.OverbarExtraAscender, fonts);
}
