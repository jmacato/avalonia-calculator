using System.Collections.Generic;
using System.Linq;
using CSharpMath.Display;
using CSharpMath.Display.FrontEnd;
using HarfBuzzSharp;

namespace CSharpMath.Rendering.BackEnd;

public sealed class MathTable : FontMathTable<Fonts, Glyph>
{
    private MathTable()
    {
    }

    public static MathTable Instance { get; } = new();

    private static float ReadConstant(OpenTypeMathConstant constant, Fonts fonts) =>
        fonts.MathTypeface.GetConstant(constant) * fonts.ScaleFor(fonts.MathTypeface);

    protected override short ScriptPercentScaleDown(Fonts fonts) =>
        checked((short)fonts.MathTypeface.GetConstant(OpenTypeMathConstant.ScriptPercentScaleDown));

    protected override short ScriptScriptPercentScaleDown(Fonts fonts) =>
        checked((short)fonts.MathTypeface.GetConstant(OpenTypeMathConstant.ScriptScriptPercentScaleDown));

    public override float AxisHeight(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.AxisHeight, fonts);

    public override float FractionDenomDisplayStyleGapMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionDenomDisplayStyleGapMin, fonts);

    public override float FractionDenominatorDisplayStyleShiftDown(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionDenominatorDisplayStyleShiftDown, fonts);

    public override float FractionDenominatorGapMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionDenominatorGapMin, fonts);

    public override float FractionDenominatorShiftDown(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionDenominatorShiftDown, fonts);

    public override float FractionNumDisplayStyleGapMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionNumDisplayStyleGapMin, fonts);

    public override float FractionNumeratorDisplayStyleShiftUp(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionNumeratorDisplayStyleShiftUp, fonts);

    public override float FractionNumeratorGapMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionNumeratorGapMin, fonts);

    public override float FractionNumeratorShiftUp(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.FractionNumeratorShiftUp, fonts);

    public override float FractionRuleThickness(Fonts fonts) =>
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

    public override float GetItalicCorrection(Fonts fonts, Glyph glyph) =>
        glyph.Typeface.GetItalicCorrection(glyph.GlyphId) * fonts.ScaleFor(glyph.Typeface);

    public override Glyph GetLargerGlyph(Fonts fonts, Glyph glyph)
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

    public override IEnumerable<GlyphPart<Glyph>>? GetVerticalGlyphAssembly(Glyph rawGlyph, Fonts fonts)
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

    public override float LowerLimitBaselineDropMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.LowerLimitBaselineDropMin, fonts);

    public override float LowerLimitGapMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.LowerLimitGapMin, fonts);

    public override float MinConnectorOverlap(Fonts fonts) =>
        fonts.MathTypeface.GetMinConnectorOverlap(Direction.TopToBottom) *
        fonts.ScaleFor(fonts.MathTypeface);

    protected override short RadicalDegreeBottomRaisePercent(Fonts fonts) =>
        checked((short)fonts.MathTypeface.GetConstant(OpenTypeMathConstant.RadicalDegreeBottomRaisePercent));

    public override float RadicalDisplayStyleVerticalGap(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalDisplayStyleVerticalGap, fonts);

    public override float RadicalExtraAscender(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalExtraAscender, fonts);

    public override float RadicalKernAfterDegree(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalKernAfterDegree, fonts);

    public override float RadicalKernBeforeDegree(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalKernBeforeDegree, fonts);

    public override float RadicalRuleThickness(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalRuleThickness, fonts);

    public override float RadicalVerticalGap(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.RadicalVerticalGap, fonts);

    public override float SpaceAfterScript(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SpaceAfterScript, fonts);

    public override float StackBottomDisplayStyleShiftDown(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.StackBottomDisplayStyleShiftDown, fonts);

    public override float StackBottomShiftDown(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.StackBottomShiftDown, fonts);

    public override float StackDisplayStyleGapMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.StackDisplayStyleGapMin, fonts);

    public override float StackGapMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.StackGapMin, fonts);

    public override float StackTopDisplayStyleShiftUp(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.StackTopDisplayStyleShiftUp, fonts);

    public override float StackTopShiftUp(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.StackTopShiftUp, fonts);

    public override float SubscriptBaselineDropMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SubscriptBaselineDropMin, fonts);

    public override float SubscriptShiftDown(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SubscriptShiftDown, fonts);

    public override float SubscriptTopMax(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SubscriptTopMax, fonts);

    public override float SubSuperscriptGapMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SubSuperscriptGapMin, fonts);

    public override float SuperscriptBaselineDropMax(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptBaselineDropMax, fonts);

    public override float SuperscriptBottomMaxWithSubscript(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptBottomMaxWithSubscript, fonts);

    public override float SuperscriptBottomMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptBottomMin, fonts);

    public override float SuperscriptShiftUp(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptShiftUp, fonts);

    public override float SuperscriptShiftUpCramped(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.SuperscriptShiftUpCramped, fonts);

    public override float UpperLimitBaselineRiseMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.UpperLimitBaselineRiseMin, fonts);

    public override float UpperLimitGapMin(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.UpperLimitGapMin, fonts);

    public override float UnderbarVerticalGap(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.UnderbarVerticalGap, fonts);

    public override float AccentBaseHeight(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.AccentBaseHeight, fonts);

    public override float GetTopAccentAdjustment(Fonts fonts, Glyph glyph)
    {
        int attachment = glyph.Typeface.GetTopAccentAttachment(glyph.GlyphId);
        if (attachment == 0)
        {
            attachment = checked((int)(glyph.Typeface.GetAdvance(glyph.GlyphId) / 2));
        }

        return attachment * fonts.ScaleFor(glyph.Typeface);
    }

    public override float UnderbarRuleThickness(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.UnderbarRuleThickness, fonts);

    public override float OverbarVerticalGap(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.OverbarVerticalGap, fonts);

    public override float OverbarRuleThickness(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.OverbarRuleThickness, fonts);

    public override float OverbarExtraAscender(Fonts fonts) =>
        ReadConstant(OpenTypeMathConstant.OverbarExtraAscender, fonts);
}
