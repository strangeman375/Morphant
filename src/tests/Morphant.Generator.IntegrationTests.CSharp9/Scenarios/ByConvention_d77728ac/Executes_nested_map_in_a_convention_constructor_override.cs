// Compiled integration scenario: TypeMapperNestedMapTests/ByConventionTests::Executes_nested_map_in_a_convention_constructor_override
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_d77728ac
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record OuterSource(int Id, ChildSource Child);

    public sealed class OuterDestination
    {
        public OuterDestination(int id, ChildDestination child)
        {
            Id = id;
            Child = child;
        }

        public int Id { get; }

        public ChildDestination Child { get; }
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Construct(source => new(
                    ByConvention(),
                    new()
                    {
                        child = Create<ChildDestination>(source.Child)
                    }));
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public ChildDestination Create(
            ChildSource? source,
            MappingContext context) =>
            new(source!.Value + 1);

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context) =>
            throw new InvalidOperationException(
                "The nested Update method was selected.");
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
                .AddSingleton<ITypeMapper<
                    ChildSource,
                    ChildDestination>>(new ChildMapper())
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
            var result = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(7, new ChildSource(10)));

            if (result.Id != 7 || result.Child.Value != 11)
            {
                throw new InvalidOperationException(
                    "The convention constructor override was ignored.");
            }
        }
    }
}
