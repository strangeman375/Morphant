using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class ExplicitMappingActualizationTests
{
    private const string Mapper =
        "Morphant.Generated.TypeMapper.TestMapper__c4f37cd504cad969975e05a81c79a459.g.cs";

    [Test]
    public void Actualizes_explicit_mapping_strategies_without_stale_code()
    {
        var generated = new[]
        {
            "Morphant.Generated.Construction.ChildDestination__b186009bc4411bdc54c2ac6a976ff28e.g.cs",
            "Morphant.Generated.MappingExtension.TestMapper.ChildSourceToChildDestination__33d35a845ae14174c96bd90de735fb5a.g.cs",
            "Morphant.Generated.Member.ChildDestination__b186009bc4411bdc54c2ac6a976ff28e.g.cs",
            "Morphant.Generated.MemberExtension.TestMapper.ChildSourceToChildDestination__33d35a845ae14174c96bd90de735fb5a.g.cs",
            "Morphant.Generated.Construction.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
            "Morphant.Generated.MappingExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs",
            "Morphant.Generated.Member.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
            "Morphant.Generated.MemberExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs",
            Mapper
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "declarative construction",
                [SourceFile("TestCase.cs", DeclarativeConstruction(1))],
                generated,
                "TestCase.Scenario"),
            ExecutableStep(
                "declarative resolution",
                [SourceFile("TestCase.cs", DeclarativeResolution(2))],
                generated,
                "TestCase.Scenario",
                ChangedMapper()),
            ExecutableStep(
                "manual conversion",
                [SourceFile("TestCase.cs", ManualConversionSource)],
                generated,
                "TestCase.Scenario",
                ChangedMapper()),
            ExecutableStep(
                "declarative construction restored",
                [SourceFile("TestCase.cs", DeclarativeConstruction(4))],
                generated,
                "TestCase.Scenario",
                ChangedMapper()));
    }

    private static ExpectedIncrementalStage ChangedMapper()
    {
        return Stage(
            "BuildTypeMapperRequests",
            Expected(Mapper, IncrementalStepRunReason.Modified));
    }

    private static string DeclarativeConstruction(int delta)
    {
        return Declarative(
            ".Construct(source => new(Create(source.Child)))",
            delta);
    }

    private static string DeclarativeResolution(int delta)
    {
        return Declarative(
            ".Resolve((source, _) => new(Create(source.Child)))",
            delta);
    }

    private static string Declarative(string configuration, int delta)
    {
        return DeclarativeSource
            .Replace("__CONFIGURATION__", configuration)
            .Replace("__DELTA__", delta.ToString())
            .Replace("__EXPECTED_VALUE__", (7 + delta).ToString());
    }

    // lang=c#
    private const string DeclarativeSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record Source(int Value, ChildSource Child);

    public sealed class Destination
    {
        public Destination(ChildDestination child) => Child = child;

        public ChildDestination Child { get; }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>();
            builder.Map<Source, Destination>()
                __CONFIGURATION__
                .Members((source, _) => new()
                {
                    Value = source.Value + __DELTA__
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(
                new Source(7, new ChildSource(5)));

            if (result.Value != __EXPECTED_VALUE__ ||
                result.Child.Value != 5)
            {
                throw new InvalidOperationException(
                    "The declarative mapping was not actualized.");
            }
        }
    }
}
""";

    // lang=c#
    private const string ManualConversionSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record Source(int Value, ChildSource Child);

    public sealed class Destination
    {
        public Destination(ChildDestination child) => Child = child;

        public ChildDestination Child { get; }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>();
            builder.Map<Source, Destination>()
                .Convert(source => new Destination(
                    new ChildDestination(source!.Child.Value + 30))
                {
                    Value = source.Value + 3
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(
                new Source(7, new ChildSource(5)));

            if (result.Value != 10 || result.Child.Value != 35)
            {
                throw new InvalidOperationException(
                    "The manual conversion was not actualized.");
            }
        }
    }
}
""";
}
