// Compiled integration scenario: TypeMapperConvertTests/LifecycleTests::Applies_only_MappingMode_as_the_manual_operation_gate
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_7a235a44
{
    public sealed record Source(int Value);

    public sealed record CreateDestination(int Value);

    public sealed record UpdateDestination(int Value);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int CreateCalls { get; private set; }

        public static int UpdateCalls { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, CreateDestination>(MappingMode.Create)
                .Convert((source, _, _) =>
                {
                    CreateCalls++;
                    return new(source?.Value ?? -1);
                });

            builder.Map<Source, UpdateDestination>(MappingMode.Update)
                .Convert((source, previous, _) =>
                {
                    UpdateCalls++;
                    return previous.HasValue
                        ? previous.Value
                        : new UpdateDestination(source?.Value ?? -1);
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<
                    Source,
                    CreateDestination>>(generated)
                .AddSingleton<ITypeMapper<
                    Source,
                    UpdateDestination>>(generated)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
            var source = new Source(7);

            var created = mapper.Map<Source, CreateDestination>(source);
            ExpectNotSupported(() =>
                mapper.Map(source, new CreateDestination(1)));
            ExpectNotSupported(() =>
                mapper.Map<Source, UpdateDestination>(source));
            var createdByUpdate = mapper.Map<Source, UpdateDestination>(
                source,
                null);
            var previous = new UpdateDestination(9);
            var reused = mapper.Map(source, previous);

            if (created.Value != 7 ||
                createdByUpdate.Value != 7 ||
                !ReferenceEquals(previous, reused) ||
                TestMapper.CreateCalls != 1 ||
                TestMapper.UpdateCalls != 2)
            {
                throw new InvalidOperationException(
                    "MappingMode did not exclusively gate Convert.");
            }
        }

        private static void ExpectNotSupported(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingOperationNotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A disabled manual operation was executed.");
        }
    }
}
