// Compiled integration scenario: an unreachable interface base plan
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismUnreachableBase_b82d0006
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public sealed class Dog : IDog { }
    public sealed class Unknown : IAnimal { }
    public interface IAnimalDto { }
    public sealed class DogDto : IAnimalDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IAnimal, IAnimalDto>()
                .ForDerived<IDog, DogDto>()
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw);
            builder.Map<IDog, DogDto>()
                .Convert(_ => new DogDto());
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<IAnimal, IAnimalDto>)new TestMapper();

            if (mapper.Create(new Dog()) is not DogDto)
            {
                throw new InvalidOperationException(
                    "The known branch was not selected.");
            }

            try
            {
                mapper.Create(new Unknown());
                throw new InvalidOperationException(
                    "The unknown branch reached the base plan.");
            }
            catch (UnmatchedPolymorphicMappingException)
            {
            }
        }
    }
}
