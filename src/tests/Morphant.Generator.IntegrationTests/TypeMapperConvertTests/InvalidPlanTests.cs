using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperConvertTests;

[TestFixture]
internal sealed class InvalidPlanTests
{
    [Test]
    public void Rejects_captures_duplicates_mixed_plans_and_map_settings()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace TestCase
{
    public sealed record Source(int Value);

    public sealed record CapturedDestination(int Value);

    public sealed record FunctionDestination(int Value);

    public sealed record DuplicateDestination(int Value);

    public sealed record MixedConstructDestination(int Value);

    public sealed class MixedMembersDestination
    {
        public int Value { get; set; }
    }

    public sealed record SettingDestination(int Value);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var offset = Environment.TickCount;

            CapturedDestination Create(Source? source) =>
                new((source?.Value ?? 0) + 1);

            builder.Map<Source, CapturedDestination>()
                .Convert((source, _, _) =>
                    new((source?.Value ?? 0) + offset));

            builder.Map<Source, FunctionDestination>()
                .Convert((source, _, _) =>
                    new FunctionDestination(Create(source).Value));

            builder.Map<Source, DuplicateDestination>()
                .Convert((source, _, _) =>
                    new(source?.Value ?? 0))
                .Convert((source, _, _) =>
                    new((source?.Value ?? 0) + 1));

            builder.Map<Source, MixedConstructDestination>()
                .Construct(source => new(source.Value))
                .Convert((source, _, _) =>
                    new(source?.Value ?? 0));

            builder.Map<Source, MixedMembersDestination>()
                .Members((source, _) => new()
                {
                    Value = source.Value
                })
                .Convert((source, _, _) => new()
                {
                    Value = source?.Value ?? 0
                });

            builder.Map<Source, SettingDestination>()
                .NullSourceHandling(NullSourceHandling.ReturnNull)
                .Convert((source, _, _) =>
                    new(source?.Value ?? 0));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var source = new Source(3);

            ExpectUnsupported(() =>
                ((ITypeMapper<Source, CapturedDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, FunctionDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, DuplicateDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, MixedConstructDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, MixedMembersDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, SettingDestination>)mapper)
                .Create(source, context));
        }

        private static void ExpectUnsupported(Action action)
        {
            try
            {
                action();
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An invalid Convert plan was executed.");
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Does_not_interpret_declarative_markers_or_run_conventions()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace TestCase
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

    public sealed class FactoryDestination
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

            builder.Map<Source, FactoryDestination>()
                .Convert((_, _, _) =>
                    Wrap<FactoryDestination>(
                        ByFactory(() => new FactoryDestination())));

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
                ((ITypeMapper<Source, FactoryDestination>)mapper)
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
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A declarative marker escaped into Convert.");
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
