// Compiled integration scenario: TypeMapperInheritanceTests/PlanCompositionTests::Merges_included_Members_by_destination_member_and_rebuilds_dependencies
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_19641a70
{
    public class Animal
    {
        public string Name { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public string Kept { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
        public string Breed { get; init; } = string.Empty;
    }

    public class AnimalDto
    {
        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Kept { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
        public string Breed { get; set; } = string.Empty;

        public string Extra { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected static string ObsoleteName(Animal source) =>
            throw new InvalidOperationException(
                "An overridden dependency was evaluated.");

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = ObsoleteName(source),
                    Code = "base:" + source.Code,
                    Kept = "base:" + source.Kept
                });
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .Members((source, _, result) => new()
                {
                    Name = "dog:" + source.Name,
                    Code = Ignore(),
                    Breed = source.Breed,
                    Extra = result.Name + ":extra"
                })
                .IncludeBase<Animal, AnimalDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Dog, DogDto>)new DogMapper();
            var result = mapper.Create(
                new Dog
                {
                    Name = "name",
                    Code = "code",
                    Kept = "kept",
                    Breed = "breed"
                },
                default);

            if (result.Name != "dog:name" ||
                result.Code != string.Empty ||
                result.Kept != "base:kept" ||
                result.Breed != "breed" ||
                result.Extra != "dog:name:extra")
            {
                throw new InvalidOperationException(
                    "The effective Members plan was composed incorrectly.");
            }

            try
            {
                mapper.Create(null, default);
            }
            catch (ArgumentNullException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Included pair settings were not inherited.");
        }
    }
}
