// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/SourceDiscardTests::Removes_structured_source_discards_without_changing_runtime_callbacks
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0031

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SourceDiscard_a11ce00e
{
    public sealed class Source
    {
        public int Value { get; init; }

        public int Probe
        {
            get
            {
                ProbeReads++;
                return Value;
            }
        }

        public int ProbeReads { get; private set; }
    }

    public sealed class ConstructDestination
    {
        public int Value { get; set; }
    }

    public sealed class ResolveDestination
    {
        public int Value { get; set; }
    }

    public sealed class MembersDestination
    {
        public int Value { get; set; }
    }

    public sealed class RuntimeConstructDestination
    {
        public int Value { get; set; }
    }

    public sealed class RuntimeResolveDestination
    {
        public int Value { get; set; }
    }

    public sealed class NonDiscardDestination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(source =>
                {
                    _ = source.Probe;
                    return new();
                })
                .Members(source => new() { Value = source.Value });

            builder.Map<Source, ResolveDestination>()
                .Resolve((source, previous) =>
                {
                    _ = source.Probe;
                    return new();
                })
                .Members(source => new() { Value = source.Value });

            builder.Map<Source, MembersDestination>()
                .Members(source =>
                {
                    _ = source.Probe;
                    return new() { Value = source.Value };
                });

            builder.Map<Source, RuntimeConstructDestination>()
                .ConstructUsing(source =>
                {
                    _ = source.Probe;
                    return new RuntimeConstructDestination
                    {
                        Value = source.Value
                    };
                });

            builder.Map<Source, RuntimeResolveDestination>()
                .ResolveUsing((source, previous) =>
                {
                    _ = source.Probe;
                    return previous.HasValue
                        ? previous.Value
                        : new RuntimeResolveDestination
                        {
                            Value = source.Value
                        };
                });

            builder.Map<Source, NonDiscardDestination>()
                .Construct(source =>
                {
                    var _ = 0;
                    _ = source.Probe;
                    return new();
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            AssertReads<ConstructDestination>(mapper, expectedReads: 0);
            AssertReads<ResolveDestination>(mapper, expectedReads: 0);
            AssertReads<MembersDestination>(mapper, expectedReads: 0);
            AssertReads<RuntimeConstructDestination>(mapper, expectedReads: 1);
            AssertReads<RuntimeResolveDestination>(mapper, expectedReads: 1);
            AssertNonDiscardIsRejected(mapper);
        }

        private static void AssertReads<TDestination>(
            TestMapper mapper,
            int expectedReads)
        {
            var source = new Source { Value = 17 };
            var destination =
                ((ITypeMapper<Source, TDestination>)mapper)
                .Create(source, default(MappingContext));

            if (destination is null || source.ProbeReads != expectedReads)
            {
                throw new InvalidOperationException(
                    $"Expected {expectedReads} probe reads for " +
                    $"{typeof(TDestination)}, observed {source.ProbeReads}.");
            }
        }

        private static void AssertNonDiscardIsRejected(TestMapper mapper)
        {
            try
            {
                ((ITypeMapper<Source, NonDiscardDestination>)mapper)
                    .Create(
                        new Source { Value = 17 },
                        default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A local named '_' must not be treated as a compile-time " +
                "source discard.");
        }
    }
}
