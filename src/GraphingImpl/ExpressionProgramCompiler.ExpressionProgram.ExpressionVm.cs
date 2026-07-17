using System.Buffers;
using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal sealed class ExpressionProgramCompiler
{
    private readonly Dictionary<string, int> _symbolIndexes;
    private int _stack;
    public ExpressionProgramCompiler(ImmutableArray<string> symbols)
    {
        _symbolIndexes = new Dictionary<string, int>(symbols.Length, StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < symbols.Length; index++)
        {
            _symbolIndexes.Add(symbols[index], index);
        }
    }

    public ImmutableArray<VmInstruction>.Builder Instructions { get; } = ImmutableArray.CreateBuilder<VmInstruction>();
    public int MaximumStack { get; private set; }

    public void Emit(AstNode node)
    {
        switch (node.Kind)
        {
            case AstKind.Number:
                Push(new VmInstruction(VmOpCode.Constant, node.Number.ToDouble()));
                return;
            case AstKind.Variable:
                EmitVariable(node);
                return;
            case AstKind.Negate:
                Emit(node.Children[0]);
                Instructions.Add(new VmInstruction(VmOpCode.Negate));
                return;
            case AstKind.Add:
                EmitBinary(node, VmOpCode.Add);
                return;
            case AstKind.Subtract:
                EmitBinary(node, VmOpCode.Subtract);
                return;
            case AstKind.Multiply:
                EmitBinary(node, VmOpCode.Multiply);
                return;
            case AstKind.Divide:
                EmitBinary(node, VmOpCode.Divide);
                return;
            case AstKind.Power:
                EmitBinary(node, VmOpCode.Power);
                return;
            case AstKind.Function:
                EmitFunction(node);
                return;
            default:
                throw new InvalidOperationException($"Unsupported AST node {node.Kind}.");
        }
    }

    private void EmitVariable(AstNode node)
    {
        string name = IdentifierNormalizer.ToCanonicalLowerInvariant(node.Name);
        if (name == "pi")
        {
            Push(new VmInstruction(VmOpCode.Constant, Math.PI));
        }
        else if (name == "e")
        {
            Push(new VmInstruction(VmOpCode.Constant, Math.E));
        }
        else if (name == "infinity")
        {
            Push(new VmInstruction(VmOpCode.Constant, double.PositiveInfinity));
        }
        else if (_symbolIndexes.TryGetValue(node.Name, out int index))
        {
            Push(new VmInstruction(VmOpCode.Variable, Operand: index));
        }
        else
        {
            throw new GraphParseException(SyntaxErrorCode.InvalidVariableNameFormat, node.Span, $"Unknown variable '{node.Name}'.");
        }
    }

    private void EmitBinary(AstNode node, VmOpCode operation)
    {
        Emit(node.Children[0]);
        Emit(node.Children[1]);
        Instructions.Add(new VmInstruction(operation));
        _stack--;
    }

    private void EmitFunction(AstNode node)
    {
        string name = IdentifierNormalizer.ToCanonicalLowerInvariant(node.Name);
        VmOpCode operation = name switch
        {
            "sum" => VmOpCode.Sum,
            "subtract" => VmOpCode.Subtract,
            "product" => VmOpCode.Product,
            "divide" => VmOpCode.Divide,
            "power" or "pow" => VmOpCode.Power,
            "min" => VmOpCode.Minimum,
            "max" => VmOpCode.Maximum,
            "root" => VmOpCode.Root,
            "sin" => VmOpCode.Sin,
            "cos" => VmOpCode.Cos,
            "tan" => VmOpCode.Tan,
            "cot" => VmOpCode.Cot,
            "sec" => VmOpCode.Sec,
            "csc" => VmOpCode.Csc,
            "asin" or "arcsin" => VmOpCode.Asin,
            "acos" or "arccos" => VmOpCode.Acos,
            "atan" or "arctan" => VmOpCode.Atan,
            "abs" => VmOpCode.Abs,
            "sqrt" => VmOpCode.Sqrt,
            "ln" => VmOpCode.Ln,
            "log" or "log10" => VmOpCode.Log10,
            "exp" => VmOpCode.Exp,
            "floor" => VmOpCode.Floor,
            "ceil" or "ceiling" => VmOpCode.Ceiling,
            "sign" or "sgn" => VmOpCode.Sign,
            "sinh" => VmOpCode.Sinh,
            "cosh" => VmOpCode.Cosh,
            "tanh" => VmOpCode.Tanh,
            "factorial" => VmOpCode.Factorial,
            _ => throw new GraphParseException(SyntaxErrorCode.InvalidToken, node.Span, $"Unsupported function '{node.Name}'.")
        };
        if (operation == VmOpCode.Root && node.Children.Length == 1)
        {
            operation = VmOpCode.Sqrt;
        }

        ValidateArity(node, operation);
        foreach (AstNode argument in node.Children)
        {
            Emit(argument);
        }

        Instructions.Add(new VmInstruction(operation, Operand: node.Children.Length));
        _stack -= node.Children.Length - 1;
    }

    private static void ValidateArity(AstNode node, VmOpCode operation)
    {
        int count = node.Children.Length;
        bool valid = operation switch
        {
            VmOpCode.Sum or VmOpCode.Product or VmOpCode.Minimum or VmOpCode.Maximum => count >= 1,
            VmOpCode.Subtract or VmOpCode.Divide or VmOpCode.Power => count == 2,
            VmOpCode.Root => count is 1 or 2,
            _ => count == 1
        };
        if (!valid)
        {
            throw new GraphParseException(SyntaxErrorCode.IncorrectNumParameter, node.Span, $"Function '{node.Name}' received an incorrect number of arguments.");
        }
    }

    private void Push(VmInstruction instruction)
    {
        Instructions.Add(instruction);
        _stack++;
        MaximumStack = Math.Max(MaximumStack, _stack);
    }
}
