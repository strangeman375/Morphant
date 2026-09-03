using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class AssemblySettingsLifecycleActualizationTests
{
    private static readonly string[] GeneratedFiles =
    [
        "Morphant.Generated.Construction.TestCase_Destination.g.cs",
        "Morphant.Generated.MappingExtension." +
        "TestCase_Source__TestCase_Destination.g.cs",
        "Morphant.Generated.Member.TestCase_Destination.g.cs",
        "Morphant.Generated.MemberExtension." +
        "TestCase_Source__TestCase_Destination.g.cs",
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs"
    ];

    [Test]
    public void Actualizes_each_assembly_setting_independently_and_restores()
    {
        var files = new[] { SourceFile("TestCase.cs", Source) };
        var sourceTypeStart = Source.IndexOf(
            "Source?, Destination?",
            StringComparison.Ordinal);
        var destinationTypeStart = sourceTypeStart + "Source?, ".Length;
        var sourceMemberStart = Source.IndexOf(
            "SourceOnly",
            StringComparison.Ordinal);
        var destinationMemberStart = Source.IndexOf(
            "DestinationOnly",
            StringComparison.Ordinal);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            DefaultStep("defaults", files),
            SettingStep(
                "create-only mapping mode",
                files,
                "MorphantMappingMode",
                "Create"),
            DefaultStep("mapping mode restored", files),
            SettingStep(
                "throwing null-source handling",
                files,
                "MorphantNullSourceHandling",
                "Throw"),
            DefaultStep("null-source handling restored", files),
            SettingStep(
                "throwing null-destination handling",
                files,
                "MorphantNullDestinationHandling",
                "Throw"),
            DefaultStep("null-destination handling restored", files),
            SettingStep(
                "throwing unknown-derived handling",
                files,
                "MorphantUnknownDerivedTypeHandling",
                "Throw"),
            DefaultStep("unknown-derived handling restored", files),
            SettingStep(
                "parameterless constructor selection",
                files,
                "MorphantConstructorSelection",
                "Parameterless"),
            DefaultStep("constructor selection restored", files),
            SettingStep(
                "explicit member selection",
                files,
                "MorphantMemberSelection",
                "Explicit"),
            DefaultStep("member selection restored", files),
            SettingStep(
                "disabled flattening",
                files,
                "MorphantFlattening",
                "None"),
            DefaultStep("flattening restored", files),
            StepWithOptionsAndDiagnostics(
                "strict unmapped-member validation",
                files,
                new Dictionary<string, string>
                {
                    ["build_property.MorphantUnmappedMemberValidation"] =
                        "Strict"
                },
                GeneratedFiles,
                [
                    CompilerDiagnostic(
                        "MORPH0047",
                        DiagnosticSeverity.Warning,
                        "TestCase.cs",
                        sourceTypeStart,
                        "Source?".Length,
                        CompilerDiagnosticLocation(
                            "TestCase.cs",
                            sourceMemberStart,
                            "SourceOnly".Length)),
                    CompilerDiagnostic(
                        "MORPH0048",
                        DiagnosticSeverity.Warning,
                        "TestCase.cs",
                        destinationTypeStart,
                        "Destination?".Length,
                        CompilerDiagnosticLocation(
                            "TestCase.cs",
                            destinationMemberStart,
                            "DestinationOnly".Length))
                ]),
            DefaultStep("unmapped-member validation restored", files));
    }

    private static GeneratorIncrementalityStep DefaultStep(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> files)
    {
        return StepWithOptions(
            name,
            files,
            new Dictionary<string, string>(),
            GeneratedFiles);
    }

    private static GeneratorIncrementalityStep SettingStep(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> files,
        string property,
        string value)
    {
        return StepWithOptions(
            name,
            files,
            new Dictionary<string, string>
            {
                ["build_property." + property] = value
            },
            GeneratedFiles);
    }

    // lang=c#
    private const string Source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }

        public int SourceOnly { get; init; }
    }

    public sealed class Destination
    {
        public Destination()
        {
            Constructor = 1;
        }

        public Destination(int value)
        {
            Constructor = 2;
            Value = value;
        }

        public int Constructor { get; }

        public int Value { get; set; }

        public int DestinationOnly { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source?, Destination?>();
    }
}
""";
}
