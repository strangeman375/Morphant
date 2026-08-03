using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceDocumentationTests
{
    [Test]
    public void Inherits_destination_documentation_and_supplies_complete_fallbacks()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

namespace TestCase
{
    /// <summary>Source documentation.</summary>
    public sealed class Source { }

    /// <summary>Destination documentation.</summary>
    public sealed class Destination
    {
        /// <summary>Creates a destination from its value.</summary>
        /// <param name="value">The initial value.</param>
        public Destination(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
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
            Does.Contain(
                "/// <inheritdoc cref=\"global::TestCase.Destination\"/>"));
        Assert.That(
            plan,
            Does.Contain(
                "/// <inheritdoc cref=\"global::TestCase.Destination.Destination(global::System.Int32)\"/>"));
        Assert.That(
            plan,
            Does.Contain(
                "Contains mappings for constructor arguments of <see cref=\"global::TestCase.Destination\"/>."));
        Assert.That(
            plan,
            Does.Contain(
                "Configures the <c>value</c> constructor argument."));
        Assert.That(
            plan,
            Does.Contain(
                "Creates a destination instance using convention-based mapping."));
        Assert.That(
            plan,
            Does.Contain(
                "Creates a destination instance using factory-based construction."));
        Assert.That(
            plan,
            Does.Contain(
                "Selects an existing destination as the mapping result."));

        Assert.That(
            extension,
            Does.Contain(
                "Configures how to construct a destination when no existing destination is used."));
        Assert.That(
            extension,
            Does.Contain(
                "Configures how to select or construct the destination from the source and an optional existing destination."));
        Assert.That(
            extension,
            Does.Contain("Configures a fully manual mapping algorithm."));
    }

    [Test]
    public void Generates_a_meaningful_plan_summary_without_destination_docs()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var plan = generated[
            "Morphant.Generated.Construction.TestCase_Destination.g.cs"];

        Assert.That(
            plan,
            Does.Contain(
                "Describes construction of <see cref=\"global::TestCase.Destination\"/>."));
    }
}
