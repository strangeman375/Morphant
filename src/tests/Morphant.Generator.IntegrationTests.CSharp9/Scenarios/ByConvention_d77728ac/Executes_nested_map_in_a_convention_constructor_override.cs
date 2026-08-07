// Compiled integration scenario: TypeMapperNestedMapTests/ByConventionTests::Executes_nested_map_in_a_convention_constructor_override
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9;
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
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(
                outer);
            provider.Add<ITypeMapper<ChildSource, ChildDestination>>(
                new ChildMapper());
            var mapper = new Mapper(provider);
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
