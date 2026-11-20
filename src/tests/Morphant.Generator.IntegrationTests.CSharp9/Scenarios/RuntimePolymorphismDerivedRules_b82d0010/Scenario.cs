// Compiled integration scenario: rules in a selected derived plan
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismDerivedRules_b82d0010
{
    public class Animal { }
    public sealed class Dog : Animal
    {
        public DogDetails Details { get; init; } = new();
    }
    public sealed class DogDetails
    {
        public string Name { get; init; } = string.Empty;
        public Customer Customer { get; init; } = new();
    }
    public sealed class Customer
    {
        public string Name { get; init; } = string.Empty;
    }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto
    {
        public string Name { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .Convert(_ => new AnimalDto());
            builder.Map<Dog, DogDto>()
                .IncludeMembers(source => source.Details);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Animal, AnimalDto>)new TestMapper();
            var result = mapper.Create(new Dog
            {
                Details = new DogDetails
                {
                    Name = "Ada",
                    Customer = new Customer { Name = "Lovelace" }
                }
            });

            if (result is not DogDto
                {
                    Name: "Ada",
                    CustomerName: "Lovelace"
                })
            {
                throw new InvalidOperationException(
                    "The selected derived plan skipped configuration rules.");
            }
        }
    }
}
