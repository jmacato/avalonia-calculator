namespace Graphing.Symbolics;

internal readonly record struct SingleHarmonicRangeCertificateReplayReplayComplex(BigRational Real, BigRational Imaginary)
{
    public static SingleHarmonicRangeCertificateReplayReplayComplex Zero { get; } = new(BigRational.Zero, BigRational.Zero);
    public static SingleHarmonicRangeCertificateReplayReplayComplex One { get; } = new(BigRational.One, BigRational.Zero);
    public bool IsZero => Real.IsZero && Imaginary.IsZero;

    public static SingleHarmonicRangeCertificateReplayReplayComplex operator +(SingleHarmonicRangeCertificateReplayReplayComplex left, SingleHarmonicRangeCertificateReplayReplayComplex right)
    {
        return new SingleHarmonicRangeCertificateReplayReplayComplex(left.Real + right.Real, left.Imaginary + right.Imaginary);
    }

    public static SingleHarmonicRangeCertificateReplayReplayComplex operator -(SingleHarmonicRangeCertificateReplayReplayComplex value)
    {
        return new SingleHarmonicRangeCertificateReplayReplayComplex(-value.Real, -value.Imaginary);
    }

    public static SingleHarmonicRangeCertificateReplayReplayComplex operator *(SingleHarmonicRangeCertificateReplayReplayComplex left, SingleHarmonicRangeCertificateReplayReplayComplex right)
    {
        return new SingleHarmonicRangeCertificateReplayReplayComplex(left.Real * right.Real - left.Imaginary * right.Imaginary,
            left.Real * right.Imaginary + left.Imaginary * right.Real);
    }
}
