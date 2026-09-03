// Compiled integration scenario: polymorphic Update lifecycle
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismUpdate_b82d0007
{
    public class Animal { }
    public sealed class Dog : Animal
    {
        public string Name { get; init; } = string.Empty;
    }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class Vehicle { }
    public sealed class Car : Vehicle { }
    public class VehicleDto { }
    public sealed class CarDto : VehicleDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .Convert(_ => new AnimalDto());
            builder.Map<Dog, DogDto>()
                .NullDestinationHandling(NullDestinationHandling.Throw)
                .Members(source => new() { Name = source.Name });

            builder.Map<Vehicle, VehicleDto>()
                .ForDerived<Car, CarDto>()
                .Convert(_ => new VehicleDto());
            builder.Map<Car, CarDto>()
                .Convert((_, _) => new CarDto());
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var animalMapper =
                (ITypeMapper<Animal, AnimalDto>)mapper;
            var destination = new DogDto();
            var updated = animalMapper.Update(
                new Dog { Name = "Ada" },
                destination);

            if (!ReferenceEquals(updated, destination) ||
                destination.Name != "Ada")
            {
                throw new InvalidOperationException(
                    "Derived Update did not preserve destination identity.");
            }

            try
            {
                animalMapper.Update(new Dog(), null);
                throw new InvalidOperationException(
                    "The derived null-destination policy was bypassed.");
            }
            catch (NullDestinationException exception)
            {
                if (exception.SourceType != typeof(Dog) ||
                    exception.DestinationType != typeof(DogDto))
                {
                    throw new InvalidOperationException(
                        "The null failure belongs to the wrong pair.");
                }
            }

            var vehicleMapper =
                (ITypeMapper<Vehicle, VehicleDto>)mapper;
            var previous = new CarDto();
            var replacement = vehicleMapper.Update(new Car(), previous);

            if (replacement is not CarDto ||
                ReferenceEquals(replacement, previous))
            {
                throw new InvalidOperationException(
                    "The derived pair could not return a replacement.");
            }
        }
    }
}
