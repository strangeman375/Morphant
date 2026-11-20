// Compiled integration scenario: MapperDispatchTests/SuccessfulMappingTests::Dispatches_create_and_update_to_the_exact_generated_pair
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationMapping_9d7a0101
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; init; }

        public MappingOperation Operation { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert((source, previous, context) => new Destination
                {
                    Value = (source?.Value ?? 0) +
                        (previous.HasValue ? previous.Value.Value : 0),
                    Operation = context.Operation
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<Source, Destination>>(generated)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true
                });
            var mapper = provider.GetRequiredService<IMapper>();
            var created = mapper.Map<Source, Destination>(new Source
            {
                Value = 4
            });
            var supplied = new Destination { Value = 7 };
            var updated = mapper.Map(
                new Source { Value = 5 },
                supplied);
            var updatedFromNull = mapper.Map<Source, Destination>(
                new Source { Value = 6 },
                null);

            if (created.Value != 4 ||
                created.Operation != MappingOperation.Create ||
                ReferenceEquals(updated, supplied) ||
                updated.Value != 12 ||
                updated.Operation != MappingOperation.Update ||
                updatedFromNull.Value != 6 ||
                updatedFromNull.Operation != MappingOperation.Update)
            {
                throw new InvalidOperationException(
                    "IMapper did not preserve the generated Create and " +
                    "Update contracts or the replacement result.");
            }
        }
    }
}
