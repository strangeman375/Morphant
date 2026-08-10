// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/CaptureTests::Rejects_deferred_previous_and_result_captures_but_allows_snapshots
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DeferredInputs_a11ce008
{
    public sealed class Source
    {
    }

    public sealed class InvalidResolvePreviousDestination
    {
        public InvalidResolvePreviousDestination(Func<bool> callback) =>
            Callback = callback;

        public Func<bool> Callback { get; }
    }

    public sealed class InvalidMembersPreviousDestination
    {
        public Func<bool> Callback { get; set; } = () => false;
    }

    public sealed class InvalidMembersResultReadDestination
    {
        public int Value { get; } = 1;

        public Func<int> Callback { get; set; } = () => -1;
    }

    public sealed class InvalidMembersResultCallDestination
    {
        public Action Callback { get; set; } = () => { };

        public void Reset()
        {
        }
    }

    public sealed class InvalidMembersResultMutationDestination
    {
        public int Value { get; set; }

        public Action Callback { get; set; } = () => { };
    }

    public sealed class InvalidMembersResultLocalFunctionDestination
    {
        public int Value { get; } = 2;

        public Func<int> Callback { get; set; } = () => -1;
    }

    public sealed class InvalidMembersResultAnonymousMethodDestination
    {
        public int Value { get; } = 3;

        public Func<int> Callback { get; set; } = () => -1;
    }

    public sealed class ValidResolveSnapshotDestination
    {
        public ValidResolveSnapshotDestination(Func<bool> callback) =>
            Callback = callback;

        public Func<bool> Callback { get; }
    }

    public sealed class ValidMembersSnapshotDestination
    {
        public int Value { get; } = 7;

        public Func<int> Callback { get; set; } = () => -1;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, InvalidResolvePreviousDestination>()
                .Resolve((_, previous) => new(
                    Value<Func<bool>>(() => previous.HasValue)));

            builder.Map<Source, InvalidMembersPreviousDestination>()
                .Members((_, previous) => new()
                {
                    Callback = Value<Func<bool>>(
                        () => previous.HasValue)
                });

            builder.Map<Source, InvalidMembersResultReadDestination>()
                .Members((_, _, result) => new()
                {
                    Callback = Value<Func<int>>(() => result.Value)
                });

            builder.Map<Source, InvalidMembersResultCallDestination>()
                .Members((_, _, result) => new()
                {
                    Callback = Value<Action>(() => result.Reset())
                });

            builder.Map<Source, InvalidMembersResultMutationDestination>()
                .Members((_, _, result) => new()
                {
                    Callback = Value<Action>(() => result.Value++)
                });

            builder.Map<Source, InvalidMembersResultLocalFunctionDestination>()
                .Members((_, _, result) => new()
                {
                    Callback = Value<Func<int>>(() =>
                    {
                        int ReadValue() => result.Value;

                        return ReadValue();
                    })
                });

            builder.Map<Source, InvalidMembersResultAnonymousMethodDestination>()
                .Members((_, _, result) => new()
                {
                    Callback = Value<Func<int>>(
                        delegate { return result.Value; })
                });

            builder.Map<Source, ValidResolveSnapshotDestination>()
                .Resolve((_, previous) =>
                {
                    var hadPrevious = previous.HasValue;

                    return new(Value<Func<bool>>(
                        () => hadPrevious));
                });

            builder.Map<Source, ValidMembersSnapshotDestination>()
                .Members((_, _, result) =>
                {
                    var value = result.Value;

                    return new()
                    {
                        Callback = Value<Func<int>>(() => value)
                    };
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source();

            ExpectUnsupported<InvalidResolvePreviousDestination>(
                mapper,
                source);
            ExpectUnsupported<InvalidMembersPreviousDestination>(
                mapper,
                source);
            ExpectUnsupported<InvalidMembersResultReadDestination>(
                mapper,
                source);
            ExpectUnsupported<InvalidMembersResultCallDestination>(
                mapper,
                source);
            ExpectUnsupported<InvalidMembersResultMutationDestination>(
                mapper,
                source);
            ExpectUnsupported<InvalidMembersResultLocalFunctionDestination>(
                mapper,
                source);
            ExpectUnsupported<InvalidMembersResultAnonymousMethodDestination>(
                mapper,
                source);

            AssertResolveSnapshot(mapper, source);
            AssertMembersSnapshot(mapper, source);
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
                "A previous or result input escaped into a deferred callback.");
        }

        private static void AssertResolveSnapshot(
            TestMapper mapper,
            Source source)
        {
            var typeMapper =
                (ITypeMapper<Source, ValidResolveSnapshotDestination>)mapper;
            var created = typeMapper.Create(
                source,
                default(MappingContext));
            var updated = typeMapper.Update(
                source,
                created,
                default(MappingContext));

            if (created.Callback() || !updated.Callback())
            {
                throw new InvalidOperationException(
                    "A previous snapshot changed deferred semantics.");
            }
        }

        private static void AssertMembersSnapshot(
            TestMapper mapper,
            Source source)
        {
            var destination =
                ((ITypeMapper<Source, ValidMembersSnapshotDestination>)mapper)
                .Create(source, default(MappingContext));

            if (destination.Callback() != 7)
            {
                throw new InvalidOperationException(
                    "A result snapshot changed deferred semantics.");
            }
        }
    }
}
