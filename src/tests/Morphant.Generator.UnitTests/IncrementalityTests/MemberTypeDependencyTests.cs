using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class MemberTypeDependencyTests
{
    private static readonly string[] Generated =
    [
        "Morphant.Generated.Construction.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
        "Morphant.Generated.MappingExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs",
        "Morphant.Generated.Member.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
        "Morphant.Generated.MemberExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs",
        "Morphant.Generated.TypeMapper.TestMapper__c4f37cd504cad969975e05a81c79a459.g.cs"
    ];

    [TestCase("property")]
    [TestCase("field")]
    [TestCase("inherited")]
    [TestCase("constructor")]
    [TestCase("flattened")]
    public void Actualizes_member_conversions_and_restores_previous_behavior(string shape)
    {
        var dto = ConversionDtos;
        if (shape == "field") dto = dto.Replace("Number Value { get; set; }", "Number Value;");
        if (shape == "inherited") dto = dto.Replace("public class Source", "public class Source : SourceBase { }\npublic class SourceBase");
        if (shape == "constructor") dto = dto.Replace("public int Value { get; set; }", "public Destination(int value = 0) { Value = value; }\npublic int Value { get; set; }");
        if (shape == "flattened") dto = dto.Replace("public Number Value { get; set; }", "public SourcePart Part { get; set; } = new();\n}\npublic class SourcePart { public Number Value { get; set; }").Replace("public int Value", "public int PartValue");
        var mapper = shape == "flattened" ? Mapper.Replace("();", "().Flattening(Flattening.Auto);") : Mapper;

        GeneratorIncrementalityStep Edit(string name, bool implicitConversion) => ExecutableStep(
            name,
            [SourceFile("Mapper.cs", mapper), SourceFile("Dtos.cs", dto),
                SourceFile("Number.cs", Number.Replace("__KIND__", implicitConversion ? "implicit" : "explicit")),
                SourceFile("Scenario.cs", Scenario.Replace("__EXPECTED__", implicitConversion ? "1" : "0")
                    .Replace("__MEMBER__", shape == "flattened" ? "PartValue" : "Value"))],
            Generated,
            "TestCase.Scenario");

        RunAndAssert(LanguageVersion.CSharp9, static () => new MorphantGenerator(),
            Edit("explicit conversion", false),
            Edit("implicit conversion added", true),
            Edit("explicit conversion restored", false));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Actualizes_member_conversion_after_replacing_a_reference(bool projectReference)
    {
        MetadataReference Reference(string kind) => projectReference
            ? CreateCompilationReference("Numbers", Number.Replace("__KIND__", kind))
            : CreateReference("Numbers", Number.Replace("__KIND__", kind));
        var explicitReference = Reference("explicit");
        var implicitReference = Reference("implicit");
        var files = new[] { SourceFile("Mapper.cs", Mapper), SourceFile("Dtos.cs", ConversionDtos) };

        RunAndAssert(LanguageVersion.CSharp9, static () => new MorphantGenerator(),
            StepWithReferences("explicit", files, [explicitReference], Generated),
            StepWithReferences("implicit", files, [implicitReference], Generated),
            StepWithReferences("restored", files, [explicitReference], Generated));
    }

    [Test]
    public void Actualizes_reference_conversion_when_a_member_base_type_gains_an_interface()
    {
        // lang=c#
        const string dtos = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public interface IValue { }
    public class Item : ItemBase { }
    public class Source { public Item Value { get; set; } = new(); }
    public class Destination { public IValue? Value { get; set; } }
}
""";
        // lang=c#
        const string baseType = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public class ItemBase __INTERFACE__ { }
}
""";
        // lang=c#
        const string scenario = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (Morphant.ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source();
            if ((mapper.Create(source, default).Value != null) != __ASSIGNED__ ||
                (mapper.Update(source, new Destination(), default).Value != null) != __ASSIGNED__)
                throw new System.InvalidOperationException("Stale reference conversion.");
        }
    }
}
""";
        GeneratorIncrementalityStep Edit(string name, bool implements) => ExecutableStep(name,
            [SourceFile("Mapper.cs", Mapper), SourceFile("Dtos.cs", dtos),
                SourceFile("Base.cs", baseType.Replace("__INTERFACE__", implements ? ": IValue" : "")),
                SourceFile("Scenario.cs", scenario.Replace("__ASSIGNED__", implements ? "true" : "false"))],
            Generated, "TestCase.Scenario");

        RunAndAssert(LanguageVersion.CSharp9, static () => new MorphantGenerator(),
            Edit("no conversion", false), Edit("base implements interface", true), Edit("interface removed", false));
    }

    [TestCase("property", false)]
    [TestCase("property", true)]
    [TestCase("constructor", false)]
    [TestCase("constructor", true)]
    [TestCase("array", false)]
    [TestCase("generic", false)]
    [TestCase("nested", false)]
    [TestCase("nested", true)]
    [TestCase("nested constructor", false)]
    [TestCase("nested constructor", true)]
    public void Actualizes_obsolete_member_types_at_both_cache_boundaries(string shape, bool refreshMapper)
    {
        var member = shape switch
        {
            "constructor" => "public Destination(Item? value = null) { }",
            "array" => "public Item[]? Value { get; set; }",
            "generic" => "public System.Collections.Generic.List<Item>? Value { get; set; }",
            "nested" => "public Outer<Item>.Inner? Value { get; set; }",
            "nested constructor" => "public Destination(Outer<Item>.Inner? value = null) { }",
            _ => "public Item? Value { get; set; }"
        };
        var dto = EmptyDtos.Replace("__MEMBER__", member);
        if (shape.StartsWith("nested", StringComparison.Ordinal))
            dto = dto.Replace("public class Source", "public class Outer<T> { public class Inner { } }\n    public class Source");
        GeneratorIncrementalityStep Edit(string name, bool obsolete) => StepWithDiagnostics(
            name,
            [SourceFile("Mapper.cs", obsolete && refreshMapper ? Mapper.Replace("builder.Map", "/* refresh */ builder.Map") : Mapper),
                SourceFile("Dtos.cs", dto),
                SourceFile("Item.cs", Item.Replace("__ATTRIBUTE__", obsolete ? "[System.Obsolete(\"old\")]" : ""))],
            shape.EndsWith("constructor", StringComparison.Ordinal) ? [Generated[0], Generated[1], Generated[4]] : Generated,
            !obsolete ? [] : shape is "property" or "constructor"
                ? [CompilerDiagnostic("CS0618", DiagnosticSeverity.Warning, "Dtos.cs", dto.IndexOf("Item", StringComparison.Ordinal), 4),
                    CompilerDiagnostic("CS0618", DiagnosticSeverity.Warning, "Dtos.cs", dto.IndexOf("Item", StringComparison.Ordinal), 5)]
                : [CompilerDiagnostic("CS0618", DiagnosticSeverity.Warning, "Dtos.cs", dto.IndexOf("Item", StringComparison.Ordinal), 4)]);

        RunAndAssert(LanguageVersion.CSharp9, static () => new MorphantGenerator(),
            Edit("ordinary type", false), Edit("obsolete type", true), Edit("ordinary type restored", false));
    }

    [TestCase("source", false)]
    [TestCase("source", true)]
    [TestCase("project", false)]
    [TestCase("project", true)]
    [TestCase("metadata", false)]
    [TestCase("metadata", true)]
    public void Actualizes_conversions_through_containing_type_arguments(string referenceKind, bool multipleLevels)
    {
        // lang=c#
        const string types = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public interface IValue { }
    public class Item __BASE__ { }
}
""";
        // lang=c#
        const string dtos = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public interface IWrapper<out T> { }
    public class Outer<T> { public class Inner : IWrapper<T> { } }
    public class Source { public Outer<Item>.Inner Value { get; set; } = new(); }
    public class Destination { public IWrapper<IValue>? Value { get; set; } }
}
""";
        // lang=c#
        const string scenario = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (Morphant.ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source();
            if ((mapper.Create(source, default).Value != null) != __ASSIGNED__ ||
                (mapper.Update(source, new Destination(), default).Value != null) != __ASSIGNED__)
                throw new System.InvalidOperationException("Stale containing type argument.");
        }
    }
}
""";
        var declarations = multipleLevels
            ? dtos.Replace("public class Inner : IWrapper<T> { }", "public class Middle<U> { public class Inner<V> : IWrapper<T> { } }")
                .Replace("Outer<Item>.Inner", "Outer<Item>.Middle<string>.Inner<int>")
            : dtos;

        GeneratorIncrementalityStep Edit(string name, bool implements)
        {
            var item = types.Replace("__BASE__", implements ? ": IValue" : "");
            if (referenceKind == "source")
                return ExecutableStep(name,
                    [SourceFile("Mapper.cs", Mapper), SourceFile("Dtos.cs", declarations), SourceFile("Item.cs", item),
                        SourceFile("Scenario.cs", scenario.Replace("__ASSIGNED__", implements ? "true" : "false"))],
                    Generated, "TestCase.Scenario");

            var reference = referenceKind == "project"
                ? CreateCompilationReference("Items", item)
                : CreateReference("Items", item);
            return StepWithReferences(name, [SourceFile("Mapper.cs", Mapper), SourceFile("Dtos.cs", declarations)],
                [reference], Generated);
        }

        RunAndAssert(LanguageVersion.CSharp9, static () => new MorphantGenerator(),
            Edit("no conversion", false), Edit("interface added", true), Edit("interface removed", false));
    }

    // lang=c#
    private const string Mapper = """
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string ConversionDtos = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public class Source { public Number Value { get; set; } }
    public class Destination { public int Value { get; set; } }
}
""";

    // lang=c#
    private const string Number = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public struct Number
    {
        public static __KIND__ operator int(Number value) => 1;
    }
}
""";

    // lang=c#
    private const string Scenario = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (Morphant.ITypeMapper<Source, Destination>)new TestMapper();
            if (mapper.Create(new Source(), default).__MEMBER__ != __EXPECTED__ ||
                mapper.Update(new Source(), new Destination(), default).__MEMBER__ != __EXPECTED__)
                throw new System.InvalidOperationException("Stale member conversion.");
        }
    }
}
""";

    // lang=c#
    private const string EmptyDtos = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    public class Source { }
    public class Destination { __MEMBER__ }
}
""";

    // lang=c#
    private const string Item = """
#nullable enable
#pragma warning disable CS1591
namespace TestCase
{
    __ATTRIBUTE__
    public class Item { }
}
""";
}
