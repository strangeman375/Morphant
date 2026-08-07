// Compiled integration scenario: TypeMapperInheritanceTests/PlanCompositionTests::Does_not_include_Construct_and_recomputes_derived_construction
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_07647072
{
    public class Animal
    {
        public int Seed { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
        public string Breed { get; init; } = string.Empty;
    }

    public class AnimalDto
    {
        public AnimalDto(int seed) => Seed = seed;

        public int Seed { get; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
        public DogDto(int seed) : base(seed)
        {
        }

        public string Breed { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper
    {
        private static AnimalDto CreateBase(int seed) => new(seed + 1000);

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .Construct(source => new(ByFactory(() =>
                    CreateBase(source.Seed))))
                .Members((source, _) => new()
                {
                    Name = "base:" + source.Name
                });
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper
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
                        Seed = 17,
                        Name = "name",
                        Breed = "breed"
                    },
                    default);

            if (result.Seed != 17 ||
                result.Name != "base:name" ||
                result.Breed != "dog:breed")
            {
                throw new InvalidOperationException(
                    "Construct was included or derived construction was not recomputed.");
            }
        }
    }
}
