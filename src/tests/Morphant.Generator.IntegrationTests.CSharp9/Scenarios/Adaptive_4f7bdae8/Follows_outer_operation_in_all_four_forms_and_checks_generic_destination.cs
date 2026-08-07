// Compiled integration scenario: TypeMapperNestedMapTests/AdaptiveTests::Follows_outer_operation_in_all_four_forms_and_checks_generic_destination
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Adaptive_4f7bdae8
{
    public interface IChildDestination
    {
        int Value { get; }
    }

    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value) : IChildDestination;

    public sealed record OtherChildDestination(int Value) : IChildDestination;

    public sealed record OuterSource(
        ChildSource First,
        ChildSource Second,
        ChildSource Third,
        ChildSource Fourth);

    public sealed class ConstructorDestination
    {
        public ConstructorDestination(
            ChildDestination first,
            IChildDestination? second,
            ChildDestination third,
            ChildDestination fourth)
        {
            First = first;
            Second = second;
            Third = third;
            Fourth = fourth;
        }

        public ChildDestination First { get; }

        public IChildDestination? Second { get; }

        public ChildDestination Third { get; }

        public ChildDestination Fourth { get; }
    }

    public sealed class MemberDestination
    {
        public ChildDestination First { get; set; } = new(-1);

        public IChildDestination? Second { get; set; }

        public ChildDestination Third { get; set; } = new(-1);

        public ChildDestination Fourth { get; set; } = new(-1);
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<OuterSource, ConstructorDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct((source, _) =>
                {
                    var first = Map();
                    var second = Map<ChildDestination>();
                    var third = Map(source.Third);
                    var fourth = Map<ChildDestination>(source.Fourth);

                    return new(first, second, third, fourth);
                });

            builder.Map<OuterSource, MemberDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) =>
                {
                    var first = Map();
                    var second = Map<ChildDestination>();
                    var third = Map(source.Third);
                    var fourth = Map<ChildDestination>(source.Fourth);

                    return new()
                    {
                        First = first,
                        Second = second,
                        Third = third,
                        Fourth = fourth
                    };
                });
        }
    }

    public sealed record Call(
        MappingOperation Operation,
        int Source,
        int? Destination);

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public List<Call> Calls { get; } = new();

        public ChildDestination Create(
            ChildSource? source,
            MappingContext context)
        {
            Calls.Add(new Call(context.Operation, source!.Value, null));
            return new ChildDestination(source.Value * 10);
        }

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            Calls.Add(new Call(
                context.Operation,
                source!.Value,
                destination?.Value));
            return new ChildDestination(
                source.Value * 10 + (destination?.Value ?? 1000));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var child = new ChildMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, ConstructorDestination>>(
                outer);
            provider.Add<ITypeMapper<OuterSource, MemberDestination>>(
                outer);
            provider.Add<ITypeMapper<ChildSource, ChildDestination>>(child);
            var mapper = new Mapper(provider);
            var source = new OuterSource(
                new ChildSource(1),
                new ChildSource(2),
                new ChildSource(3),
                new ChildSource(4));

            var createdConstructor = mapper.Map<
                OuterSource,
                ConstructorDestination>(source);
            var createdMembers = mapper.Map<
                OuterSource,
                MemberDestination>(source);

            AssertValues(createdConstructor, 10, 20, 30, 40);
            AssertValues(createdMembers, 10, 20, 30, 40);
            AssertCalls(child, MappingOperation.Create, null, 8);

            var normalizedNullUpdate = mapper.Map(
                source,
                default(MemberDestination));

            AssertValues(normalizedNullUpdate, 10, 20, 30, 40);
            AssertCalls(child, MappingOperation.Create, null, 4);

            var previousConstructor = new ConstructorDestination(
                new ChildDestination(11),
                new ChildDestination(12),
                new ChildDestination(13),
                new ChildDestination(14));
            var previousMembers = new MemberDestination
            {
                First = new ChildDestination(21),
                Second = null,
                Third = new ChildDestination(23),
                Fourth = new ChildDestination(24)
            };
            var updatedConstructor = mapper.Map(
                source,
                previousConstructor);
            var updatedMembers = mapper.Map(source, previousMembers);

            AssertValues(updatedConstructor, 21, 32, 43, 54);
            AssertValues(updatedMembers, 31, 1020, 53, 64);
            AssertCalls(child, MappingOperation.Update, expected: null, 8);

            previousMembers.Second = new OtherChildDestination(9);

            try
            {
                mapper.Map(source, previousMembers);
                throw new InvalidOperationException(
                    "An incompatible generic destination was accepted.");
            }
            catch (InvalidCastException)
            {
            }
        }

        private static void AssertValues(
            ConstructorDestination value,
            int first,
            int second,
            int third,
            int fourth)
        {
            if (value.First.Value != first ||
                value.Second?.Value != second ||
                value.Third.Value != third ||
                value.Fourth.Value != fourth)
            {
                throw new InvalidOperationException(
                    "Adaptive constructor values are incorrect.");
            }
        }

        private static void AssertValues(
            MemberDestination value,
            int first,
            int second,
            int third,
            int fourth)
        {
            if (value.First.Value != first ||
                value.Second?.Value != second ||
                value.Third.Value != third ||
                value.Fourth.Value != fourth)
            {
                throw new InvalidOperationException(
                    "Adaptive member values are incorrect.");
            }
        }

        private static void AssertCalls(
            ChildMapper mapper,
            MappingOperation operation,
            int? expected,
            int count)
        {
            if (mapper.Calls.Count != count)
            {
                throw new InvalidOperationException(
                    "Adaptive nested mappings were not called once each.");
            }

            foreach (var call in mapper.Calls)
            {
                if (call.Operation != operation ||
                    operation == MappingOperation.Create &&
                    call.Destination != expected)
                {
                    throw new InvalidOperationException(
                        "Adaptive nested mapping selected the wrong operation.");
                }
            }

            mapper.Calls.Clear();
        }
    }
}
