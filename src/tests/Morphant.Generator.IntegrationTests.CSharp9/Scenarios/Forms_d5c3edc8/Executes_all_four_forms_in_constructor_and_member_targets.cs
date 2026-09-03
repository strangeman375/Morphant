// Compiled integration scenario: TypeMapperNestedMapTests/FormsTests::Executes_all_four_forms_in_constructor_and_member_targets
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Forms_d5c3edc8
{
    public sealed record ChildSource(int Value);

    public interface IChildDestination
    {
        int Value { get; }
    }

    public sealed record ChildDestination(int Value)
        : IChildDestination;

    public sealed record OuterSource(
        ChildSource First,
        ChildSource Second,
        ChildSource Third,
        ChildSource Fourth);

    public sealed class ConstructorDestination
    {
        public ConstructorDestination(
            IChildDestination first,
            IChildDestination second,
            ChildDestination third,
            IChildDestination fourth)
        {
            First = first;
            Second = second;
            Third = third;
            Fourth = fourth;
        }

        public IChildDestination First { get; }

        public IChildDestination Second { get; }

        public ChildDestination Third { get; }

        public IChildDestination Fourth { get; }
    }

    public sealed class MemberDestination
    {
        public IChildDestination First { get; set; } =
            new ChildDestination(-1);

        public IChildDestination Second { get; set; } =
            new ChildDestination(-1);

        public ChildDestination Third { get; set; } =
            new(-1);

        public IChildDestination Fourth { get; set; } =
            new ChildDestination(-1);
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper<OuterMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<OuterSource, ConstructorDestination>()
                .Resolve((source, previous) => new(
                    Create(source.First),
                    Create<ChildDestination>(source.Second),
                    Update(
                        source.Third,
                        previous.HasValue
                            ? previous.Value.Third
                            : null),
                    Update<ChildDestination>(
                        source.Fourth,
                        previous.HasValue
                            ? (ChildDestination?)previous.Value.Fourth
                            : null)));

            builder.Map<OuterSource, MemberDestination>()
                .Members((source, previous) => new()
                {
                    First = Create(source.First),
                    Second = Create<ChildDestination>(source.Second),
                    Third = Update(
                        source.Third,
                        previous.HasValue
                            ? previous.Value.Third
                            : null),
                    Fourth = Update<ChildDestination>(
                        source.Fourth,
                        previous.HasValue
                            ? (ChildDestination?)previous.Value.Fourth
                            : null)
                });
        }
    }

    public sealed record Call(
        string Pair,
        MappingOperation Operation,
        int? Previous);

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>,
        ITypeMapper<ChildSource, IChildDestination>
    {
        public List<Call> Calls { get; } = new();

        ChildDestination ITypeMapper<
            ChildSource,
            ChildDestination>.Create(
            ChildSource? source,
            MappingContext context)
        {
            Calls.Add(new("concrete", context.Operation, null));
            return new ChildDestination(source!.Value + 10);
        }

        ChildDestination ITypeMapper<
            ChildSource,
            ChildDestination>.Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            Calls.Add(new(
                "concrete",
                context.Operation,
                destination?.Value));
            return new ChildDestination(
                source!.Value + (destination?.Value ?? 20));
        }

        IChildDestination ITypeMapper<
            ChildSource,
            IChildDestination>.Create(
            ChildSource? source,
            MappingContext context)
        {
            Calls.Add(new("interface", context.Operation, null));
            return new ChildDestination(source!.Value + 100);
        }

        IChildDestination ITypeMapper<
            ChildSource,
            IChildDestination>.Update(
            ChildSource? source,
            IChildDestination? destination,
            MappingContext context)
        {
            Calls.Add(new(
                "interface",
                context.Operation,
                destination?.Value));
            return new ChildDestination(
                source!.Value + (destination?.Value ?? 200));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var child = new ChildMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<
                    OuterSource,
                    ConstructorDestination>>(outer)
                .AddSingleton<ITypeMapper<
                    OuterSource,
                    MemberDestination>>(outer)
                .AddSingleton<ITypeMapper<
                    ChildSource,
                    ChildDestination>>(child)
                .AddSingleton<ITypeMapper<
                    ChildSource,
                    IChildDestination>>(child)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
            var source = new OuterSource(
                new ChildSource(1),
                new ChildSource(2),
                new ChildSource(3),
                new ChildSource(4));

            var createdConstructor = mapper.Map<
                OuterSource,
                ConstructorDestination>(source);
            var previousConstructor = new ConstructorDestination(
                new ChildDestination(31),
                new ChildDestination(32),
                new ChildDestination(33),
                new ChildDestination(34));
            var updatedConstructor = mapper.Map(
                source,
                previousConstructor);
            var createdMembers = mapper.Map<
                OuterSource,
                MemberDestination>(source);
            var previousMembers = new MemberDestination
            {
                First = new ChildDestination(41),
                Second = new ChildDestination(42),
                Third = new ChildDestination(43),
                Fourth = new ChildDestination(44)
            };
            var updatedMembers = mapper.Map(source, previousMembers);

            AssertValues(createdConstructor, 101, 12, 23, 24);
            AssertValues(updatedConstructor, 101, 12, 36, 38);
            AssertValues(createdMembers, 101, 12, 23, 24);
            AssertValues(updatedMembers, 101, 12, 46, 48);

            if (ReferenceEquals(
                    previousConstructor,
                    updatedConstructor) ||
                !ReferenceEquals(previousMembers, updatedMembers))
            {
                throw new InvalidOperationException(
                    "The outer mapping used the wrong result identity.");
            }

            if (child.Calls.Count != 16)
            {
                throw new InvalidOperationException(
                    "The nested mappings were not each called once.");
            }

            for (var group = 0; group < 4; group++)
            {
                var offset = group * 4;

                AssertCall(
                    child.Calls[offset],
                    "interface",
                    MappingOperation.Create,
                    null);
                AssertCall(
                    child.Calls[offset + 1],
                    "concrete",
                    MappingOperation.Create,
                    null);
                AssertCall(
                    child.Calls[offset + 2],
                    "concrete",
                    MappingOperation.Update,
                    group is 1 ? 33 : group is 3 ? 43 : null);
                AssertCall(
                    child.Calls[offset + 3],
                    "concrete",
                    MappingOperation.Update,
                    group is 1 ? 34 : group is 3 ? 44 : null);
            }
        }

        private static void AssertValues(
            ConstructorDestination destination,
            int first,
            int second,
            int third,
            int fourth)
        {
            if (destination.First.Value != first ||
                destination.Second.Value != second ||
                destination.Third.Value != third ||
                destination.Fourth.Value != fourth)
            {
                throw new InvalidOperationException(
                    "Constructor nested values are incorrect.");
            }
        }

        private static void AssertValues(
            MemberDestination destination,
            int first,
            int second,
            int third,
            int fourth)
        {
            if (destination.First.Value != first ||
                destination.Second.Value != second ||
                destination.Third.Value != third ||
                destination.Fourth.Value != fourth)
            {
                throw new InvalidOperationException(
                    "Member nested values are incorrect.");
            }
        }

        private static void AssertCall(
            Call call,
            string pair,
            MappingOperation operation,
            int? previous)
        {
            if (call.Pair != pair ||
                call.Operation != operation ||
                call.Previous != previous)
            {
                throw new InvalidOperationException(
                    "A nested call used the wrong pair, operation, or " +
                    "destination.");
            }
        }
    }
}
