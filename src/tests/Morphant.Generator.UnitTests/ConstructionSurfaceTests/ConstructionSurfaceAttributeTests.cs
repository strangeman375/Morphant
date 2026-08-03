using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceAttributeTests
{
    [Test]
    public void Copies_applicable_Obsolete_attributes_to_the_generated_plan()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591, CS0612, CS0618

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    [Obsolete("Use CurrentDestination instead.")]
    public sealed class Destination
    {
        [Obsolete("Use the string constructor.", true)]
        public Destination(int value) { }

        public Destination(string value) { }
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

        Assert.That(
            plan,
            Does.Contain(
                "[global::System.ObsoleteAttribute(\"Use CurrentDestination instead.\")]\r\n" +
                "    internal sealed class DestinationConstruction"));
        Assert.That(
            plan,
            Does.Contain(
                "[global::System.ObsoleteAttribute(\"Use the string constructor.\", true)]\r\n" +
                "        public DestinationConstruction(global::Morphant.Members.ConstructorParameter<int> value)"));
        Assert.That(
            plan.Split(
                "[global::System.ObsoleteAttribute",
                StringSplitOptions.None),
            Has.Length.EqualTo(3));
    }
}
