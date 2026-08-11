using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.CompatibilityDiagnosticsTests;

[TestFixture]
internal sealed class RuntimeContractTests
{
    [Test]
    public void Accepts_the_normal_runtime_and_the_test_owned_revision_1_contract()
    {
        var actual = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references:
            [
                CompatibilityGeneratorTest.ActualRuntimeReference
            ]);
        var fixture = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references:
            [
                CompatibilityGeneratorTest.CreateCompatibleRuntimeReference()
            ]);

        CompatibilityGeneratorTest.AssertDiagnostics(actual);
        CompatibilityGeneratorTest.AssertDiagnostics(fixture);
        Assert.That(actual.GeneratedSources, Is.Empty);
        Assert.That(fixture.GeneratedSources, Is.Empty);
    }

    [Test]
    public void Reports_missing_without_any_runtime_candidate_once_per_compilation()
    {
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            [
                CompatibilityGeneratorTest.EmptySource,
                CompatibilityGeneratorTest.EmptySource.Replace(
                    "Placeholder",
                    "SecondPlaceholder",
                    StringComparison.Ordinal),
                CompatibilityGeneratorTest.EmptySource.Replace(
                    "Placeholder",
                    "ThirdPlaceholder",
                    StringComparison.Ordinal)
            ]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0002",
                "Morphant generator requires a reference to a compatible Morphant runtime library."));
        Assert.That(result.GeneratedSources, Is.Empty);
    }

    [Test]
    public void Treats_a_partial_bootstrap_as_incompatible_not_missing()
    {
        var partialRuntime = CompatibilityGeneratorTest.CreateReference(
            "PartialRuntime",
"""
#pragma warning disable CS1591
using System;
namespace Morphant
{
    public sealed class MorphantMapperAttribute : Attribute
    {
    }
}
""");
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [partialRuntime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible(
                "contract revision metadata 'Morphant.GeneratorContractVersion' is missing"));
    }

    [Test]
    public void Gives_ambiguity_precedence_over_candidate_compatibility()
    {
        var incompatible =
            CompatibilityGeneratorTest.CreateCompatibleRuntimeReference("2");
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references:
            [
                CompatibilityGeneratorTest.ActualRuntimeReference,
                incompatible
            ]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0003",
                "Multiple Morphant runtime contracts were found. Reference exactly one compatible Morphant runtime library."));
    }

    [Test]
    public void Reports_runtime_plus_consumer_shadow_types_as_ambiguous()
    {
        const string shadow =
"""
#nullable enable
#pragma warning disable CS0436, CS1591
using System;

namespace Morphant
{
    public sealed class MorphantMapperAttribute : Attribute
    {
    }
}
""";
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            [shadow],
            [CompatibilityGeneratorTest.ActualRuntimeReference]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0003",
                "Multiple Morphant runtime contracts were found. Reference exactly one compatible Morphant runtime library."));
    }

    [TestCase("")]
    [TestCase("01")]
    [TestCase("+1")]
    [TestCase(" 1")]
    [TestCase("2147483648")]
    public void Rejects_noncanonical_or_unrepresentable_revision_metadata(
        string revision)
    {
        var runtime =
            CompatibilityGeneratorTest.CreateCompatibleRuntimeReference(
                revision);
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible(
                "contract revision metadata 'Morphant.GeneratorContractVersion' is invalid"));
    }

    [Test]
    public void Rejects_duplicate_revision_metadata_before_manifest_shape()
    {
        var runtime = CompatibilityGeneratorTest.CreateCompatibleRuntimeReference(
            mutate: source => source.Insert(
                source.IndexOf("namespace Morphant", StringComparison.Ordinal),
                "[assembly: AssemblyMetadata(" +
                "\"Morphant.GeneratorContractVersion\", \"2\")]\n\n"));
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible(
                "contract revision metadata 'Morphant.GeneratorContractVersion' is duplicated"));
    }

    [Test]
    public void Rejects_an_unsupported_revision_with_the_exact_reason()
    {
        var runtime =
            CompatibilityGeneratorTest.CreateCompatibleRuntimeReference("2");
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible(
                "contract revision '2' is not supported; expected '1'"));
    }

    [TestCaseSource(nameof(ShapeFailures))]
    public void Rejects_each_bootstrap_shape_class_with_a_stable_first_reason(
        Func<string, string> mutate,
        string metadataName,
        string failureKind)
    {
        var runtime = CompatibilityGeneratorTest.CreateCompatibleRuntimeReference(
            mutate: mutate);
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            [CompatibilityGeneratorTest.EmptySource],
            [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible(
                $"required symbol '{metadataName}' {failureKind}"));
        Assert.That(result.GeneratedSources, Is.Empty);
    }

    [TestCaseSource(nameof(MultipleShapeFailures))]
    public void Reports_the_first_shape_failure_by_group_then_ordinal_name(
        Func<string, string> mutate,
        string metadataName)
    {
        var runtime = CompatibilityGeneratorTest.CreateCompatibleRuntimeReference(
            mutate: mutate);
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible(
                $"required symbol '{metadataName}' has an incompatible shape"));
    }

    private static IEnumerable<TestCaseData> ShapeFailures()
    {
        yield return Case(
            source => source.Replace(
                "MorphantMapperAttribute",
                "MissingMorphantMapperAttribute",
                StringComparison.Ordinal),
            "Morphant.MorphantMapperAttribute",
            "is missing",
            "missing metadata name");
        yield return Case(
            source => source.Replace(
                "public sealed class MorphantMapperAttribute",
                "internal sealed class MorphantMapperAttribute",
                StringComparison.Ordinal),
            "Morphant.MorphantMapperAttribute",
            "has an incompatible shape",
            "accessibility");
        yield return Case(
            source => source.Replace(
                "protected abstract void Configure(MapperBuilder builder);",
                "protected abstract int Configure(MapperBuilder builder);",
                StringComparison.Ordinal),
            "Morphant.TypeMapper",
            "has an incompatible shape",
            "required member signature");
        yield return Case(
            source => source.Replace(
                "MappingMode value = Morphant.MappingMode.Default",
                "int value = 0",
                StringComparison.Ordinal),
            "Morphant.MapperBuilder",
            "has an incompatible shape",
            "builder registration signature");
        yield return Case(
            source => source.Replace(
                "Create = 1,\n        Update = 2,\n        CreateAndUpdate = 3",
                "Create = 4,\n        Update = 2,\n        CreateAndUpdate = 6",
                StringComparison.Ordinal),
            "Morphant.MappingMode",
            "has an incompatible shape",
            "enum constants");
        yield return Case(
            source => source.Replace(
                "public delegate TResult Construct<in TSource, out TResult>",
                "public delegate TResult Construct<TSource, out TResult>",
                StringComparison.Ordinal),
            "Morphant.Delegates.Construct`2",
            "has an incompatible shape",
            "delegate variance");
        yield return Case(
            source => source.Replace(
                "public sealed class AutoMarker : MemberMarker",
                "public sealed class AutoMarker",
                StringComparison.Ordinal),
            "Morphant.Markers.AutoMarker",
            "has an incompatible shape",
            "marker inheritance");
        yield return Case(
            source => source.Replace(
                "implicit operator ConstructorParameter<T>(T value)",
                "implicit operator ConstructorParameter<T>(string value)",
                StringComparison.Ordinal),
            "Morphant.Members.ConstructorParameter`1",
            "has an incompatible shape",
            "wrapper conversion");
        yield return Case(
            source => source.Replace(
                "Type destinationType,\n            string reason)",
                "Type destinationType,\n            object reason)",
                StringComparison.Ordinal),
            "Morphant.Exceptions.MappingConfigurationException",
            "has an incompatible shape",
            "exception constructor");
    }

    private static IEnumerable<TestCaseData> MultipleShapeFailures()
    {
        yield return new TestCaseData(
                (Func<string, string>)(source => source
                    .Replace(
                        "public sealed class MorphantMapperAttribute",
                        "internal sealed class MorphantMapperAttribute",
                        StringComparison.Ordinal)
                    .Replace(
                        "protected abstract void Configure(MapperBuilder builder);",
                        "protected abstract int Configure(MapperBuilder builder);",
                        StringComparison.Ordinal)),
                "Morphant.MorphantMapperAttribute")
            .SetName("Reports_first_failure_by_manifest_group");
        yield return new TestCaseData(
                (Func<string, string>)(source => source
                    .Replace(
                        "public readonly struct MappingContext",
                        "public struct MappingContext",
                        StringComparison.Ordinal)
                    .Replace(
                        "Create = 1,\n        Update = 2,\n        CreateAndUpdate = 3",
                        "Create = 4,\n        Update = 2,\n        CreateAndUpdate = 6",
                        StringComparison.Ordinal)),
                "Morphant.Context.MappingContext")
            .SetName("Reports_first_runtime_symbol_by_ordinal_name");
        yield return new TestCaseData(
                (Func<string, string>)(source => source
                    .Replace(
                        "public delegate TResult Construct<in TSource, out TResult>",
                        "public delegate TResult Construct<TSource, out TResult>",
                        StringComparison.Ordinal)
                    .Replace(
                        "public delegate TResult ConstructUsing<in TSource, out TResult>",
                        "public delegate TResult ConstructUsing<TSource, out TResult>",
                        StringComparison.Ordinal)),
                "Morphant.Delegates.ConstructUsing`2")
            .SetName("Reports_first_delegate_by_ordinal_name");
    }

    private static TestCaseData Case(
        Func<string, string> mutate,
        string metadataName,
        string failureKind,
        string name)
    {
        return new TestCaseData(mutate, metadataName, failureKind)
            .SetName($"Rejects_bootstrap_{name.Replace(' ', '_')}");
    }

    private static ExpectedCompatibilityDiagnostic Incompatible(string reason)
    {
        return new ExpectedCompatibilityDiagnostic(
            "MORPH0004",
            "The referenced Morphant runtime contract is incompatible with " +
            $"this generator: {reason}.");
    }
}
