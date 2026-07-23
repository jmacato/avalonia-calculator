namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
internal sealed class MemberNotNullAttribute(params string[] members) : Attribute
{
    public string[] Members { get; } = members;
}
