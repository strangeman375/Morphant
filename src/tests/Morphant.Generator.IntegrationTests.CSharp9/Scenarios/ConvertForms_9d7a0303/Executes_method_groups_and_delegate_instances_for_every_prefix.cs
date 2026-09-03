// Compiled integration scenario: TypeMapperConvertTests/CallbackFormsTests::Executes_method_groups_and_delegate_instances_for_every_prefix
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;
using Morphant.Delegates;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConvertForms_9d7a0303
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed record SourceOnlyDestination(int Value);

    public sealed record PreviousDestination(int Value);

    public sealed record ContextDestination(int Value);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        private readonly Convert<
            Source?,
            ContextDestination,
            MappingContext,
            ContextDestination> _contextDelegate;

        public TestMapper()
        {
            _contextDelegate = (source, previous, context) =>
            {
                ContextDelegateCalls++;
                return new ContextDestination(
                    (source?.Value ?? 0) +
                    (previous.HasValue ? previous.Value.Value : 0) +
                    (context.Operation == MappingOperation.Create
                        ? 100
                        : 200));
            };
        }

        public static int SourceOnlyCalls { get; private set; }

        public static int PreviousCalls { get; private set; }

        public static int ContextDelegateCalls { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, SourceOnlyDestination>()
                .Convert(ConvertSource);
            builder.Map<Source, PreviousDestination>()
                .Convert(ConvertPrevious);
            builder.Map<Source, ContextDestination>()
                .Convert(_contextDelegate);
        }

        private static SourceOnlyDestination ConvertSource(Source? source)
        {
            SourceOnlyCalls++;
            return new SourceOnlyDestination(source?.Value ?? -1);
        }

        private static PreviousDestination ConvertPrevious(
            Source? source,
            Option<PreviousDestination> previous)
        {
            PreviousCalls++;
            return new PreviousDestination(
                (source?.Value ?? 0) +
                (previous.HasValue ? previous.Value.Value : 10));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var sourceOnly =
                ((ITypeMapper<Source, SourceOnlyDestination>)mapper)
                .Create(null);
            var previous =
                ((ITypeMapper<Source, PreviousDestination>)mapper)
                .Update(
                    new Source { Value = 3 },
                    new PreviousDestination(5));
            var contextMapper =
                (ITypeMapper<Source, ContextDestination>)mapper;
            var contextCreate = contextMapper.Create(
                new Source { Value = 7 });
            var contextUpdate = contextMapper.Update(
                new Source { Value = 11 },
                new ContextDestination(13));

            if (sourceOnly.Value != -1 ||
                previous.Value != 8 ||
                contextCreate.Value != 107 ||
                contextUpdate.Value != 224 ||
                TestMapper.SourceOnlyCalls != 1 ||
                TestMapper.PreviousCalls != 1 ||
                TestMapper.ContextDelegateCalls != 2)
            {
                throw new InvalidOperationException(
                    "A Convert method group or delegate instance received " +
                    "the wrong inputs or was invoked more than once.");
            }
        }
    }
}
