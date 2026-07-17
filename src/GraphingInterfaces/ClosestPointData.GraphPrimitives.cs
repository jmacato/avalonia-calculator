namespace Graphing;

public readonly record struct ClosestPointData(int FormulaId, float ScreenX, float ScreenY, double X, double Y, double Rho, double Theta, double T)
{
    public static ClosestPointData Unavailable => new(-1, float.NaN, float.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
}
