// Compiled integration scenario: generic mapper substitution
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismGeneric_b82d0013
{
    public class Animal<T> { }
    public sealed class Dog<T> : Animal<T>
    {
        public T Value { get; init; } = default!;
    }
    public class AnimalDto<T> { }
    public sealed class DogDto<T> : AnimalDto<T>
    {
        public T Value { get; init; } = default!;
    }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal<T>, AnimalDto<T>>()
                .ForDerived<Dog<T>, DogDto<T>>()
                .Convert(_ => new AnimalDto<T>());
            builder.Map<Dog<T>, DogDto<T>>()
                .Convert(source => new DogDto<T>
                {
                    Value = source!.Value
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Animal<string>, AnimalDto<string>>)
                new TestMapper<string>();

            if (mapper.Create(new Dog<string>
                {
                    Value = "generic"
                }) is not DogDto<string> { Value: "generic" })
            {
                throw new InvalidOperationException(
                    "Generic ForDerived substitution failed.");
            }
        }
    }
}
