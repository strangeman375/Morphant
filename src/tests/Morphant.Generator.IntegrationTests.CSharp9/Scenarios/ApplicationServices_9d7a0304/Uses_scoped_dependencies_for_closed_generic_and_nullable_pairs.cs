// Compiled integration scenario: MapperDispatchTests/SuccessfulMappingTests::Uses_scoped_dependencies_for_closed_generic_and_nullable_pairs
#nullable enable
#pragma warning disable CS1591

using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationServices_9d7a0304
{
    public sealed record Box<T>(T Value);

    public sealed class ScopeLabel
    {
        private static int _nextId;

        public ScopeLabel()
        {
            Id = Interlocked.Increment(ref _nextId);
        }

        public int Id { get; }

        public string Format(int value) => Id + ":" + value;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        private readonly ScopeLabel _label;

        public TestMapper(ScopeLabel label)
        {
            _label = label;
        }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Box<int>, Box<string>>()
                .ConstructUsing(source =>
                    new Box<string>(_label.Format(source.Value)));

            builder.Map<int?, string?>()
                .Convert(source => source.HasValue
                    ? _label.Format(source.Value)
                    : null);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            using var provider = new ServiceCollection()
                .AddScoped<ScopeLabel>()
                .AddScoped<TestMapper>()
                .AddScoped<ITypeMapper<Box<int>, Box<string>>>(services =>
                    services.GetRequiredService<TestMapper>())
                .AddScoped<ITypeMapper<int?, string?>>(services =>
                    services.GetRequiredService<TestMapper>())
                .AddScoped<IMapper, Mapper>()
                .BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });

            using var firstScope = provider.CreateScope();
            using var secondScope = provider.CreateScope();

            var first = VerifyScope(firstScope.ServiceProvider, 7, 11);
            var repeated = VerifyScope(firstScope.ServiceProvider, 13, 17);
            var second = VerifyScope(secondScope.ServiceProvider, 19, 23);

            if (first != repeated || first == second)
            {
                throw new InvalidOperationException(
                    "Scoped mapper dependencies were not shared within one " +
                    "application scope and isolated between scopes.");
            }
        }

        private static int VerifyScope(
            IServiceProvider services,
            int boxedValue,
            int nullableValue)
        {
            var concrete = services.GetRequiredService<TestMapper>();
            var boxedContract = services.GetRequiredService<
                ITypeMapper<Box<int>, Box<string>>>();
            var nullableContract = services.GetRequiredService<
                ITypeMapper<int?, string?>>();
            var mapper = services.GetRequiredService<IMapper>();
            var label = services.GetRequiredService<ScopeLabel>();

            if (!ReferenceEquals(concrete, boxedContract) ||
                !ReferenceEquals(concrete, nullableContract))
            {
                throw new InvalidOperationException(
                    "Exact pair registrations did not resolve the same " +
                    "scoped generated mapper.");
            }

            var boxed = mapper.Map<Box<int>, Box<string>>(
                new Box<int>(boxedValue));
            var nullable = mapper.Map<int?, string?>(nullableValue);
            var nullValue = mapper.Map<int?, string?>(null);

            if (boxed.Value != label.Format(boxedValue) ||
                nullable != label.Format(nullableValue) ||
                nullValue is not null)
            {
                throw new InvalidOperationException(
                    "Closed generic or nullable exact-pair dispatch used the " +
                    "wrong scoped dependency.");
            }

            return label.Id;
        }
    }
}
