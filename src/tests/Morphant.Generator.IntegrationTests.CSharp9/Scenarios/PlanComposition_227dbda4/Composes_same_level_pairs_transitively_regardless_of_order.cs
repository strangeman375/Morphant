// Compiled integration scenario: TypeMapperInheritanceTests/PlanCompositionTests::Composes_same_level_pairs_transitively_regardless_of_order
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_227dbda4
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

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Members((source, _) => new()
                {
                    Breed = "dog:" + source.Breed
                });
            builder.Map<Animal, AnimalDto>()
                .IncludeBase<Entity, EntityDto>()
                .Members((source, _) => new()
                {
                    Name = "animal:" + source.Name
                });
            builder.Map<Entity, EntityDto>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Id = "entity:" + source.Id
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Dog, DogDto>)new TestMapper();
            var result = mapper.Create(
                new Dog
                {
                    Id = "17",
                    Name = "name",
                    Breed = "breed"
                },
                default);

            if (result.Id != "entity:17" ||
                result.Name != "animal:name" ||
                result.Breed != "dog:breed")
            {
                throw new InvalidOperationException(
                    "Same-level IncludeBase composition was incorrect.");
            }

            try
            {
                mapper.Create(null, default);
            }
            catch (global::Morphant.Exceptions.NullSourceException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Same-level pair settings were not inherited.");
        }
    }
}
