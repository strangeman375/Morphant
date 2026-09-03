// Compiled integration scenario: TypeMapperInheritanceTests/GenericAndAccessibilityTests::Treats_inaccessible_inherited_expressions_as_unsupported_plans
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0028

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_242822d4
{
    public class Animal
    {
        public string Value { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
    }

    public class PrivateAnimalDto
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class PrivateDogDto : PrivateAnimalDto
    {
    }

    public class BaseExpressionAnimalDto
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class BaseExpressionDogDto : BaseExpressionAnimalDto
    {
    }

    public abstract class MapperSupport : TypeMapper<MapperSupport>
    {
        protected string Decorate(string value) => "support:" + value;

        protected override void Configure(MapperBuilder builder)
        {
        }
    }

    public abstract class BaseMapper : MapperSupport
    {
        private static string Secret(string value) => "secret:" + value;

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, PrivateAnimalDto>()
                .Members((source, _) => new()
                {
                    Value = Secret(source.Value)
                });
            builder.Map<Animal, BaseExpressionAnimalDto>()
                .Members((source, _) => new()
                {
                    Value = base.Decorate(source.Value)
                });
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, PrivateDogDto>()
                .IncludeBase<Animal, PrivateAnimalDto>();
            builder.Map<Dog, BaseExpressionDogDto>()
                .IncludeBase<Animal, BaseExpressionAnimalDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DerivedMapper();
            var source = new Dog { Value = "value" };

            ExpectNotSupported(() =>
                ((ITypeMapper<Dog, PrivateDogDto>)mapper)
                    .Create(source, default));
            ExpectNotSupported(() =>
                ((ITypeMapper<Dog, BaseExpressionDogDto>)mapper)
                    .Create(source, default));
        }

        private static void ExpectNotSupported(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An inaccessible inherited plan was transferred.");
        }
    }
}
