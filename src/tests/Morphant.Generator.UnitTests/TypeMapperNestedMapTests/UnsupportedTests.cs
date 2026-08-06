using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class UnsupportedTests
{
    [Test]
    public void Rejects_untyped_or_incompatible_maps_without_implicit_auto_dispatch()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
#pragma warning disable CS8619

using System;
using Morphant;
using Morphant.Context;

namespace TestCase
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record Source(ChildSource Child);

    public sealed class UntypedLocalDestination
    {
        public ChildDestination Child { get; set; } = new(-1);
    }

    public sealed class IncompatibleDestination
    {
        public string Text { get; set; } = string.Empty;
    }

    public sealed class NullableResultDestination
    {
        public ChildDestination Child { get; set; } = new(-1);
    }

    public sealed class AutomaticDestination
    {
        public ChildDestination Child { get; set; } = new(-1);
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, UntypedLocalDestination>()
                .Members((source, _) =>
                {
                    var child = Map(source.Child);
                    return new() { Child = child };
                });

            builder.Map<Source, IncompatibleDestination>()
                .Members((source, _) => new()
                {
                    Text = Map<int>(source.Child.Value)
                });

            builder.Map<Source, NullableResultDestination>()
                .Members((source, _) => new()
                {
                    Child = Map<ChildDestination?>(source.Child)
                });

            builder.Map<Source, AutomaticDestination>()
                .Members((source, _) => new()
                {
                    Child = Auto()
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source(new ChildSource(1));

            AssertUnsupported<UntypedLocalDestination>(mapper, source);
            AssertUnsupported<IncompatibleDestination>(mapper, source);
            AssertUnsupported<NullableResultDestination>(mapper, source);
            AssertUnsupported<AutomaticDestination>(mapper, source);
        }

        private static void AssertUnsupported<TDestination>(
            TestMapper mapper,
            Source source)
        {
            try
            {
                ((ITypeMapper<Source, TDestination>)mapper).Map(
                    source,
                    default(MappingContext));
                throw new InvalidOperationException(
                    "An invalid declarative Map was accepted.");
            }
            catch (NotSupportedException)
            {
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
