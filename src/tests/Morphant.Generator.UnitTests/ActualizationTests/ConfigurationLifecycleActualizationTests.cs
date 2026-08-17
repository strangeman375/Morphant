using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class ConfigurationLifecycleActualizationTests
{
    private const string Mapper =
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

    private static readonly string[] PrimaryGeneratedFiles =
    [
        "Morphant.Generated.Construction.TestCase_Destination.g.cs",
        "Morphant.Generated.MappingExtension." +
        "TestCase_Source__TestCase_Destination.g.cs",
        "Morphant.Generated.Member.TestCase_Destination.g.cs",
        "Morphant.Generated.MemberExtension." +
        "TestCase_Source__TestCase_Destination.g.cs",
        Mapper
    ];

    private static readonly string[] BothGeneratedFiles =
    [
        .. PrimaryGeneratedFiles,
        "Morphant.Generated.Construction.TestCase_SecondDestination.g.cs",
        "Morphant.Generated.MappingExtension." +
        "TestCase_SecondSource__TestCase_SecondDestination.g.cs",
        "Morphant.Generated.Member.TestCase_SecondDestination.g.cs",
        "Morphant.Generated.MemberExtension." +
        "TestCase_SecondSource__TestCase_SecondDestination.g.cs"
    ];

    [Test]
    public void Actualizes_added_and_removed_configuration_without_stale_code()
    {
        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ScenarioStep("default mapping", string.Empty, 0, 7),
            ScenarioStep(
                "Members added",
                """
                .Members((source, _) => new()
                {
                    Value = source.Value + 10
                })
                """,
                0,
                17),
            ScenarioStep("Members removed", string.Empty, 0, 7),
            ScenarioStep(
                "ConstructUsing added",
                ".ConstructUsing(source => " +
                "new Destination { Marker = source.Value + 20 })",
                27,
                7),
            ScenarioStep("ConstructUsing removed", string.Empty, 0, 7),
            ScenarioStep(
                "ResolveUsing added",
                """
                .ResolveUsing((source, previous) =>
                    previous.HasValue
                        ? previous.Value
                        : new Destination
                        {
                            Marker = source.Value + 30
                        })
                """,
                37,
                7),
            ScenarioStep("ResolveUsing removed", string.Empty, 0, 7),
            ScenarioStep(
                "Convert added",
                ".Convert(_ => new Destination { Marker = 37, Value = 38 })",
                37,
                38),
            ScenarioStep("Convert removed", string.Empty, 0, 7),
            ScenarioStep(
                "second Map added",
                string.Empty,
                0,
                7,
                includeSecondMap: true),
            ScenarioStep("second Map removed", string.Empty, 0, 7));
    }

    private static GeneratorIncrementalityStep ScenarioStep(
        string name,
        string configuration,
        int expectedMarker,
        int expectedValue,
        bool includeSecondMap = false)
    {
        var source = SourceTemplate
            .Replace("__CONFIGURATION__", configuration)
            .Replace(
                "__SECOND_MAP__",
                includeSecondMap
                    ? "builder.Map<SecondSource, SecondDestination>();"
                    : string.Empty)
            .Replace("__EXPECTED_MARKER__", expectedMarker.ToString())
            .Replace("__EXPECTED_VALUE__", expectedValue.ToString())
            .Replace(
                "__SECOND_ASSERTION__",
                includeSecondMap
                    ? """
                      var secondMapper =
                          (ITypeMapper<SecondSource, SecondDestination>)mapper;
                      var second = secondMapper.Create(new SecondSource
                      {
                          Value = 11
                      });

                      if (second.Value != 11)
                      {
                          throw new InvalidOperationException(
                              "The second mapping was not actualized.");
                      }
                      """
                    : """
                      if ((object)mapper is
                          ITypeMapper<SecondSource, SecondDestination>)
                      {
                          throw new InvalidOperationException(
                              "A removed mapping remained in generated code.");
                      }
                      """);

        return ExecutableStep(
            name,
            [SourceFile("TestCase.cs", source)],
            includeSecondMap ? BothGeneratedFiles : PrimaryGeneratedFiles,
            "TestCase.Scenario",
            name == "default mapping" ? [] : [ChangedMapper()]);
    }

    private static ExpectedIncrementalStage ChangedMapper()
    {
        return Stage(
            "BuildTypeMapperRequests",
            Expected(Mapper, IncrementalStepRunReason.Modified));
    }

    // lang=c#
    private const string SourceTemplate =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Marker { get; init; }

        public int Value { get; set; }
    }

    public sealed class SecondSource
    {
        public int Value { get; init; }
    }

    public sealed class SecondDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                __CONFIGURATION__;
            __SECOND_MAP__
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var primaryMapper =
                (ITypeMapper<Source, Destination>)mapper;
            var primary = primaryMapper.Create(new Source { Value = 7 });

            if (primary.Marker != __EXPECTED_MARKER__ ||
                primary.Value != __EXPECTED_VALUE__)
            {
                throw new InvalidOperationException(
                    "The primary mapping was not actualized.");
            }

            __SECOND_ASSERTION__
        }
    }
}
""";
}
