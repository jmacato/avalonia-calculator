using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using CalcEngine;
using UnitConversionManager;

namespace CalcEngineTests;

public class CoreStaticStateTests
{
    [Fact]
    public void CoreAssemblyContainsNoUserDeclaredNonConstantStaticFields()
    {
        string[] mutableStaticFields = ReadMutableStaticFields();

        Assert.True(
            mutableStaticFields.Length == 0,
            "Core process-wide state must be constant or compiler-generated only:\n" +
            string.Join("\n", mutableStaticFields));
    }

    private static string[] ReadMutableStaticFields()
    {
        using FileStream stream = File.OpenRead(typeof(RatPak).Assembly.Location);
        using var portableExecutable = new PEReader(stream);
        MetadataReader metadata = portableExecutable.GetMetadataReader();
        var mutableFields = new List<string>();
        foreach (TypeDefinitionHandle typeHandle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(typeHandle);
            string typeName = metadata.GetString(type.Name);
            if (typeName.StartsWith('<'))
            {
                continue;
            }

            string typeNamespace = metadata.GetString(type.Namespace);
            foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
            {
                FieldDefinition field = metadata.GetFieldDefinition(fieldHandle);
                FieldAttributes attributes = field.Attributes;
                if ((attributes & FieldAttributes.Static) == 0 ||
                    (attributes & FieldAttributes.Literal) != 0)
                {
                    continue;
                }

                mutableFields.Add($"{typeNamespace}.{typeName}.{metadata.GetString(field.Name)}");
            }
        }

        mutableFields.Sort(StringComparer.Ordinal);
        return mutableFields.ToArray();
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
