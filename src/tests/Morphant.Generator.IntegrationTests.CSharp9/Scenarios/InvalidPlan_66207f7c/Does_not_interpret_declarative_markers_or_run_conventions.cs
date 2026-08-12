// Compiled integration scenario: TypeMapperConvertTests/InvalidPlanTests::Does_not_interpret_declarative_markers_or_run_conventions
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0033

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidPlan_66207f7c
{
    public sealed record Source(int Value);

    public sealed class PlainDestination
    {
        public int Value { get; set; }
    }

    public sealed class AutoDestination
    {
    }

    public sealed class IgnoreDestination
    {
    }

    public sealed class ConventionDestination
    {
    }

    public sealed class MapDestination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, PlainDestination>()
                .Convert((_, _, _) => new()
                {
                    Value = -1
                });

            builder.Map<Source, AutoDestination>()
                .Convert((_, _, _) => Wrap<AutoDestination>(Auto()));

            builder.Map<Source, IgnoreDestination>()
                .Convert((_, _, _) => Wrap<IgnoreDestination>(Ignore()));

            builder.Map<Source, ConventionDestination>()
                .Convert((_, _, _) =>
                    Wrap<ConventionDestination>(ByConvention()));

            builder.Map<Source, MapDestination>()
                .Convert((source, _, _) =>
                    Wrap<MapDestination>(Map(source)));
        }

        private static TDestination Wrap<TDestination>(object marker)
            where TDestination : new() =>
            new();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var source = new Source(42);
            var plain =
                ((ITypeMapper<Source, PlainDestination>)mapper)
                .Create(source, context);

            if (plain.Value != -1)
            {
                throw new InvalidOperationException(
                    "Convert unexpectedly ran convention members.");
            }

            ExpectUnsupported(() =>
                ((ITypeMapper<Source, AutoDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, IgnoreDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, ConventionDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, MapDestination>)mapper)
                .Create(source, context));
        }

        private static void ExpectUnsupported(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A declarative marker escaped into Convert.");
        }
    }
}
