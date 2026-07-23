using System.Collections.Immutable;
using static MathComposer.Avalonia.OpenType.OpenTypeMathFont;

namespace MathComposer.Avalonia.OpenType;

internal sealed class OpenTypeType2BoundsInterpreter(
    OpenTypeMathFont font,
    OpenTypeTableRecord cff,
    ImmutableArray<OpenTypeCffSlice> globalSubroutines,
    ImmutableArray<OpenTypeCffSlice> localSubroutines)
{
    private const int MaximumSubroutineDepth = 10;
    private const int MaximumOperationsPerGlyph = 100_000;
    private const int MaximumOperandStack = 96;

    private readonly double[] _transient = new double[32];
    private readonly OpenTypeBoundsBuilder _bounds = new();
    private int _stemCount;

    public OpenTypeGlyphBounds ReadBounds(OpenTypeCffSlice charString)
    {
        _bounds.Reset();
        Array.Clear(_transient);
        _stemCount = 0;
        var stack = new List<double>(MaximumOperandStack);
        int operations = 0;
        Execute(charString, stack, depth: 0, ref operations);
        return _bounds.ToGlyphBounds();
    }

    private void Execute(
        OpenTypeCffSlice slice,
        List<double> stack,
        int depth,
        ref int operations)
    {
        if (depth > MaximumSubroutineDepth)
        {
            throw Invalid("A Type 2 charstring exceeds the subroutine-depth limit.");
        }

        font.EnsureTableAbsolute(cff, slice.Offset, slice.Length);
        int offset = slice.Offset;
        int end = checked(slice.Offset + slice.Length);
        while (offset < end)
        {
            if (++operations > MaximumOperationsPerGlyph)
            {
                throw Invalid("A Type 2 charstring exceeds the operation limit.");
            }

            byte operation = font.ReadByte(offset++);
            if (operation is >= 32 or 28 or 255)
            {
                Push(stack, ReadNumber(operation, ref offset, end));
                continue;
            }

            if (IsControlOperator(operation))
            {
                if (ExecuteControlOperator(operation, stack, depth, ref offset, end, ref operations))
                {
                    return;
                }

                continue;
            }

            if (operation <= 22)
            {
                ExecuteBasicDrawingOperator(operation, stack);
            }
            else
            {
                ExecuteAdvancedDrawingOperator(operation, stack);
            }
        }
    }

    private static bool IsControlOperator(byte operation)
    {
        return operation is 1 or 3 or 10 or 11 or 12 or 14 or 18 or 19 or 20 or 23 or 29;
    }

    private bool ExecuteControlOperator(
        byte operation,
        List<double> stack,
        int depth,
        ref int offset,
        int end,
        ref int operations)
    {
        switch (operation)
        {
            case 1:
            case 3:
            case 18:
            case 23:
                ConsumeStems(stack);
                break;
            case 10:
                CallSubroutine(stack, localSubroutines, depth, ref operations);
                break;
            case 11:
                return true;
            case 12:
                if (offset >= end)
                {
                    throw Invalid("A Type 2 escape operator is truncated.");
                }

                ExecuteEscaped(font.ReadByte(offset++), stack);
                break;
            case 14:
                if (stack.Count is not (0 or 1 or 4 or 5))
                {
                    throw Invalid("A Type 2 endchar has an invalid operand count.");
                }

                stack.Clear();
                return true;
            case 19:
            case 20:
                ConsumeStems(stack);
                int maskBytes = checked((_stemCount + 7) / 8);
                if (offset > end - maskBytes)
                {
                    throw Invalid("A Type 2 hint mask is truncated.");
                }

                offset += maskBytes;
                break;
            case 29:
                CallSubroutine(stack, globalSubroutines, depth, ref operations);
                break;
            default:
                throw Invalid($"Unsupported Type 2 control operator {operation}.");
        }

        return false;
    }

    private void ExecuteBasicDrawingOperator(byte operation, List<double> stack)
    {
        switch (operation)
        {
            case 4:
                PrepareMove(stack, 1);
                _bounds.Move(0, stack[0]);
                stack.Clear();
                break;
            case 5:
                RequireMultiple(stack, 2, 2);
                for (int index = 0; index < stack.Count; index += 2)
                {
                    _bounds.Line(stack[index], stack[index + 1]);
                }

                stack.Clear();
                break;
            case 6:
                DrawAlternatingLines(stack, horizontalFirst: true);
                break;
            case 7:
                DrawAlternatingLines(stack, horizontalFirst: false);
                break;
            case 8:
                RequireMultiple(stack, 6, 6);
                for (int index = 0; index < stack.Count; index += 6)
                {
                    Curve(stack, index);
                }

                stack.Clear();
                break;
            case 21:
                PrepareMove(stack, 2);
                _bounds.Move(stack[0], stack[1]);
                stack.Clear();
                break;
            case 22:
                PrepareMove(stack, 1);
                _bounds.Move(stack[0], 0);
                stack.Clear();
                break;
            default:
                throw Invalid($"Unsupported Type 2 charstring operator {operation}.");
        }
    }

    private void ExecuteAdvancedDrawingOperator(byte operation, List<double> stack)
    {
        switch (operation)
        {
            case 24:
                if (stack.Count < 8 || (stack.Count - 2) % 6 != 0)
                {
                    throw Invalid("A Type 2 rcurveline has an invalid operand count.");
                }

                int curveEnd = stack.Count - 2;
                for (int index = 0; index < curveEnd; index += 6)
                {
                    Curve(stack, index);
                }

                _bounds.Line(stack[^2], stack[^1]);
                stack.Clear();
                break;
            case 25:
                if (stack.Count < 8 || (stack.Count - 6) % 2 != 0)
                {
                    throw Invalid("A Type 2 rlinecurve has an invalid operand count.");
                }

                int lineEnd = stack.Count - 6;
                for (int index = 0; index < lineEnd; index += 2)
                {
                    _bounds.Line(stack[index], stack[index + 1]);
                }

                Curve(stack, lineEnd);
                stack.Clear();
                break;
            case 26:
                DrawVvCurves(stack);
                break;
            case 27:
                DrawHhCurves(stack);
                break;
            case 30:
                DrawAlternatingCurves(stack, horizontalFirst: false);
                break;
            case 31:
                DrawAlternatingCurves(stack, horizontalFirst: true);
                break;
            default:
                throw Invalid($"Unsupported Type 2 charstring operator {operation}.");
        }
    }

    private double ReadNumber(byte first, ref int offset, int end)
    {
        if (first is >= 32 and <= 246)
        {
            return first - 139;
        }

        if (first is >= 247 and <= 250)
        {
            EnsureCffNumberBytes(offset, 1, end);
            return (first - 247) * 256 + font.ReadByte(offset++) + 108;
        }

        if (first is >= 251 and <= 254)
        {
            EnsureCffNumberBytes(offset, 1, end);
            return -(first - 251) * 256 - font.ReadByte(offset++) - 108;
        }

        if (first == 28)
        {
            EnsureCffNumberBytes(offset, 2, end);
            short result = font.ReadInt16(offset);
            offset += 2;
            return result;
        }

        if (first == 255)
        {
            EnsureCffNumberBytes(offset, 4, end);
            int fixedValue = font.ReadInt32(offset);
            offset += 4;
            return fixedValue / 65536d;
        }

        throw Invalid("A Type 2 number encoding is invalid.");
    }

    private void CallSubroutine(
        List<double> stack,
        ImmutableArray<OpenTypeCffSlice> subroutines,
        int depth,
        ref int operations)
    {
        int operand = ToStackInteger(Pop(stack));
        int bias = subroutines.Length < 1240
            ? 107
            : subroutines.Length < 33900 ? 1131 : 32768;
        int index = checked(operand + bias);
        if ((uint)index >= (uint)subroutines.Length)
        {
            throw Invalid("A Type 2 subroutine index is out of range.");
        }

        Execute(subroutines[index], stack, depth + 1, ref operations);
    }

    private void ConsumeStems(List<double> stack)
    {
        if ((stack.Count & 1) != 0)
        {
            stack.RemoveAt(0);
        }

        if ((stack.Count & 1) != 0)
        {
            throw Invalid("A Type 2 stem operator has an invalid operand count.");
        }

        _stemCount = checked(_stemCount + stack.Count / 2);
        if (_stemCount > 4096)
        {
            throw Invalid("A Type 2 charstring declares too many stems.");
        }

        stack.Clear();
    }

    private static void PrepareMove(List<double> stack, int required)
    {
        if (stack.Count == required + 1)
        {
            stack.RemoveAt(0);
        }

        if (stack.Count != required)
        {
            throw Invalid("A Type 2 move operator has an invalid operand count.");
        }
    }

    private void DrawAlternatingLines(List<double> stack, bool horizontalFirst)
    {
        if (stack.Count == 0)
        {
            throw Invalid("A Type 2 line operator has no operands.");
        }

        bool horizontal = horizontalFirst;
        foreach (double operand in stack)
        {
            _bounds.Line(horizontal ? operand : 0, horizontal ? 0 : operand);
            horizontal = !horizontal;
        }

        stack.Clear();
    }

    private void DrawVvCurves(List<double> stack)
    {
        int index = 0;
        double firstDx = 0;
        if ((stack.Count & 1) != 0)
        {
            firstDx = stack[index++];
        }

        if (stack.Count - index == 0 || (stack.Count - index) % 4 != 0)
        {
            throw Invalid("A Type 2 vvcurveto has an invalid operand count.");
        }

        bool first = true;
        while (index < stack.Count)
        {
            _bounds.Curve(
                first ? firstDx : 0,
                stack[index++],
                stack[index++],
                stack[index++],
                0,
                stack[index++]);
            first = false;
        }

        stack.Clear();
    }

    private void DrawHhCurves(List<double> stack)
    {
        int index = 0;
        double firstDy = 0;
        if ((stack.Count & 1) != 0)
        {
            firstDy = stack[index++];
        }

        if (stack.Count - index == 0 || (stack.Count - index) % 4 != 0)
        {
            throw Invalid("A Type 2 hhcurveto has an invalid operand count.");
        }

        bool first = true;
        while (index < stack.Count)
        {
            _bounds.Curve(
                stack[index++],
                first ? firstDy : 0,
                stack[index++],
                stack[index++],
                stack[index++],
                0);
            first = false;
        }

        stack.Clear();
    }

    private void DrawAlternatingCurves(List<double> stack, bool horizontalFirst)
    {
        if (stack.Count < 4 || stack.Count % 4 is not (0 or 1))
        {
            throw Invalid("A Type 2 alternating curve operator has an invalid operand count.");
        }

        int index = 0;
        bool horizontal = horizontalFirst;
        while (stack.Count - index >= 4)
        {
            bool lastHasExtra = stack.Count - index == 5;
            if (horizontal)
            {
                double dx1 = stack[index++];
                double dx2 = stack[index++];
                double dy2 = stack[index++];
                double dy3 = stack[index++];
                double dx3 = lastHasExtra ? stack[index++] : 0;
                _bounds.Curve(dx1, 0, dx2, dy2, dx3, dy3);
            }
            else
            {
                double dy1 = stack[index++];
                double dx2 = stack[index++];
                double dy2 = stack[index++];
                double dx3 = stack[index++];
                double dy3 = lastHasExtra ? stack[index++] : 0;
                _bounds.Curve(0, dy1, dx2, dy2, dx3, dy3);
            }

            horizontal = !horizontal;
        }

        if (index != stack.Count)
        {
            throw Invalid("A Type 2 alternating curve leaves unused operands.");
        }

        stack.Clear();
    }

    private void ExecuteEscaped(byte operation, List<double> stack)
    {
        if (operation <= 24)
        {
            ExecuteBasicEscaped(operation, stack);
        }
        else
        {
            ExecuteAdvancedEscaped(operation, stack);
        }
    }

    private void ExecuteBasicEscaped(byte operation, List<double> stack)
    {
        switch (operation)
        {
            case 3:
                Binary(stack, static (left, right) => left != 0 && right != 0 ? 1 : 0);
                break;
            case 4:
                Binary(stack, static (left, right) => left != 0 || right != 0 ? 1 : 0);
                break;
            case 5:
                Push(stack, Pop(stack) == 0 ? 1 : 0);
                break;
            case 9:
                Push(stack, Math.Abs(Pop(stack)));
                break;
            case 10:
                Binary(stack, static (left, right) => left + right);
                break;
            case 11:
                Binary(stack, static (left, right) => left - right);
                break;
            case 12:
                Binary(stack, static (left, right) => right == 0
                    ? throw Invalid("A Type 2 division uses zero.")
                    : left / right);
                break;
            case 14:
                Push(stack, -Pop(stack));
                break;
            case 15:
                Binary(stack, static (left, right) => left == right ? 1 : 0);
                break;
            case 18:
                _ = Pop(stack);
                break;
            case 20:
                {
                    int index = ToTransientIndex(Pop(stack));
                    _transient[index] = Pop(stack);
                    break;
                }
            case 21:
                Push(stack, _transient[ToTransientIndex(Pop(stack))]);
                break;
            case 22:
                {
                    double secondThreshold = Pop(stack);
                    double firstThreshold = Pop(stack);
                    double secondValue = Pop(stack);
                    double firstValue = Pop(stack);
                    Push(stack, firstThreshold <= secondThreshold ? firstValue : secondValue);
                    break;
                }
            case 23:
                Push(stack, 0.5);
                break;
            case 24:
                Binary(stack, static (left, right) => left * right);
                break;
            default:
                throw Invalid($"Unsupported Type 2 escaped operator {operation}.");
        }
    }

    private void ExecuteAdvancedEscaped(byte operation, List<double> stack)
    {
        switch (operation)
        {
            case 26:
                {
                    double value = Pop(stack);
                    if (value < 0)
                    {
                        throw Invalid("A Type 2 square root uses a negative value.");
                    }

                    Push(stack, Math.Sqrt(value));
                    break;
                }
            case 27:
                {
                    double value = Pop(stack);
                    Push(stack, value);
                    Push(stack, value);
                    break;
                }
            case 28:
                {
                    double right = Pop(stack);
                    double left = Pop(stack);
                    Push(stack, right);
                    Push(stack, left);
                    break;
                }
            case 29:
                {
                    int index = Math.Max(0, ToStackInteger(Pop(stack)));
                    if (stack.Count == 0)
                    {
                        throw Invalid("A Type 2 index operator has no source operand.");
                    }

                    index = Math.Min(index, stack.Count - 1);
                    Push(stack, stack[stack.Count - 1 - index]);
                    break;
                }
            case 30:
                Roll(stack);
                break;
            case 34:
                DrawHFlex(stack);
                break;
            case 35:
                DrawFlex(stack);
                break;
            case 36:
                DrawHFlex1(stack);
                break;
            case 37:
                DrawFlex1(stack);
                break;
            default:
                throw Invalid($"Unsupported Type 2 escaped operator {operation}.");
        }
    }

    private void DrawHFlex(List<double> stack)
    {
        RequireCount(stack, 7);
        _bounds.Curve(stack[0], 0, stack[1], stack[2], stack[3], 0);
        _bounds.Curve(stack[4], 0, stack[5], -stack[2], stack[6], 0);
        stack.Clear();
    }

    private void DrawFlex(List<double> stack)
    {
        RequireCount(stack, 13);
        Curve(stack, 0);
        Curve(stack, 6);
        stack.Clear();
    }

    private void DrawHFlex1(List<double> stack)
    {
        RequireCount(stack, 9);
        _bounds.Curve(stack[0], stack[1], stack[2], stack[3], stack[4], 0);
        _bounds.Curve(stack[5], 0, stack[6], stack[7], stack[8], -(stack[1] + stack[3] + stack[7]));
        stack.Clear();
    }

    private void DrawFlex1(List<double> stack)
    {
        RequireCount(stack, 11);
        double dx = stack[0] + stack[2] + stack[4] + stack[6] + stack[8];
        double dy = stack[1] + stack[3] + stack[5] + stack[7] + stack[9];
        double lastDx = Math.Abs(dx) > Math.Abs(dy) ? stack[10] : -dx;
        double lastDy = Math.Abs(dx) > Math.Abs(dy) ? -dy : stack[10];
        _bounds.Curve(stack[0], stack[1], stack[2], stack[3], stack[4], stack[5]);
        _bounds.Curve(stack[6], stack[7], stack[8], stack[9], lastDx, lastDy);
        stack.Clear();
    }

    private static void Roll(List<double> stack)
    {
        int shift = ToStackInteger(Pop(stack));
        int count = ToStackInteger(Pop(stack));
        if (count < 0 || count > stack.Count)
        {
            throw Invalid("A Type 2 roll count is out of range.");
        }

        if (count <= 1)
        {
            return;
        }

        shift %= count;
        if (shift < 0)
        {
            shift += count;
        }

        if (shift == 0)
        {
            return;
        }

        int start = stack.Count - count;
        double[] values = stack.GetRange(start, count).ToArray();
        for (int index = 0; index < count; index++)
        {
            stack[start + (index + shift) % count] = values[index];
        }
    }

    private void Curve(List<double> stack, int index)
    {
        _bounds.Curve(
            stack[index],
            stack[index + 1],
            stack[index + 2],
            stack[index + 3],
            stack[index + 4],
            stack[index + 5]);
    }

    private static void Binary(
        List<double> stack,
        Func<double, double, double> operation)
    {
        double right = Pop(stack);
        double left = Pop(stack);
        Push(stack, operation(left, right));
    }

    private static double Pop(List<double> stack)
    {
        if (stack.Count == 0)
        {
            throw Invalid("A Type 2 operand stack underflowed.");
        }

        int index = stack.Count - 1;
        double result = stack[index];
        stack.RemoveAt(index);
        return result;
    }

    private static void Push(List<double> stack, double value)
    {
        if (!double.IsFinite(value) || stack.Count >= MaximumOperandStack)
        {
            throw Invalid("A Type 2 operand is invalid or its stack overflowed.");
        }

        stack.Add(value);
    }

    private static int ToStackInteger(double value)
    {
        if (value != Math.Truncate(value) || value < int.MinValue || value > int.MaxValue)
        {
            throw Invalid("A Type 2 integer operand is invalid.");
        }

        return (int)value;
    }

    private static int ToTransientIndex(double value)
    {
        int index = ToStackInteger(value);
        if ((uint)index >= 32)
        {
            throw Invalid("A Type 2 transient-array index is out of range.");
        }

        return index;
    }

    private static void RequireCount(List<double> stack, int count)
    {
        if (stack.Count != count)
        {
            throw Invalid("A Type 2 operator has an invalid operand count.");
        }
    }

    private static void RequireMultiple(List<double> stack, int divisor, int minimum)
    {
        if (stack.Count < minimum || stack.Count % divisor != 0)
        {
            throw Invalid("A Type 2 operator has an invalid operand count.");
        }
    }
}
