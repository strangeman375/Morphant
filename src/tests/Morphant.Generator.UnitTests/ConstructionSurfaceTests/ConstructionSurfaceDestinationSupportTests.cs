using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceDestinationSupportTests
{
    [Test]
    public void Generates_only_the_surface_allowed_by_each_destination_capability()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using System.Collections.Generic;
using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class EmptyClass { }
    public struct CustomStruct { }
    public interface IDestination { }
    public abstract class AbstractDestination { }

    public sealed class FactoryOnly
    {
        private FactoryOnly() { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, EmptyClass>();
            builder.Map<Source, CustomStruct>();
            builder.Map<Source, IDestination>();
            builder.Map<Source, AbstractDestination>();
            builder.Map<Source, FactoryOnly>();
            builder.Map<Source, int>();
            builder.Map<Source, List<int>>();
        }
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);

        Assert.That(
            generated.Keys,
            Is.EquivalentTo(new[]
            {
                "Morphant.Generated.Construction.TestCase_CustomStruct.g.cs",
                "Morphant.Generated.Construction.TestCase_EmptyClass.g.cs",
                "Morphant.Generated.MappingExtension.TestCase_Source__System_Int32.g.cs",
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_AbstractDestination.g.cs",
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_CustomStruct.g.cs",
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_EmptyClass.g.cs",
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_FactoryOnly.g.cs",
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_IDestination.g.cs"
            }));

        Assert.That(
            generated.Values.Count(static value =>
                value.Contains(
                    "internal sealed class EmptyClassConstruction",
                    StringComparison.Ordinal)),
            Is.EqualTo(1));
        Assert.That(
            generated.Values.Count(static value =>
                value.Contains(
                    "internal sealed class CustomStructConstruction",
                    StringComparison.Ordinal)),
            Is.EqualTo(1));
        Assert.That(
            generated.Values,
            Has.None.Contains("IDestinationConstruction"));
        Assert.That(
            generated.Values,
            Has.None.Contains("FactoryOnlyConstruction"));
    }

    [Test]
    public void Uses_only_constructors_visible_from_the_common_generated_context()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        private Destination(byte value) { }
        protected Destination(short value) { }
        private protected Destination(long value) { }
        internal Destination(int value) { }
        protected internal Destination(uint value) { }
        public Destination(string value) { }

        [MorphantMapper]
        public partial class TestMapper : TypeMapper
        {
            protected override void Configure(MapperBuilder builder) =>
                builder.Map<Source, Destination>();
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

        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<int> value"));
        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<uint> value"));
        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<string> value"));
        Assert.That(
            plan,
            Does.Not.Contain("ConstructorParameter<byte> value"));
        Assert.That(
            plan,
            Does.Not.Contain("ConstructorParameter<short> value"));
        Assert.That(
            plan,
            Does.Not.Contain("ConstructorParameter<long> value"));
    }

    [Test]
    public void Reuses_one_plan_for_nullable_and_non_nullable_custom_structs()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public struct Destination
    {
        public Destination(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, Destination?>();
        }
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);

        Assert.That(
            generated.Keys.Count(static name =>
                name.Contains(".Construction.", StringComparison.Ordinal)),
            Is.EqualTo(1));
        Assert.That(
            generated.Keys.Count(static name =>
                name.Contains(".MappingExtension.", StringComparison.Ordinal)),
            Is.EqualTo(2));
    }
}
