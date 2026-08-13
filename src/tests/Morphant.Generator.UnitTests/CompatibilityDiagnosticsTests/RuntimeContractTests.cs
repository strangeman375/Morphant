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
                RuntimeContractFixture.Compatible().CreateReference()
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
            sources:
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
                "Morphant requires a reference to a compatible runtime library."));
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
                "the runtime does not provide compatibility information"));
    }

    [Test]
    public void Gives_ambiguity_precedence_over_candidate_compatibility()
    {
        var incompatible =
            RuntimeContractFixture.Compatible()
                .WithRevision("2")
                .CreateReference();
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
                "Multiple Morphant runtime libraries were found. Reference exactly one."));
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
            sources: [shadow],
            references: [CompatibilityGeneratorTest.ActualRuntimeReference]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0003",
                "Multiple Morphant runtime libraries were found. Reference exactly one."));
    }

    [TestCase("")]
    [TestCase("01")]
    [TestCase("+1")]
    [TestCase(" 1")]
    [TestCase("2147483648")]
    public void Rejects_noncanonical_or_unrepresentable_revision_metadata(
        string revision)
    {
        var runtime = RuntimeContractFixture.Compatible()
            .WithRevision(revision)
            .CreateReference();
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible(
                "the runtime contains invalid compatibility information"));
    }

    [Test]
    public void Rejects_duplicate_revision_metadata_before_manifest_shape()
    {
        var runtime = RuntimeContractFixture.Compatible()
            .WithDuplicateRevision("2")
            .CreateReference();
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible(
                "the runtime contains duplicate compatibility information"));
    }

    [Test]
    public void Rejects_an_unsupported_revision_with_the_exact_reason()
    {
        var runtime =
            RuntimeContractFixture.Compatible()
                .WithRevision("2")
                .CreateReference();
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible("the runtime and generator versions do not match"));
    }

    [TestCase(
        RuntimeContractDefect.MissingMapperAttribute,
        "Morphant.MorphantMapperAttribute",
        "is missing")]
    [TestCase(
        RuntimeContractDefect.InternalMapperAttribute,
        "Morphant.MorphantMapperAttribute",
        "has an incompatible shape")]
    [TestCase(
        RuntimeContractDefect.ConfigureReturnsInt,
        "Morphant.TypeMapper",
        "has an incompatible shape")]
    [TestCase(
        RuntimeContractDefect.MapUsesIntInsteadOfMappingMode,
        "Morphant.MapperBuilder",
        "has an incompatible shape")]
    [TestCase(
        RuntimeContractDefect.InvalidMappingModeValues,
        "Morphant.MappingMode",
        "has an incompatible shape")]
    [TestCase(
        RuntimeContractDefect.InvariantConstructSource,
        "Morphant.Delegates.Construct`2",
        "has an incompatible shape")]
    [TestCase(
        RuntimeContractDefect.AutoMarkerWithoutMemberMarker,
        "Morphant.Markers.AutoMarker",
        "has an incompatible shape")]
    [TestCase(
        RuntimeContractDefect.ConstructorParameterConvertsFromString,
        "Morphant.Members.ConstructorParameter`1",
        "has an incompatible shape")]
    [TestCase(
        RuntimeContractDefect.MappingConfigurationReasonIsObject,
        "Morphant.Exceptions.MappingConfigurationException",
        "has an incompatible shape")]
    public void Rejects_each_bootstrap_shape_class(
        RuntimeContractDefect defect,
        string _,
        string __)
    {
        var runtime = RuntimeContractFixture.Compatible()
            .With(defect)
            .CreateReference();
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            sources: [CompatibilityGeneratorTest.EmptySource],
            references: [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible("the runtime API does not match this generator"));
        Assert.That(result.GeneratedSources, Is.Empty);
    }

    [TestCase(
        RuntimeContractDefect.InternalMapperAttribute,
        RuntimeContractDefect.ConfigureReturnsInt,
        "Morphant.MorphantMapperAttribute")]
    [TestCase(
        RuntimeContractDefect.MutableMappingContext,
        RuntimeContractDefect.InvalidMappingModeValues,
        "Morphant.Context.MappingContext")]
    [TestCase(
        RuntimeContractDefect.InvariantConstructSource,
        RuntimeContractDefect.InvariantConstructUsingSource,
        "Morphant.Delegates.ConstructUsing`2")]
    public void Rejects_runtime_with_multiple_shape_failures(
        RuntimeContractDefect firstDefect,
        RuntimeContractDefect secondDefect,
        string _)
    {
        var runtime = RuntimeContractFixture.Compatible()
            .With(firstDefect, secondDefect)
            .CreateReference();
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [runtime]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            Incompatible("the runtime API does not match this generator"));
    }

    private static ExpectedCompatibilityDiagnostic Incompatible(string reason)
    {
        return new ExpectedCompatibilityDiagnostic(
            "MORPH0004",
            "The Morphant runtime is incompatible with " +
            $"this generator: {reason}.");
    }
}
