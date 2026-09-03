// Compiled integration scenario: TypeMapperInheritanceTests/GenericAndAccessibilityTests::Does_not_transfer_an_inaccessible_Construct_plan
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_d13d9d85
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
        public AnimalDto(string value) => Value = value;

        public string Value { get; }
    }

    public sealed class DogDto : AnimalDto
    {
        public DogDto(string value) : base(value)
        {
        }
    }

    public abstract class BaseMapper : TypeMapper<BaseMapper>
    {
        private static string Secret(int value) => "secret:" + value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .Construct(source => new(Secret(source.Value)));
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Construct(source => new("current:" + source.Value));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DerivedMapper())
                    .Create(new Dog { Value = 17 }, default);

            if (result.Value != "current:17")
            {
                throw new InvalidOperationException(
                    "The inaccessible base Construct remained effective.");
            }
        }
    }
}
