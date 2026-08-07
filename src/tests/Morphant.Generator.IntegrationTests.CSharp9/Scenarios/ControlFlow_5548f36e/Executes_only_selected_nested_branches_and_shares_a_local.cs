// Compiled integration scenario: TypeMapperNestedMapTests/ControlFlowTests::Executes_only_selected_nested_branches_and_shares_a_local
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ControlFlow_5548f36e
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record OuterSource(
        int Mode,
        ChildSource First,
        ChildSource Second,
        ChildSource Third,
        ChildSource Fourth);

    public sealed class OuterDestination
    {
        public ChildDestination Child { get; set; } = new(-1);

        public ChildDestination Other { get; set; } = new(-1);
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Members((source, _) =>
                {
                    var shared =
                        Create<ChildDestination>(source.First);
                    var selected = source.Mode switch
                    {
                        0 => shared,
                        1 => Create<ChildDestination>(source.Second),
                        _ => Create<ChildDestination>(source.Third)
                    };

                    return new()
                    {
                        Child = selected,
                        Other = source.Mode == 0
                            ? shared
                            : Create<ChildDestination>(source.Fourth)
                    };
                });
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public List<int> Values { get; } = new();

        public ChildDestination Create(
            ChildSource? source,
            MappingContext context)
        {
            if (context.Operation != MappingOperation.Create)
            {
                throw new InvalidOperationException(
                    "A one-argument nested Map did not use Create.");
            }

            Values.Add(source!.Value);
            return new ChildDestination(source.Value * 10);
        }

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context) =>
            throw new InvalidOperationException(
                "The nested Update method was selected.");
    }

    public sealed class ManualServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service)
                ? service
                : null;

        public void Add<TService>(TService service)
            where TService : class =>
            _services[typeof(IEnumerable<TService>)] =
                new TService[] { service };
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var child = new ChildMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(
                outer);
            provider.Add<ITypeMapper<ChildSource, ChildDestination>>(
                child);
            var mapper = new Mapper(provider);
            var values = new[]
            {
                new ChildSource(1),
                new ChildSource(2),
                new ChildSource(3),
                new ChildSource(4)
            };

            var shared = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(
                    0,
                    values[0],
                    values[1],
                    values[2],
                    values[3]));

            if (!ReferenceEquals(shared.Child, shared.Other) ||
                shared.Child.Value != 10)
            {
                throw new InvalidOperationException(
                    "The declarative nested local was not shared.");
            }

            AssertCalls(child, 1);

            var second = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(
                    1,
                    values[0],
                    values[1],
                    values[2],
                    values[3]));

            if (second.Child.Value != 20 || second.Other.Value != 40)
            {
                throw new InvalidOperationException(
                    "The selected conditional values are incorrect.");
            }

            AssertCalls(child, 2, 4);

            var third = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(
                    2,
                    values[0],
                    values[1],
                    values[2],
                    values[3]));

            if (third.Child.Value != 30 || third.Other.Value != 40)
            {
                throw new InvalidOperationException(
                    "The selected switch values are incorrect.");
            }

            AssertCalls(child, 3, 4);
        }

        private static void AssertCalls(
            ChildMapper mapper,
            params int[] expected)
        {
            if (mapper.Values.Count != expected.Length)
            {
                throw new InvalidOperationException(
                    "An unselected nested branch was evaluated.");
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (mapper.Values[index] != expected[index])
                {
                    throw new InvalidOperationException(
                        "Nested branches ran in the wrong order.");
                }
            }

            mapper.Values.Clear();
        }
    }
}
