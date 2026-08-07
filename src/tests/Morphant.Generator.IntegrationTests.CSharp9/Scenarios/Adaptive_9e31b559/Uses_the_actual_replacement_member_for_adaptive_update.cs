// Compiled integration scenario: TypeMapperNestedMapTests/AdaptiveTests::Uses_the_actual_replacement_member_for_adaptive_update
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
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

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var child = new ChildMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<
                    OuterSource,
                    OuterDestination>>(outer)
                .AddSingleton<ITypeMapper<
                    ChildSource,
                    ChildDestination>>(child)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
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
