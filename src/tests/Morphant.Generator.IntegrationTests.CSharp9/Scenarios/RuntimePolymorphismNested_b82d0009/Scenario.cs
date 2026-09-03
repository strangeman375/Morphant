// Compiled integration scenario: transitive and nested dispatch
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismNested_b82d0009
{
    public class Animal { }
    public class Dog : Animal { }
    public sealed class ServiceDog : Dog { }
    public class AnimalDto { }
    public class DogDto : AnimalDto { }
    public sealed class ServiceDogDto : DogDto { }
    public sealed class Holder
    {
        public Animal Animal { get; init; } = null!;
    }
    public sealed class HolderDto
    {
        public AnimalDto Animal { get; set; } = null!;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .Convert(_ => new AnimalDto());
            builder.Map<Dog, DogDto>()
                .ForDerived<ServiceDog, ServiceDogDto>()
                .Convert(_ => new DogDto());
            builder.Map<ServiceDog, ServiceDogDto>()
                .Convert(_ => new ServiceDogDto());
            builder.Map<Holder, HolderDto>()
                .Members(source => new()
                {
                    Animal = Map<AnimalDto>(source.Animal)
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var animalMapper =
                (ITypeMapper<Animal, AnimalDto>)mapper;
            var holderMapper =
                (ITypeMapper<Holder, HolderDto>)mapper;

            if (animalMapper.Create(new ServiceDog()) is not
                    ServiceDogDto ||
                holderMapper.Create(new Holder
                {
                    Animal = new ServiceDog()
                }).Animal is not ServiceDogDto)
            {
                throw new InvalidOperationException(
                    "Transitive or nested dispatch stopped early.");
            }
        }
    }
}
