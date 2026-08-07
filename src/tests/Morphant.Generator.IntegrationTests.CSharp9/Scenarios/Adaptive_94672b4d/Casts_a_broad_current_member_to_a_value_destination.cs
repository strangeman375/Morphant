// Compiled integration scenario: TypeMapperNestedMapTests/AdaptiveTests::Casts_a_broad_current_member_to_a_value_destination
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Adaptive_94672b4d
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

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<
                    OuterSource,
                    OuterDestination>>(outer)
                .AddSingleton<ITypeMapper<int, int>>(new NumberMapper())
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
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
