// Compiled integration scenario: TypeMapperNestedMapTests/UnsupportedTests::Rejects_ambiguous_or_incompatible_maps_without_implicit_auto_dispatch
#nullable enable
#pragma warning disable CS1591
#pragma warning disable CS8619
#pragma warning disable MORPH0031, MORPH0033
#pragma warning disable MORPH0040

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Unsupported_4217c0f0
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record Source(ChildSource Child);

    public sealed class AmbiguousDestination
    {
        public ChildDestination First { get; set; } = new(-1);

        public ChildDestination Second { get; set; } = new(-1);
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

    public sealed class SpoofedSelectorDestination
    {
        public ChildDestination Child { get; set; } = new(-1);
    }

    public sealed class SpoofedMembers
    {
        public global::Morphant.Members.Member<ChildDestination> Child => null!;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AmbiguousDestination>()
                .Members((source, _) =>
                {
                    var child = Map(source.Child);
                    return new()
                    {
                        First = child,
                        Second = child
                    };
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

            builder.Map<Source, SpoofedSelectorDestination>()
                .Members((source, _) =>
                {
                    var members = new SpoofedMembers();
                    Update(source.Child, members.Child);
                    return new();
                });

        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source(new ChildSource(1));

            AssertUnsupported<AmbiguousDestination>(mapper, source);
            AssertUnsupported<IncompatibleDestination>(mapper, source);
            AssertUnsupported<NullableResultDestination>(mapper, source);
            AssertUnsupported<AutomaticDestination>(mapper, source);
            AssertUnsupported<SpoofedSelectorDestination>(mapper, source);
        }

        private static void AssertUnsupported<TDestination>(
            TestMapper mapper,
            Source source)
        {
            try
            {
                ((ITypeMapper<Source, TDestination>)mapper).Create(
                    source,
                    default(MappingContext));
                throw new InvalidOperationException(
                    "An invalid declarative Map was accepted.");
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
            }
        }
    }
}
