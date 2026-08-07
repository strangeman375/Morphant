// Compiled integration scenario: TypeMapperInheritanceTests/PlanCompositionTests::Accepts_interface_base_pair_assignability
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_a0a2a071
{
    public interface IAnimal
    {
        string Name { get; }
    }

    public sealed class Dog : IAnimal
    {
        public string Name { get; init; } = string.Empty;
    }

    public interface IAnimalDto
    {
        string Name { get; set; }
    }

    public sealed class DogDto : IAnimalDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IAnimal, IAnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = "base:" + source.Name
                });
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<IAnimal, IAnimalDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog { Name = "name" },
                    default);

            if (result.Name != "base:name")
            {
                throw new InvalidOperationException(
                    "Interface base-pair composition was not applied.");
            }
        }
    }
}
