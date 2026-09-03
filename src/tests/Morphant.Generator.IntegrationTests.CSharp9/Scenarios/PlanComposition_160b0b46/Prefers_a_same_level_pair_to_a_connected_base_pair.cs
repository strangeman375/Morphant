// Compiled integration scenario: TypeMapperInheritanceTests/PlanCompositionTests::Prefers_a_same_level_pair_to_a_connected_base_pair
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_160b0b46
{
    public class Animal
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
    }

    public class AnimalDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
    }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = "base:" + source.Name
                });
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper<DogMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = "current:" + source.Name
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog { Name = "name" },
                    default);

            if (result.Name != "current:name")
            {
                throw new InvalidOperationException(
                    "The connected base pair outranked the same-level pair.");
            }
        }
    }
}
