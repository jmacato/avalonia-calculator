namespace GraphingImpl;

internal readonly record struct VmInstruction(VmOpCode OpCode, double Constant = 0, int Operand = 0);
