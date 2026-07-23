using System.Collections.Immutable;

namespace Graphing;
/// <summary>
/// A compact occupancy grid for Calculator's inequality cross hatch. One bit
/// replaces one retained marker command while preserving the native lattice.
/// </summary>
public sealed record HatchGridCommand(ImmutableArray<ulong> Occupancy, int LatticeIntervals, double Width, double Height, float Radius, GraphPaint Paint) : GraphFrameCommand(GraphCommandKind.HatchGrid)
{
    public bool IsOccupied(int column, int row)
    {
        int rowsPerColumn = LatticeIntervals - 1;
        if ((uint)column >= (uint)LatticeIntervals || row <= 0 || row >= LatticeIntervals)
        {
            return false;
        }

        int bitIndex = column * rowsPerColumn + row - 1;
        int wordIndex = bitIndex >> 6;
        return (uint)wordIndex < (uint)Occupancy.Length && (Occupancy[wordIndex] & (1UL << (bitIndex & 63))) != 0;
    }
}
