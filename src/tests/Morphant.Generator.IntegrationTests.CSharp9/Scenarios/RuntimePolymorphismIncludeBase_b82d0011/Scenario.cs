// Compiled integration scenario: independent IncludeBase and ForDerived
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismIncludeBase_b82d0011
{
    public class Animal
    {
        public string Name { get; init; } = string.Empty;
    }
    public class Dog : Animal { }
    public sealed class ServiceDog : Dog { }
    public class AnimalDto
    {
        public string Name { get; set; } = string.Empty;
    }
    public class DogDto : AnimalDto { }
    public sealed class ServiceDogDto : DogDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<ServiceDog, ServiceDogDto>()
                .Members(source => new() { Name = source.Name });
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
            builder.Map<ServiceDog, ServiceDogDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var serviceDog = new ServiceDog { Name = "Ada" };
            var throughBase =
                ((ITypeMapper<Animal, AnimalDto>)mapper)
                    .Create(serviceDog);
            var throughDog =
                ((ITypeMapper<Dog, DogDto>)mapper)
                    .Create(serviceDog);

            if (throughBase is not ServiceDogDto { Name: "Ada" } ||
                throughDog.GetType() != typeof(DogDto) ||
                throughDog.Name != "Ada")
            {
                throw new InvalidOperationException(
                    "IncludeBase and ForDerived were coupled.");
            }
        }
    }
}
