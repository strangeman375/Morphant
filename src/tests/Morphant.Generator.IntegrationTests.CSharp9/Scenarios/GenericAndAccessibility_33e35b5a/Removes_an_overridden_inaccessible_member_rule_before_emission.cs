// Compiled integration scenario: TypeMapperInheritanceTests/GenericAndAccessibilityTests::Removes_an_overridden_inaccessible_member_rule_before_emission
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_33e35b5a
{
    public class Animal
    {
        public string Name { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
    }

    public class AnimalDto
    {
        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
    }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        private static string Secret(string value) => "secret:" + value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = Secret(source.Name),
                    Code = "base:" + source.Code
                });
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper<DogMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Members((source, _) => new()
                {
                    Name = "dog:" + source.Name
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog { Name = "name", Code = "code" },
                    default);

            if (result.Name != "dog:name" || result.Code != "base:code")
            {
                throw new InvalidOperationException(
                    "The overridden inaccessible rule remained effective.");
            }
        }
    }
}
