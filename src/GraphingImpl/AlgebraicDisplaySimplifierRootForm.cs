namespace GraphingImpl;

internal readonly record struct AlgebraicDisplaySimplifierRootForm(bool IsDirect, AlgebraicDisplaySimplifierQuadraticValue DirectValue, AlgebraicDisplaySimplifierQuadraticValue Square, int RootSign)
{
    public static AlgebraicDisplaySimplifierRootForm Direct(AlgebraicDisplaySimplifierQuadraticValue value)
    {
        return new AlgebraicDisplaySimplifierRootForm(true, value, default, 0);
    }

    public static AlgebraicDisplaySimplifierRootForm Extension(AlgebraicDisplaySimplifierQuadraticValue square, int sign)
    {
        return new AlgebraicDisplaySimplifierRootForm(false, default, square, sign);
    }
}
