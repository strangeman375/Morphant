// Compiled integration scenario: TypeMapperNestedMapTests/AdaptiveTests::Uses_the_actual_replacement_member_for_adaptive_update
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Adaptive_9e31b559
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
