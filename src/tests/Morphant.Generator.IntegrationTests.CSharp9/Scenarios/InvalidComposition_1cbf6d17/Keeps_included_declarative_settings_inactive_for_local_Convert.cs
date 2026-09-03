// Compiled integration scenario: TypeMapperInheritanceTests/InvalidCompositionTests::Keeps_included_declarative_settings_inactive_for_local_Convert
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidComposition_1cbf6d17
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
        public AnimalDto(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class DogDto : AnimalDto
    {
        public DogDto(int value) : base(value)
        {
        }
    }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .ConstructorSelection(ConstructorSelection.Explicit)
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Value));
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper<DogMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Convert((source, _, _) =>
                    new DogDto(source?.Value ?? -1));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog { Value = 17 },
                    default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "Included no-effect settings invalidated local Convert.");
            }
        }
    }
}
