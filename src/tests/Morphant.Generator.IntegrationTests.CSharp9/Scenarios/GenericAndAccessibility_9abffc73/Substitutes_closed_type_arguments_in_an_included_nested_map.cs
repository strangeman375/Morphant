// Compiled integration scenario: TypeMapperInheritanceTests/GenericAndAccessibilityTests::Substitutes_closed_type_arguments_in_an_included_nested_map
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_9abffc73
{
    public sealed class ChildSource<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class ChildDestination<T>
    {
        public T Value { get; set; } = default!;
    }

    public class OuterSource<T>
    {
        public ChildSource<T> Child { get; init; } = new();
    }

    public sealed class DogSource : OuterSource<int>
    {
    }

    public class OuterDestination<T>
    {
        public ChildDestination<T> Child { get; set; } = new();
    }

    public sealed class DogDestination : OuterDestination<int>
    {
    }

    public abstract class GenericBaseMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource<T>, ChildDestination<T>>();
            builder.Map<OuterSource<T>, OuterDestination<T>>()
                .Members((source, _) => new()
                {
                    Child = Create<ChildDestination<T>>(source.Child)
                });
        }
    }

    [MorphantMapper]
    public partial class ClosedMapper : GenericBaseMapper<int>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<ChildSource<int>, ChildDestination<int>>();
            builder.Map<DogSource, DogDestination>()
                .IncludeBase<OuterSource<int>, OuterDestination<int>>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var typeMapper = new ClosedMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<
                    ChildSource<int>,
                    ChildDestination<int>>>(typeMapper)
                .AddSingleton<ITypeMapper<
                    DogSource,
                    DogDestination>>(typeMapper)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
            var result = mapper.Map<
                DogSource,
                DogDestination>(
                new DogSource
                {
                    Child = new ChildSource<int> { Value = 17 }
                });

            if (result.Child.Value != 17)
            {
                throw new InvalidOperationException(
                    "Included nested-map types were not substituted.");
            }
        }
    }
}
