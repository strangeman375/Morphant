using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class AdaptiveTests
{
    [Test]
    public void Follows_outer_operation_in_all_four_forms_and_checks_generic_destination()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace TestCase
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
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Uses_the_actual_replacement_member_for_adaptive_update()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace TestCase
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record OuterSource(ChildSource Child);

    public sealed class OuterDestination
    {
        public OuterDestination(ChildDestination child)
        {
            Child = child;
        }

        public ChildDestination Child { get; set; }
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Construct((_, previous) => new(
                    previous.HasValue
                        ? new ChildDestination(40)
                        : new ChildDestination(0)))
                .Members((source, _) => new()
                {
                    Child = Map(source.Child)
                });
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public int? UpdateDestination { get; private set; }

        public ChildDestination Create(
            ChildSource? source,
            MappingContext context) =>
            new(source!.Value * 10);

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            UpdateDestination = destination?.Value;
            return new ChildDestination(
                source!.Value + destination!.Value);
        }
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
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(outer);
            provider.Add<ITypeMapper<ChildSource, ChildDestination>>(child);
            var mapper = new Mapper(provider);
            var previous = new OuterDestination(
                new ChildDestination(5));
            var result = mapper.Map(
                new OuterSource(new ChildSource(3)),
                previous);

            if (ReferenceEquals(previous, result) ||
                child.UpdateDestination != 40 ||
                result.Child.Value != 43)
            {
                throw new InvalidOperationException(
                    "Adaptive Update did not use the replacement member.");
            }
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
    public void Casts_a_broad_current_member_to_a_value_destination()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace TestCase
{
    public sealed record OuterSource(int Number);

    public sealed class OuterDestination
    {
        public object? Number { get; set; }
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Members((source, _) => new()
                {
                    Number = Map<int>(source.Number)
                });
    }

    public sealed class NumberMapper : ITypeMapper<int, int>
    {
        public int Create(int source, MappingContext context) =>
            source * 10;

        public int Update(
            int source,
            int destination,
            MappingContext context) =>
            source + destination;
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
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(outer);
            provider.Add<ITypeMapper<int, int>>(new NumberMapper());
            var mapper = new Mapper(provider);
            var created = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(2));
            var updated = mapper.Map(
                new OuterSource(3),
                new OuterDestination { Number = 7 });

            if (!Equals(created.Number, 20) ||
                !Equals(updated.Number, 10))
            {
                throw new InvalidOperationException(
                    "Adaptive value destination conversion is incorrect.");
            }
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
