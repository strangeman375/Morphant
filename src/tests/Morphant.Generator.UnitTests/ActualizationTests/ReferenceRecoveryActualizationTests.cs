using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class ReferenceRecoveryActualizationTests
{
    private static readonly string[] GeneratedFiles =
    [
        "Morphant.Generated.Construction.ExternalModels_Destination.g.cs",
        "Morphant.Generated.MappingExtension.TestCase_Source__ExternalModels_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.Member.ExternalModels_Destination.g.cs",
        "Morphant.Generated.MemberExtension.TestCase_Source__ExternalModels_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs"
    ];

    [Test]
    public void Removes_outputs_for_a_missing_reference_and_restores_them()
    {
        var firstReference = CreateReference(
            "ExternalModels",
            ExternalModelsSource);
        var equivalentReference = CreateReference(
            "ExternalModels",
            ExternalModelsSource);
        var files = new[] { SourceFile("TestCase.cs", MapperSource) };
        var namespaceStart = MapperSource.IndexOf(
            "ExternalModels",
            StringComparison.Ordinal);
        var destinationStart = MapperSource.LastIndexOf(
            "Destination",
            StringComparison.Ordinal);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            StepWithReferences(
                "reference available",
                files,
                [firstReference],
                GeneratedFiles),
            StepWithReferences(
                "equivalent reference object substituted",
                files,
                [equivalentReference],
                GeneratedFiles),
            StepWithReferencesAndDiagnostics(
                "used reference removed",
                files,
                [],
                [],
                [
                    CompilerDiagnostic(
                        "CS0246",
                        DiagnosticSeverity.Error,
                        "TestCase.cs",
                        namespaceStart,
                        "ExternalModels".Length),
                    CompilerDiagnostic(
                        "CS0246",
                        DiagnosticSeverity.Error,
                        "TestCase.cs",
                        destinationStart,
                        "Destination".Length)
                ]),
            StepWithReferences(
                "used reference restored",
                files,
                [equivalentReference],
                GeneratedFiles));
    }

    // lang=c#
    private const string ExternalModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace ExternalModels
{
    public sealed class Destination
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string MapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using ExternalModels;
using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";
}
