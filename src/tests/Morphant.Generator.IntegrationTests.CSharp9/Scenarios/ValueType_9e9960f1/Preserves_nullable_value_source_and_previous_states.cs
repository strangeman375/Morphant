// Compiled integration scenario: TypeMapperConvertTests/ValueTypeTests::Preserves_nullable_value_source_and_previous_states
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ValueType_9e9960f1
{
    public sealed record Call(
        MappingOperation Operation,
        int? Source,
        bool PreviousHasValue);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static List<Call> Calls { get; } = new();

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int?, int?>()
                .Convert((source, previous, context) =>
                {
                    Calls.Add(new(
                        context.Operation,
                        source,
                        previous.HasValue));

                    if (!source.HasValue)
                    {
                        return previous.TryGetValue(out var destination)
                            ? destination
                            : null;
                    }

                    return previous.TryGetValue(out var value)
                        ? value + source.Value
                        : source.Value;
                });
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
            var generated = new TestMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<int?, int?>>(generated);
            var mapper = new Mapper(provider);
            var createdNull = mapper.Map<int?, int?>(null);
            var updatedNull = mapper.Map<int?, int?>(null, null);
            var updatedValue = mapper.Map<int?, int?>(3, 5);

            if (createdNull is not null ||
                updatedNull is not null ||
                updatedValue != 8 ||
                TestMapper.Calls.Count != 3 ||
                TestMapper.Calls[0] != new Call(
                    MappingOperation.Create,
                    null,
                    false) ||
                TestMapper.Calls[1] != new Call(
                    MappingOperation.Update,
                    null,
                    false) ||
                TestMapper.Calls[2] != new Call(
                    MappingOperation.Update,
                    3,
                    true))
            {
                throw new InvalidOperationException(
                    "Nullable value call state was changed.");
            }
        }
    }
}
