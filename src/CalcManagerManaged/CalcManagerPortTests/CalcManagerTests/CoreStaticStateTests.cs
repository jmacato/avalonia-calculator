using System.Reflection;
using System.Runtime.CompilerServices;
using CalcEngine;
using UnitConversionManager;

namespace CalcEngineTests;

public class CoreStaticStateTests
{
    [Fact]
    public void CoreAssemblyContainsNoUserDeclaredNonConstantStaticFields()
    {
        Assembly coreAssembly = typeof(RatPak).Assembly;
        string[] mutableStaticFields = coreAssembly
            .GetTypes()
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .SelectMany(type => type.GetFields(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly))
            .Where(field => !field.IsLiteral)
            .Select(field =>
                $"{field.DeclaringType!.FullName}.{field.Name} ({field.FieldType.FullName})")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            mutableStaticFields.Length == 0,
            "Core process-wide state must be constant or compiler-generated only:\n" +
            string.Join("\n", mutableStaticFields));
    }

    [Fact]
    public void EmptyUnitSentinelsDoNotShareMutableState()
    {
        Unit first = Unit.EmptyUnit;
        first.Name = "modified";
        first.IsConversionSource = false;

        Unit second = Unit.EmptyUnit;

        Assert.NotSame(first, second);
        Assert.Equal(string.Empty, second.Name);
        Assert.True(second.IsConversionSource);
    }

    [Fact]
    public void RatPakInstancesDoNotShareMutableConstants()
    {
        RatPak first = new(32);
        RatPak second = new(32);

        first.Pi.Pp.Sign = -1;

        Assert.NotSame(first.Pi, second.Pi);
        Assert.NotSame(first.Pi.Pp, second.Pi.Pp);
        Assert.Equal(1, second.Pi.Pp.Sign);
    }
}
