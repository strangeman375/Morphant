// Compiled integration scenario: CallbackDiagnosticsTests::Suppressed_transfer_failures_keep_atomic_and_independent_paths
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0030, MORPH0031

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsDiscovery_9d7a0201
{
    public sealed class Source
    {
    }

    public sealed class ConstructDestination
    {
    }

    public sealed class ResolveDestination
    {
    }

    public sealed class MembersDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConstructUsingDestination
    {
    }

    public sealed class ResolveUsingDestination
    {
    }

    public sealed class ConvertDestination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        private static bool SelectFirst => true;

        protected override void Configure(MapperBuilder builder)
        {
            global::Morphant.Delegates.ConstructUsing<
                Source,
                ConstructUsingDestination> constructUsing =
                source => new ConstructUsingDestination();
            global::Morphant.Delegates.ResolveUsing<
                Source,
                ResolveUsingDestination,
                ResolveUsingDestination> resolveUsingFirst =
                (source, previous) => new ResolveUsingDestination();
            global::Morphant.Delegates.ResolveUsing<
                Source,
                ResolveUsingDestination,
                ResolveUsingDestination> resolveUsingSecond =
                (source, previous) => new ResolveUsingDestination();
            global::Morphant.Delegates.Convert<Source?, ConvertDestination>
                convertFirst = source => new ConvertDestination();
            global::Morphant.Delegates.Convert<Source?, ConvertDestination>
                convertSecond = source => new ConvertDestination();

            builder.Map<Source, ConstructDestination>()
                .Construct(source =>
                {
                    _ = builder;
                    return new();
                });
            builder.Map<Source, ResolveDestination>()
                .Resolve((source, previous) =>
                {
                    _ = builder;
                    return new();
                });
            builder.Map<Source, MembersDestination>()
                .Members(source =>
                {
                    _ = builder;
                    return new();
                });
            builder.Map<Source, ConstructUsingDestination>()
                .ConstructUsing(constructUsing);
            builder.Map<Source, ResolveUsingDestination>()
                .ResolveUsing(
                    SelectFirst
                        ? resolveUsingFirst
                        : resolveUsingSecond);
            builder.Map<Source, ConvertDestination>()
                .Convert(
                    SelectFirst
                        ? convertFirst
                        : convertSecond);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            AssertUnsupported<ConstructDestination>(mapper);
            AssertUnsupported<ResolveDestination>(mapper);
            AssertUnsupported<MembersDestination>(mapper);
            AssertUnsupported<ConstructUsingDestination>(mapper);
            AssertUnsupported<ResolveUsingDestination>(mapper);
            AssertUnsupported<ConvertDestination>(mapper);
        }

        private static void AssertUnsupported<TDestination>(
            TestMapper mapper)
        {
            var contract = (ITypeMapper<Source, TDestination>)mapper;

            try
            {
                contract.Create(new Source(), default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                $"The {typeof(TDestination)} callback escaped transfer " +
                "analysis or its mapping contract was omitted.");
        }
    }
}
