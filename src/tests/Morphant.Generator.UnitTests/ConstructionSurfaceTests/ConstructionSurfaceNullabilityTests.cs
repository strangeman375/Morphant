using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceNullabilityTests
{
    [Test]
    public void Preserves_constructor_input_nullability_and_normalizes_mapping_roots()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System.Diagnostics.CodeAnalysis;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        public Destination(
            string required,
            string? optional,
            [AllowNull] string allowNull,
            [DisallowNull] string? disallowNull,
            int? nullableValue) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source?, Destination?>()
                .Construct((Source source) => new(
                    source.ToString(),
                    null,
                    null,
                    string.Empty,
                    null))
                .Convert((source, previous, _) =>
                    previous.HasValue
                        ? previous.Value
                        : null);
        }
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var plan = generated[
            "Morphant.Generated.Construction.TestCase_Destination.g.cs"];
        var extension = generated[
            "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination.g.cs"];

        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<string> @required"));
        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<string?>? optional"));
        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<string?>? allowNull"));
        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<string> disallowNull"));
        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<int?>? nullableValue"));
        Assert.That(
            extension,
            Does.Contain(
                "Func<global::TestCase.Source, global::TestCase.Morphant.Generated.DestinationConstruction>"));
        Assert.That(
            extension,
            Does.Contain(
                "Func<global::TestCase.Source?, global::Morphant.Option<global::TestCase.Destination>, global::Morphant.Context.MappingContext, global::TestCase.Destination?>"));
    }

    [Test]
    public void Unwraps_nullable_value_roots_without_losing_nested_annotations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System.Collections.Generic;
using Morphant;

namespace TestCase
{
    public struct Source<T> { }

    public struct Destination<T>
    {
        public Destination(T value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<List<string?>>?, Destination<List<string?>>?>()
                .Construct(_ => new(new List<string?>()))
                .Convert((source, previous, _) =>
                    previous.HasValue
                        ? previous.Value
                        : default);
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var extension = generated.Values.Single(static value =>
            value.Contains(
                "MorphantGeneratedMappingExtensions",
                StringComparison.Ordinal));

        Assert.That(
            extension,
            Does.Contain(
                "Func<global::TestCase.Source<global::System.Collections.Generic.List<string?>>, global::TestCase.Morphant.Generated.DestinationConstruction<global::System.Collections.Generic.List<string?>>>"));
        Assert.That(
            extension,
            Does.Contain(
                "Option<global::TestCase.Destination<global::System.Collections.Generic.List<string?>>>"));
    }
}
