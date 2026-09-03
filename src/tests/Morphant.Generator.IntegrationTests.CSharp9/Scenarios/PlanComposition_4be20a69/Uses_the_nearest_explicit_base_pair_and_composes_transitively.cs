// Compiled integration scenario: TypeMapperInheritanceTests/PlanCompositionTests::Uses_the_nearest_explicit_base_pair_and_composes_transitively
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_4be20a69
{
    public class Entity
    {
        public string Id { get; init; } = string.Empty;
    }

    public class Animal : Entity
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
        public string Breed { get; init; } = string.Empty;
    }

    public class EntityDto
    {
        public string Id { get; set; } = string.Empty;
    }

    public class AnimalDto : EntityDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
        public string Breed { get; set; } = string.Empty;
    }

    public abstract class FarMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : FarMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Entity, EntityDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Id = "entity:" + source.Id
                });
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = "far:" + source.Name
                });
        }
    }

    public abstract class NearMapper<TMapper> : FarMapper<TMapper>
        where TMapper : NearMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Animal, AnimalDto>()
                .IncludeBase<Entity, EntityDto>()
                .Members((source, _) => new()
                {
                    Name = "near:" + source.Name
                });
        }
    }

    [MorphantMapper]
    public partial class DogMapper : NearMapper<DogMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Members((source, _) => new()
                {
                    Breed = "dog:" + source.Breed
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog
                    {
                        Id = "17",
                        Name = "name",
                        Breed = "breed"
                    },
                    default);

            if (result.Id != "entity:17" ||
                result.Name != "near:name" ||
                result.Breed != "dog:breed")
            {
                throw new InvalidOperationException(
                    "The nearest or transitive base pair was not composed.");
            }
        }
    }
}
