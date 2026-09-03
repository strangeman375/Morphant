// Compiled integration scenario: exact IncludeBase composition
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismExactIncludeBase_b82d0012
{
    public class Animal { }
    public sealed class Dog : Animal { }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto { }

    public abstract class BaseMapper : TypeMapper<BaseMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .Convert(_ => new AnimalDto());
            builder.Map<Dog, DogDto>()
                .Convert(_ => new DogDto());
        }
    }

    [MorphantMapper]
    public partial class TestMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Animal, AnimalDto>()
                .IncludeBase<Animal, AnimalDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Animal, AnimalDto>)new TestMapper())
                    .Create(new Dog());

            if (result.GetType() != typeof(AnimalDto))
            {
                throw new InvalidOperationException(
                    "Exact IncludeBase imported a ForDerived link.");
            }
        }
    }
}
