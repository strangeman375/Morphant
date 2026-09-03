// Compiled integration scenario: TypeMapperInheritanceTests/PlanCompositionTests::Does_not_include_Convert_and_local_Convert_replaces_included_Members
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_241e949d
{
    public class Animal
    {
        public int Value { get; init; }
    }

    public sealed class Dog : Animal
    {
    }

    public class AnimalDto
    {
        public string Kind { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
    }

    public class Vehicle
    {
        public int Value { get; init; }
    }

    public sealed class Car : Vehicle
    {
    }

    public class VehicleDto
    {
        public string Kind { get; set; } = string.Empty;
    }

    public sealed class CarDto : VehicleDto
    {
    }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Convert((source, _, _) => new AnimalDto
                {
                    Kind = "animal:" + source!.Value
                });
            builder.Map<Vehicle, VehicleDto>()
                .Members((source, _) => new()
                {
                    Kind = "vehicle:" + source.Value
                });
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper<DerivedMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Members((source, _) => new()
                {
                    Kind = "dog:" + source.Value
                });
            builder.Map<Car, CarDto>()
                .IncludeBase<Vehicle, VehicleDto>()
                .Convert((source, _, _) => new CarDto
                {
                    Kind = "car:" + source!.Value
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DerivedMapper();
            var dog = ((ITypeMapper<Dog, DogDto>)mapper).Create(
                new Dog { Value = 17 },
                default);
            var car = ((ITypeMapper<Car, CarDto>)mapper).Create(
                new Car { Value = 31 },
                default);

            if (dog.Kind != "dog:17" || car.Kind != "car:31")
            {
                throw new InvalidOperationException(
                    "Convert crossed the IncludeBase boundary.");
            }
        }
    }
}
