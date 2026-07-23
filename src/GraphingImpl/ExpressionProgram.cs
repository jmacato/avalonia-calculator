using System.Buffers;
using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal sealed class ExpressionProgram
{
    private readonly ImmutableArray<VmInstruction> _instructions;
    private ExpressionProgram(ImmutableArray<VmInstruction> instructions, ImmutableArray<string> symbols, int maximumStack)
    {
        _instructions = instructions;
        Symbols = symbols;
        MaximumStack = Math.Max(1, maximumStack);
    }

    public ImmutableArray<string> Symbols { get; }
    public int MaximumStack { get; }

    public static ExpressionProgram Compile(AstNode root, ImmutableArray<string> symbols)
    {
        var compiler = new ExpressionProgramCompiler(symbols);
        compiler.Emit(root);
        return new ExpressionProgram(compiler.Instructions.ToImmutable(), symbols, compiler.MaximumStack);
    }

    public EvaluationValue Evaluate(ReadOnlySpan<double> variables, EvalTrigUnitMode trigMode, int operationBudget = 16_384)
    {
        if (variables.Length < Symbols.Length)
        {
            throw new ArgumentException("A value is required for every compiled symbol.", nameof(variables));
        }

        EvaluationValue[]? rented = null;
        Span<EvaluationValue> stack = MaximumStack <= 128 ? stackalloc EvaluationValue[MaximumStack] : rented = ArrayPool<EvaluationValue>.Shared.Rent(MaximumStack);
        try
        {
            int top = 0;
            int operations = 0;
            foreach (VmInstruction instruction in _instructions)
            {
                if (++operations > operationBudget)
                {
                    return EvaluationValue.Invalid(EvaluationState.BudgetExceeded);
                }

                ExecuteScalar(instruction, stack, ref top, variables, trigMode);
            }

            return top == 1 ? stack[0] : EvaluationValue.Invalid(EvaluationState.Undefined);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<EvaluationValue>.Shared.Return(rented);
            }
        }
    }

    public DerivativeJet EvaluateJet(ReadOnlySpan<double> variables, int differentiationVariable, EvalTrigUnitMode trigMode, int operationBudget = 16_384)
    {
        if ((uint)differentiationVariable >= (uint)Symbols.Length || variables.Length < Symbols.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(differentiationVariable));
        }

        DerivativeJet[]? rented = null;
        Span<DerivativeJet> stack = MaximumStack <= 128 ? stackalloc DerivativeJet[MaximumStack] : rented = ArrayPool<DerivativeJet>.Shared.Rent(MaximumStack);
        try
        {
            int top = 0;
            int operations = 0;
            foreach (VmInstruction instruction in _instructions)
            {
                if (++operations > operationBudget)
                {
                    return DerivativeJet.Invalid(EvaluationState.BudgetExceeded);
                }

                ExecuteJet(instruction, stack, ref top, variables, differentiationVariable, trigMode);
            }

            return top == 1 ? stack[0] : DerivativeJet.Invalid(EvaluationState.Undefined);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<DerivativeJet>.Shared.Return(rented);
            }
        }
    }

    public ConservativeInterval EvaluateInterval(ReadOnlySpan<ConservativeInterval> variables, EvalTrigUnitMode trigMode, int operationBudget = 16_384)
    {
        if (variables.Length < Symbols.Length)
        {
            throw new ArgumentException("An interval is required for every compiled symbol.", nameof(variables));
        }

        ConservativeInterval[]? rented = null;
        Span<ConservativeInterval> stack = MaximumStack <= 128 ? stackalloc ConservativeInterval[MaximumStack] : rented = ArrayPool<ConservativeInterval>.Shared.Rent(MaximumStack);
        try
        {
            int top = 0;
            int operations = 0;
            foreach (VmInstruction instruction in _instructions)
            {
                if (++operations > operationBudget)
                {
                    return ConservativeInterval.Invalid(EvaluationState.BudgetExceeded);
                }

                ExecuteInterval(instruction, stack, ref top, variables, trigMode);
            }

            return top == 1 ? stack[0] : ConservativeInterval.Invalid(EvaluationState.Undefined);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<ConservativeInterval>.Shared.Return(rented);
            }
        }
    }

    private static void ExecuteScalar(VmInstruction instruction, Span<EvaluationValue> stack, ref int top, ReadOnlySpan<double> variables, EvalTrigUnitMode trigMode)
    {
        switch (instruction.OpCode)
        {
            case VmOpCode.Constant:
                stack[top++] = EvaluationValue.Finite(instruction.Constant);
                return;
            case VmOpCode.Variable:
                stack[top++] = EvaluationValue.Finite(variables[instruction.Operand]);
                return;
            case VmOpCode.Negate:
                stack[top - 1] = Unary(stack[top - 1], static value => -value);
                return;
            case VmOpCode.Add:
                BinaryScalar(stack, ref top, static (left, right) => EvaluationValue.Finite(left + right));
                return;
            case VmOpCode.Subtract:
                BinaryScalar(stack, ref top, static (left, right) => EvaluationValue.Finite(left - right));
                return;
            case VmOpCode.Multiply:
                BinaryScalar(stack, ref top, static (left, right) => EvaluationValue.Finite(left * right));
                return;
            case VmOpCode.Divide:
                BinaryScalar(stack, ref top, Divide);
                return;
            case VmOpCode.Power:
                BinaryScalar(stack, ref top, Power);
                return;
            case VmOpCode.Root:
                BinaryScalar(stack, ref top, Root);
                return;
            case VmOpCode.Sum:
                AggregateScalar(stack, ref top, instruction.Operand, product: false, minimum: false, maximum: false);
                return;
            case VmOpCode.Product:
                AggregateScalar(stack, ref top, instruction.Operand, product: true, minimum: false, maximum: false);
                return;
            case VmOpCode.Minimum:
                AggregateScalar(stack, ref top, instruction.Operand, product: false, minimum: true, maximum: false);
                return;
            case VmOpCode.Maximum:
                AggregateScalar(stack, ref top, instruction.Operand, product: false, minimum: false, maximum: true);
                return;
            default:
                stack[top - 1] = ApplyScalarFunction(instruction.OpCode, stack[top - 1], trigMode);
                return;
        }
    }

    private static EvaluationValue ApplyScalarFunction(VmOpCode operation, EvaluationValue input, EvalTrigUnitMode trigMode)
    {
        if (!input.IsFinite)
        {
            return input;
        }

        double radiansFactor = RadiansFactor(trigMode);
        double value = input.Value;
        return operation switch
        {
            VmOpCode.Sin => EvaluationValue.Finite(Math.Sin(value * radiansFactor)),
            VmOpCode.Cos => EvaluationValue.Finite(Math.Cos(value * radiansFactor)),
            VmOpCode.Tan => EvaluationValue.Finite(Math.Tan(value * radiansFactor)),
            VmOpCode.Cot => Divide(1, Math.Tan(value * radiansFactor)),
            VmOpCode.Sec => Divide(1, Math.Cos(value * radiansFactor)),
            VmOpCode.Csc => Divide(1, Math.Sin(value * radiansFactor)),
            VmOpCode.Asin when value is >= -1 and <= 1 => EvaluationValue.Finite(Math.Asin(value) / radiansFactor),
            VmOpCode.Acos when value is >= -1 and <= 1 => EvaluationValue.Finite(Math.Acos(value) / radiansFactor),
            VmOpCode.Atan => EvaluationValue.Finite(Math.Atan(value) / radiansFactor),
            VmOpCode.Abs => EvaluationValue.Finite(Math.Abs(value)),
            VmOpCode.Sqrt when value >= 0 => EvaluationValue.Finite(Math.Sqrt(value)),
            VmOpCode.Ln when value > 0 => EvaluationValue.Finite(Math.Log(value)),
            VmOpCode.Log10 when value > 0 => EvaluationValue.Finite(Math.Log10(value)),
            VmOpCode.Exp => EvaluationValue.Finite(Math.Exp(value)),
            VmOpCode.Floor => EvaluationValue.Finite(Math.Floor(value)),
            VmOpCode.Ceiling => EvaluationValue.Finite(Math.Ceiling(value)),
            VmOpCode.Sign => EvaluationValue.Finite(Math.Sign(value)),
            VmOpCode.Sinh => EvaluationValue.Finite(Math.Sinh(value)),
            VmOpCode.Cosh => EvaluationValue.Finite(Math.Cosh(value)),
            VmOpCode.Tanh => EvaluationValue.Finite(Math.Tanh(value)),
            VmOpCode.Factorial => Factorial(value),
            _ => EvaluationValue.Invalid(EvaluationState.NonReal)
        };
    }

    private static void BinaryScalar(Span<EvaluationValue> stack, ref int top, Func<double, double, EvaluationValue> operation)
    {
        EvaluationValue right = stack[--top];
        EvaluationValue left = stack[top - 1];
        stack[top - 1] = !left.IsFinite ? left : !right.IsFinite ? right : operation(left.Value, right.Value);
    }

    private static void AggregateScalar(Span<EvaluationValue> stack, ref int top, int count, bool product, bool minimum, bool maximum)
    {
        int start = top - count;
        double result = product ? 1 : minimum ? double.PositiveInfinity : maximum ? double.NegativeInfinity : 0;
        EvaluationState state = EvaluationState.Finite;
        for (int index = start; index < top; index++)
        {
            EvaluationValue item = stack[index];
            if (!item.IsFinite)
            {
                state = item.State;
                break;
            }

            result = product ? result * item.Value : minimum ? Math.Min(result, item.Value) : maximum ? Math.Max(result, item.Value) : result + item.Value;
        }

        top = start + 1;
        stack[start] = state == EvaluationState.Finite ? EvaluationValue.Finite(result) : EvaluationValue.Invalid(state);
    }

    private static EvaluationValue Unary(EvaluationValue input, Func<double, double> operation)
    {
        return input.IsFinite ? EvaluationValue.Finite(operation(input.Value)) : input;
    }

    private static EvaluationValue Divide(double numerator, double denominator)
    {
        return denominator == 0
            ? EvaluationValue.Invalid(EvaluationState.Undefined)
            : EvaluationValue.Finite(numerator / denominator);
    }

    private static EvaluationValue Power(double basis, double exponent)
    {
        if (basis < 0 && !IsInteger(exponent))
        {
            return EvaluationValue.Invalid(EvaluationState.NonReal);
        }

        if (basis == 0 && exponent <= 0)
        {
            return EvaluationValue.Invalid(EvaluationState.Undefined);
        }

        return EvaluationValue.Finite(Math.Pow(basis, exponent));
    }

    private static EvaluationValue Root(double radicand, double index)
    {
        if (index == 0)
        {
            return EvaluationValue.Invalid(EvaluationState.Undefined);
        }

        if (radicand < 0)
        {
            if (!IsInteger(index) || ((long)Math.Abs(index) & 1L) == 0)
            {
                return EvaluationValue.Invalid(EvaluationState.NonReal);
            }

            return EvaluationValue.Finite(-Math.Pow(-radicand, 1 / index));
        }

        return EvaluationValue.Finite(Math.Pow(radicand, 1 / index));
    }

    private static EvaluationValue Factorial(double value)
    {
        if (!IsInteger(value) || value < 0 || value > 170)
        {
            return EvaluationValue.Invalid(EvaluationState.Undefined);
        }

        double result = 1;
        for (int index = 2; index <= (int)value; index++)
        {
            result *= index;
        }

        return EvaluationValue.Finite(result);
    }

    private static void ExecuteJet(VmInstruction instruction, Span<DerivativeJet> stack, ref int top, ReadOnlySpan<double> variables, int differentiationVariable, EvalTrigUnitMode trigMode)
    {
        switch (instruction.OpCode)
        {
            case VmOpCode.Constant:
                stack[top++] = DerivativeJet.Constant(instruction.Constant);
                return;
            case VmOpCode.Variable:
                stack[top++] = new DerivativeJet(variables[instruction.Operand], instruction.Operand == differentiationVariable ? 1 : 0, 0, double.IsFinite(variables[instruction.Operand]) ? EvaluationState.Finite : EvaluationState.Undefined);
                return;
            case VmOpCode.Negate:
                stack[top - 1] = Negate(stack[top - 1]);
                return;
            case VmOpCode.Add:
                BinaryJet(stack, ref top, Add);
                return;
            case VmOpCode.Subtract:
                BinaryJet(stack, ref top, Subtract);
                return;
            case VmOpCode.Multiply:
                BinaryJet(stack, ref top, Multiply);
                return;
            case VmOpCode.Divide:
                BinaryJet(stack, ref top, DivideJet);
                return;
            case VmOpCode.Power:
                BinaryJet(stack, ref top, PowerJet);
                return;
            case VmOpCode.Root:
                BinaryJet(stack, ref top, RootJet);
                return;
            case VmOpCode.Sum:
            case VmOpCode.Product:
            case VmOpCode.Minimum:
            case VmOpCode.Maximum:
                AggregateJet(stack, ref top, instruction.OpCode, instruction.Operand);
                return;
            default:
                stack[top - 1] = ApplyJetFunction(instruction.OpCode, stack[top - 1], trigMode);
                return;
        }
    }

    private static DerivativeJet ApplyJetFunction(VmOpCode operation, DerivativeJet input, EvalTrigUnitMode trigMode)
    {
        if (!input.IsFinite)
        {
            return input;
        }

        if (operation is VmOpCode.Floor or VmOpCode.Ceiling or VmOpCode.Sign or VmOpCode.Factorial)
        {
            return ApplyNonSmoothJet(operation, input);
        }

        double x = input.Value;
        double radians = RadiansFactor(trigMode);
        switch (operation)
        {
            case VmOpCode.Sin:
                return Compose(input, Math.Sin(x * radians), radians * Math.Cos(x * radians), -radians * radians * Math.Sin(x * radians));
            case VmOpCode.Cos:
                return Compose(input, Math.Cos(x * radians), -radians * Math.Sin(x * radians), -radians * radians * Math.Cos(x * radians));
            case VmOpCode.Tan:
                {
                    double tangent = Math.Tan(x * radians);
                    double secantSquared = 1 + tangent * tangent;
                    return Compose(input, tangent, radians * secantSquared, 2 * radians * radians * secantSquared * tangent);
                }

            case VmOpCode.Cot:
            case VmOpCode.Sec:
            case VmOpCode.Csc:
                return NumericalUnaryJet(operation, input, trigMode);
            case VmOpCode.Asin when x is >= -1 and <= 1:
                {
                    double denominator = Math.Sqrt(1 - x * x);
                    return Compose(input, Math.Asin(x) / radians, 1 / (radians * denominator), x / (radians * denominator * denominator * denominator));
                }

            case VmOpCode.Acos when x is >= -1 and <= 1:
                {
                    double denominator = Math.Sqrt(1 - x * x);
                    return Compose(input, Math.Acos(x) / radians, -1 / (radians * denominator), -x / (radians * denominator * denominator * denominator));
                }

            case VmOpCode.Atan:
                {
                    double denominator = 1 + x * x;
                    return Compose(input, Math.Atan(x) / radians, 1 / (radians * denominator), -2 * x / (radians * denominator * denominator));
                }

            case VmOpCode.Abs when x != 0:
                return Compose(input, Math.Abs(x), Math.Sign(x), 0);
            case VmOpCode.Sqrt when x > 0:
                {
                    double root = Math.Sqrt(x);
                    return Compose(input, root, 1 / (2 * root), -1 / (4 * root * root * root));
                }

            case VmOpCode.Ln when x > 0:
                return Compose(input, Math.Log(x), 1 / x, -1 / (x * x));
            case VmOpCode.Log10 when x > 0:
                {
                    double scale = Math.Log(10);
                    return Compose(input, Math.Log10(x), 1 / (x * scale), -1 / (x * x * scale));
                }

            case VmOpCode.Exp:
                {
                    double exponential = Math.Exp(x);
                    return Compose(input, exponential, exponential, exponential);
                }

            case VmOpCode.Sinh:
                return Compose(input, Math.Sinh(x), Math.Cosh(x), Math.Sinh(x));
            case VmOpCode.Cosh:
                return Compose(input, Math.Cosh(x), Math.Sinh(x), Math.Cosh(x));
            case VmOpCode.Tanh:
                {
                    double tangent = Math.Tanh(x);
                    double derivative = 1 - tangent * tangent;
                    return Compose(input, tangent, derivative, -2 * tangent * derivative);
                }

            default:
                return DerivativeJet.Invalid(EvaluationState.NonReal);
        }
    }

    private static DerivativeJet ApplyNonSmoothJet(VmOpCode operation, DerivativeJet input)
    {
        double value = input.Value;
        if (operation == VmOpCode.Floor && value != Math.Floor(value))
        {
            return DerivativeJet.Constant(Math.Floor(value));
        }

        if (operation == VmOpCode.Ceiling && value != Math.Ceiling(value))
        {
            return DerivativeJet.Constant(Math.Ceiling(value));
        }

        if (operation == VmOpCode.Sign && value != 0)
        {
            return DerivativeJet.Constant(Math.Sign(value));
        }

        if (operation == VmOpCode.Factorial && input.First == 0 && input.Second == 0)
        {
            EvaluationValue factorial = Factorial(value);
            return factorial.IsFinite ? DerivativeJet.Constant(factorial.Value) : DerivativeJet.Invalid(factorial.State);
        }

        return DerivativeJet.Invalid(EvaluationState.NonReal);
    }

    private static DerivativeJet NumericalUnaryJet(VmOpCode operation, DerivativeJet input, EvalTrigUnitMode trigMode)
    {
        double h = Math.Max(1e-6, Math.Abs(input.Value) * 1e-6);
        EvaluationValue center = ApplyScalarFunction(operation, EvaluationValue.Finite(input.Value), trigMode);
        EvaluationValue left = ApplyScalarFunction(operation, EvaluationValue.Finite(input.Value - h), trigMode);
        EvaluationValue right = ApplyScalarFunction(operation, EvaluationValue.Finite(input.Value + h), trigMode);
        if (!center.IsFinite || !left.IsFinite || !right.IsFinite)
        {
            return DerivativeJet.Invalid(EvaluationState.Undefined);
        }

        double first = (right.Value - left.Value) / (2 * h);
        double second = (right.Value - 2 * center.Value + left.Value) / (h * h);
        return Compose(input, center.Value, first, second);
    }

    private static DerivativeJet Compose(DerivativeJet input, double value, double first, double second)
    {
        DerivativeJet result = new(value, first * input.First, second * input.First * input.First + first * input.Second, EvaluationState.Finite);
        return result.IsFinite ? result : DerivativeJet.Invalid(EvaluationState.Overflow);
    }

    private static DerivativeJet Negate(DerivativeJet value)
    {
        return value.IsFinite ? new(-value.Value, -value.First, -value.Second, EvaluationState.Finite) : value;
    }

    private static DerivativeJet Add(DerivativeJet left, DerivativeJet right)
    {
        return FinishJet(left.Value + right.Value, left.First + right.First, left.Second + right.Second);
    }

    private static DerivativeJet Subtract(DerivativeJet left, DerivativeJet right)
    {
        return FinishJet(left.Value - right.Value, left.First - right.First, left.Second - right.Second);
    }

    private static DerivativeJet Multiply(DerivativeJet left, DerivativeJet right)
    {
        return FinishJet(left.Value * right.Value, left.First * right.Value + left.Value * right.First,
            left.Second * right.Value + 2 * left.First * right.First + left.Value * right.Second);
    }

    private static DerivativeJet DivideJet(DerivativeJet left, DerivativeJet right)
    {
        if (right.Value == 0)
        {
            return DerivativeJet.Invalid(EvaluationState.Undefined);
        }

        double reciprocalValue = 1 / right.Value;
        var reciprocal = FinishJet(reciprocalValue, -right.First / (right.Value * right.Value), (2 * right.First * right.First - right.Value * right.Second) / (right.Value * right.Value * right.Value));
        return reciprocal.IsFinite ? Multiply(left, reciprocal) : reciprocal;
    }

    private static DerivativeJet PowerJet(DerivativeJet basis, DerivativeJet exponent)
    {
        if (basis.Value > 0)
        {
            DerivativeJet logarithm = Compose(basis, Math.Log(basis.Value), 1 / basis.Value, -1 / (basis.Value * basis.Value));
            DerivativeJet product = Multiply(exponent, logarithm);
            return ApplyJetFunction(VmOpCode.Exp, product, EvalTrigUnitMode.Radians);
        }

        if (exponent.First == 0 && exponent.Second == 0 && IsInteger(exponent.Value))
        {
            double n = exponent.Value;
            double value = Math.Pow(basis.Value, n);
            return Compose(basis, value, n * Math.Pow(basis.Value, n - 1), n * (n - 1) * Math.Pow(basis.Value, n - 2));
        }

        return DerivativeJet.Invalid(EvaluationState.NonReal);
    }

    private static DerivativeJet RootJet(DerivativeJet radicand, DerivativeJet index)
    {
        if (index.Value == 0 || index.First != 0 || index.Second != 0)
        {
            return DerivativeJet.Invalid(EvaluationState.Undefined);
        }

        return PowerJet(radicand, DerivativeJet.Constant(1 / index.Value));
    }

    private static void BinaryJet(Span<DerivativeJet> stack, ref int top, Func<DerivativeJet, DerivativeJet, DerivativeJet> operation)
    {
        DerivativeJet right = stack[--top];
        DerivativeJet left = stack[top - 1];
        stack[top - 1] = !left.IsFinite ? left : !right.IsFinite ? right : operation(left, right);
    }

    private static void AggregateJet(Span<DerivativeJet> stack, ref int top, VmOpCode operation, int count)
    {
        int start = top - count;
        DerivativeJet result = operation == VmOpCode.Product ? DerivativeJet.Constant(1) : stack[start];
        int index = operation == VmOpCode.Product ? start : start + 1;
        for (; index < top; index++)
        {
            DerivativeJet item = stack[index];
            if (!result.IsFinite || !item.IsFinite)
            {
                result = !result.IsFinite ? result : item;
                break;
            }

            result = operation switch
            {
                VmOpCode.Sum => Add(result, item),
                VmOpCode.Product => Multiply(result, item),
                VmOpCode.Minimum when result.Value < item.Value => result,
                VmOpCode.Minimum when result.Value > item.Value => item,
                VmOpCode.Maximum when result.Value > item.Value => result,
                VmOpCode.Maximum when result.Value < item.Value => item,
                _ => DerivativeJet.Invalid(EvaluationState.Undefined)
            };
        }

        top = start + 1;
        stack[start] = result;
    }

    private static DerivativeJet FinishJet(double value, double first, double second)
    {
        return double.IsFinite(value) && double.IsFinite(first) && double.IsFinite(second)
            ? new DerivativeJet(value, first, second, EvaluationState.Finite)
            : DerivativeJet.Invalid(EvaluationState.Overflow);
    }

    private static void ExecuteInterval(VmInstruction instruction, Span<ConservativeInterval> stack, ref int top, ReadOnlySpan<ConservativeInterval> variables, EvalTrigUnitMode trigMode)
    {
        switch (instruction.OpCode)
        {
            case VmOpCode.Constant:
                stack[top++] = ConservativeInterval.Point(instruction.Constant);
                return;
            case VmOpCode.Variable:
                stack[top++] = variables[instruction.Operand];
                return;
            case VmOpCode.Negate:
                {
                    ConservativeInterval value = stack[top - 1];
                    stack[top - 1] = value.IsFinite ? new ConservativeInterval(-value.Maximum, -value.Minimum, EvaluationState.Finite) : value;
                    return;
                }

            case VmOpCode.Add:
                BinaryInterval(stack, ref top, AddInterval);
                return;
            case VmOpCode.Subtract:
                BinaryInterval(stack, ref top, SubtractInterval);
                return;
            case VmOpCode.Multiply:
                BinaryInterval(stack, ref top, MultiplyInterval);
                return;
            case VmOpCode.Divide:
                BinaryInterval(stack, ref top, DivideInterval);
                return;
            case VmOpCode.Power:
                BinaryInterval(stack, ref top, PowerInterval);
                return;
            case VmOpCode.Root:
                BinaryInterval(stack, ref top, RootInterval);
                return;
            case VmOpCode.Sum:
            case VmOpCode.Product:
            case VmOpCode.Minimum:
            case VmOpCode.Maximum:
                AggregateInterval(stack, ref top, instruction.OpCode, instruction.Operand);
                return;
            default:
                stack[top - 1] = ApplyIntervalFunction(instruction.OpCode, stack[top - 1], trigMode);
                return;
        }
    }

    private static ConservativeInterval ApplyIntervalFunction(VmOpCode operation, ConservativeInterval input, EvalTrigUnitMode trigMode)
    {
        if (!input.IsFinite)
        {
            return input;
        }

        double radians = RadiansFactor(trigMode);
        return operation switch
        {
            VmOpCode.Sin => TrigonometricInterval(input.Minimum * radians, input.Maximum * radians, cosine: false),
            VmOpCode.Cos => TrigonometricInterval(input.Minimum * radians, input.Maximum * radians, cosine: true),
            VmOpCode.Tan when CrossesTangentPole(input.Minimum * radians, input.Maximum * radians) => ConservativeInterval.Invalid(EvaluationState.Undefined),
            VmOpCode.Tan => Ordered(Math.Tan(input.Minimum * radians), Math.Tan(input.Maximum * radians)),
            VmOpCode.Asin when input.Minimum >= -1 && input.Maximum <= 1 => Ordered(Math.Asin(input.Minimum) / radians, Math.Asin(input.Maximum) / radians),
            VmOpCode.Acos when input.Minimum >= -1 && input.Maximum <= 1 => Ordered(Math.Acos(input.Maximum) / radians, Math.Acos(input.Minimum) / radians),
            VmOpCode.Atan => Ordered(Math.Atan(input.Minimum) / radians, Math.Atan(input.Maximum) / radians),
            VmOpCode.Abs when input.Minimum >= 0 => input,
            VmOpCode.Abs when input.Maximum <= 0 => Ordered(-input.Maximum, -input.Minimum),
            VmOpCode.Abs => Ordered(0, Math.Max(-input.Minimum, input.Maximum)),
            VmOpCode.Sqrt when input.Minimum >= 0 => Ordered(Math.Sqrt(input.Minimum), Math.Sqrt(input.Maximum)),
            VmOpCode.Ln when input.Minimum > 0 => Ordered(Math.Log(input.Minimum), Math.Log(input.Maximum)),
            VmOpCode.Log10 when input.Minimum > 0 => Ordered(Math.Log10(input.Minimum), Math.Log10(input.Maximum)),
            VmOpCode.Exp => Ordered(Math.Exp(input.Minimum), Math.Exp(input.Maximum)),
            VmOpCode.Floor => Ordered(Math.Floor(input.Minimum), Math.Floor(input.Maximum)),
            VmOpCode.Ceiling => Ordered(Math.Ceiling(input.Minimum), Math.Ceiling(input.Maximum)),
            VmOpCode.Sign => Ordered(Math.Sign(input.Minimum), Math.Sign(input.Maximum)),
            VmOpCode.Sinh => Ordered(Math.Sinh(input.Minimum), Math.Sinh(input.Maximum)),
            VmOpCode.Cosh => CoshInterval(input),
            VmOpCode.Tanh => Ordered(Math.Tanh(input.Minimum), Math.Tanh(input.Maximum)),
            VmOpCode.Factorial when input.Minimum == input.Maximum => ToInterval(Factorial(input.Minimum)),
            _ => ConservativeInterval.Invalid(EvaluationState.Undefined)
        };
    }

    private static ConservativeInterval AddInterval(ConservativeInterval left, ConservativeInterval right)
    {
        return Ordered(left.Minimum + right.Minimum, left.Maximum + right.Maximum);
    }

    private static ConservativeInterval SubtractInterval(ConservativeInterval left, ConservativeInterval right)
    {
        return Ordered(left.Minimum - right.Maximum, left.Maximum - right.Minimum);
    }

    private static ConservativeInterval MultiplyInterval(ConservativeInterval left, ConservativeInterval right)
    {
        double a = left.Minimum * right.Minimum;
        double b = left.Minimum * right.Maximum;
        double c = left.Maximum * right.Minimum;
        double d = left.Maximum * right.Maximum;
        return Ordered(Math.Min(Math.Min(a, b), Math.Min(c, d)), Math.Max(Math.Max(a, b), Math.Max(c, d)));
    }

    private static ConservativeInterval DivideInterval(ConservativeInterval left, ConservativeInterval right)
    {
        if (right.Minimum <= 0 && right.Maximum >= 0)
        {
            return ConservativeInterval.Invalid(EvaluationState.Undefined);
        }

        return MultiplyInterval(left, Ordered(1 / right.Maximum, 1 / right.Minimum));
    }

    private static ConservativeInterval PowerInterval(ConservativeInterval basis, ConservativeInterval exponent)
    {
        if (exponent.Minimum != exponent.Maximum || !IsInteger(exponent.Minimum))
        {
            return basis.Minimum >= 0 && exponent.Minimum == exponent.Maximum ? Ordered(Math.Pow(basis.Minimum, exponent.Minimum), Math.Pow(basis.Maximum, exponent.Maximum)) : ConservativeInterval.Invalid(EvaluationState.NonReal);
        }

        long power = (long)exponent.Minimum;
        if (power < 0 && basis.Minimum <= 0 && basis.Maximum >= 0)
        {
            return ConservativeInterval.Invalid(EvaluationState.Undefined);
        }

        double first = Math.Pow(basis.Minimum, power);
        double second = Math.Pow(basis.Maximum, power);
        if ((power & 1L) == 0 && basis.Minimum <= 0 && basis.Maximum >= 0)
        {
            return Ordered(0, Math.Max(first, second));
        }

        return Ordered(first, second);
    }

    private static ConservativeInterval RootInterval(ConservativeInterval radicand, ConservativeInterval index)
    {
        if (index.Minimum != index.Maximum || index.Minimum == 0)
        {
            return ConservativeInterval.Invalid(EvaluationState.Undefined);
        }

        EvaluationValue left = Root(radicand.Minimum, index.Minimum);
        EvaluationValue right = Root(radicand.Maximum, index.Minimum);
        return left.IsFinite && right.IsFinite ? Ordered(left.Value, right.Value) : ConservativeInterval.Invalid(!left.IsFinite ? left.State : right.State);
    }

    private static void BinaryInterval(Span<ConservativeInterval> stack, ref int top, Func<ConservativeInterval, ConservativeInterval, ConservativeInterval> operation)
    {
        ConservativeInterval right = stack[--top];
        ConservativeInterval left = stack[top - 1];
        stack[top - 1] = !left.IsFinite ? left : !right.IsFinite ? right : operation(left, right);
    }

    private static void AggregateInterval(Span<ConservativeInterval> stack, ref int top, VmOpCode operation, int count)
    {
        int start = top - count;
        ConservativeInterval result = operation == VmOpCode.Product ? ConservativeInterval.Point(1) : stack[start];
        int index = operation == VmOpCode.Product ? start : start + 1;
        for (; index < top; index++)
        {
            ConservativeInterval item = stack[index];
            if (!result.IsFinite || !item.IsFinite)
            {
                result = !result.IsFinite ? result : item;
                break;
            }

            result = operation switch
            {
                VmOpCode.Sum => AddInterval(result, item),
                VmOpCode.Product => MultiplyInterval(result, item),
                VmOpCode.Minimum => Ordered(Math.Min(result.Minimum, item.Minimum), Math.Min(result.Maximum, item.Maximum)),
                VmOpCode.Maximum => Ordered(Math.Max(result.Minimum, item.Minimum), Math.Max(result.Maximum, item.Maximum)),
                _ => ConservativeInterval.Invalid(EvaluationState.Undefined)
            };
        }

        top = start + 1;
        stack[start] = result;
    }

    private static ConservativeInterval TrigonometricInterval(double minimum, double maximum, bool cosine)
    {
        if (maximum - minimum >= Math.Tau)
        {
            return Ordered(-1, 1);
        }

        double left = cosine ? Math.Cos(minimum) : Math.Sin(minimum);
        double right = cosine ? Math.Cos(maximum) : Math.Sin(maximum);
        double resultMinimum = Math.Min(left, right);
        double resultMaximum = Math.Max(left, right);
        double offset = cosine ? 0 : Math.PI / 2;
        long firstCritical = (long)Math.Ceiling((minimum - offset) / Math.PI);
        long lastCritical = (long)Math.Floor((maximum - offset) / Math.PI);
        for (long critical = firstCritical; critical <= lastCritical; critical++)
        {
            double value = (critical & 1L) == 0 ? 1 : -1;
            resultMinimum = Math.Min(resultMinimum, value);
            resultMaximum = Math.Max(resultMaximum, value);
        }

        return Ordered(resultMinimum, resultMaximum);
    }

    private static bool CrossesTangentPole(double minimum, double maximum)
    {
        long first = (long)Math.Ceiling((minimum - Math.PI / 2) / Math.PI);
        long last = (long)Math.Floor((maximum - Math.PI / 2) / Math.PI);
        return first <= last;
    }

    private static ConservativeInterval CoshInterval(ConservativeInterval input)
    {
        double left = Math.Cosh(input.Minimum);
        double right = Math.Cosh(input.Maximum);
        return input.Minimum <= 0 && input.Maximum >= 0 ? Ordered(1, Math.Max(left, right)) : Ordered(left, right);
    }

    private static ConservativeInterval Ordered(double first, double second)
    {
        double minimum = Math.Min(first, second);
        double maximum = Math.Max(first, second);
        return double.IsFinite(minimum) && double.IsFinite(maximum) ? new ConservativeInterval(minimum, maximum, EvaluationState.Finite) : ConservativeInterval.Invalid(double.IsInfinity(minimum) || double.IsInfinity(maximum) ? EvaluationState.Overflow : EvaluationState.Undefined);
    }

    private static ConservativeInterval ToInterval(EvaluationValue value)
    {
        return value.IsFinite ? ConservativeInterval.Point(value.Value) : ConservativeInterval.Invalid(value.State);
    }

    private static double RadiansFactor(EvalTrigUnitMode mode)
    {
        return mode switch
        {
            EvalTrigUnitMode.Degrees => Math.PI / 180,
            EvalTrigUnitMode.Grads => Math.PI / 200,
            _ => 1
        };
    }

    private static bool IsInteger(double value)
    {
        return double.IsFinite(value) && value == Math.Truncate(value) && Math.Abs(value) <= 9_007_199_254_740_992d;
    }
}
