// Compiled integration scenario: TypeMapperNestedMapTests/SharingTests::Shares_equivalent_nested_calls_across_construction_and_members
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Sharing_9c1bfd0d
{
    public sealed record ChildSource(int Value);

    public sealed record ChildDestination(int Value);

    public sealed record OuterSource(ChildSource Child);

    public sealed class OuterDestination
    {
        public OuterDestination(ChildDestination constructorValue)
        {
            ConstructorValue = constructorValue;
        }

        public ChildDestination ConstructorValue { get; }

        public ChildDestination MemberValue { get; set; } = new(-1);

        public ChildDestination UpdateValue { get; set; } = new(-1);
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Construct(source => new(Create(source.Child)))
                .Members((source, _) => new()
                {
                    MemberValue =
                        Create<ChildDestination>(source.Child),
                    UpdateValue = Update(source.Child, null)
                });
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public int CreateCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public ChildDestination Create(
            ChildSource? source,
            MappingContext context)
        {
            CreateCalls++;
            return new ChildDestination(source!.Value + 10);
        }

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            UpdateCalls++;
            return new ChildDestination(source!.Value + 20);
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

            var result = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(new ChildSource(5)));

            if (!ReferenceEquals(
                    result.ConstructorValue,
                    result.MemberValue) ||
                result.ConstructorValue.Value != 15 ||
                result.UpdateValue.Value != 25 ||
                child.CreateCalls != 1 ||
                child.UpdateCalls != 1)
            {
                throw new InvalidOperationException(
                    "Nested semantic identity or operation identity is " +
                    "incorrect.");
            }
        }
    }
}
