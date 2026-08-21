using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class BasePlanReachabilityTests
{
    [Test]
    public void Throw_makes_an_interface_base_plan_unreachable()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace TestCase
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public sealed class Dog : IDog { }
    public sealed class Unknown : IAnimal { }
    public interface IAnimalDto { }
    public sealed class DogDto : IAnimalDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
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
""";

        var result = GeneratorTestDriver.Run(
            "RuntimePolymorphismUnreachableBase",
            source,
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });

        GeneratedCodeExecution.AssertScenario(
            "runtime polymorphism unreachable base",
            result.OutputCompilation,
            "TestCase.Scenario");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Requires_a_base_plan_when_fallback_or_an_exact_source_is_possible(
        bool concreteBase)
    {
        var baseDeclaration = concreteBase
            ? "public class Animal { }"
            : "public interface Animal { }";
        const string dogDeclaration = "public sealed class Dog : Animal { }";
        var handling = concreteBase
            ? ".UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Throw)"
            : string.Empty;
        // lang=c#
        var source =
$$"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    {{baseDeclaration}}
    {{dogDeclaration}}
    public interface IAnimalDto { }
    public sealed class DogDto : IAnimalDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, IAnimalDto>()
                .ForDerived<Dog, DogDto>()
                {{handling}};
            builder.Map<Dog, DogDto>()
                .Convert(_ => new DogDto());
        }
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "RuntimePolymorphismReachableBase" + concreteBase,
            source,
            LanguageVersion.CSharp9);

        Assert.That(
            result.EffectiveDiagnostics.Select(
                static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0035" }));
    }
}
