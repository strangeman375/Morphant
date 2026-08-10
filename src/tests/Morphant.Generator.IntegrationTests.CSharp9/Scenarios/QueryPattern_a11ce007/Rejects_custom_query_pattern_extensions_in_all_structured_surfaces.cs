// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/TransferredExpressionsTests::Rejects_custom_query_pattern_extensions_in_all_structured_surfaces
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;
using Morphant.Generator.IntegrationTests.CSharp9.QueryPattern_a11ce007;

namespace Morphant.Generator.IntegrationTests.CSharp9.QueryPattern_a11ce007
{
    public sealed class Sequence<T>
    {
    }

    public static class QueryOperators
    {
        public static Sequence<T> Where<T>(
            this Sequence<T> source,
            Func<T, bool> predicate) =>
            source;

        public static Sequence<TResult> Select<T, TResult>(
            this Sequence<T> source,
            Func<T, TResult> selector) =>
            new Sequence<TResult>();
    }
}

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.QueryPattern_a11ce007
{
    public sealed class Source
    {
        public Sequence<int> Values { get; } =
            new Sequence<int>();
    }

    public sealed class ConstructDestination
    {
        public ConstructDestination(Sequence<int> values) =>
            Values = values;

        public Sequence<int> Values { get; }
    }

    public sealed class ResolveDestination
    {
        public ResolveDestination(Sequence<int> values) =>
            Values = values;

        public Sequence<int> Values { get; }
    }

    public sealed class MembersDestination
    {
        public Sequence<int> Values { get; set; } =
            new Sequence<int>();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(source => new(
                    from value in source.Values
                    where value > 0
                    select value));

            builder.Map<Source, ResolveDestination>()
                .Resolve((source, _) => new(
                    from value in source.Values
                    where value > 0
                    select value));

            builder.Map<Source, MembersDestination>()
                .Members(source => new()
                {
                    Values =
                        from value in source.Values
                        where value > 0
                        select value
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source();

            ExpectUnsupported<ConstructDestination>(mapper, source);
            ExpectUnsupported<ResolveDestination>(mapper, source);
            ExpectUnsupported<MembersDestination>(mapper, source);
        }

        private static void ExpectUnsupported<TDestination>(
            TestMapper mapper,
            Source source)
        {
            try
            {
                ((ITypeMapper<Source, TDestination>)mapper).Create(
                    source,
                    default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A custom query pattern escaped into generated code.");
        }
    }
}
