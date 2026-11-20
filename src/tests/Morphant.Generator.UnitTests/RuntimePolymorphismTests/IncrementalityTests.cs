using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class IncrementalityTests
{
    [Test]
    public void Actualizes_a_changed_link_and_restores_the_original_artifact()
    {
        // lang=c#
        const string dogSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class Animal { }
    public sealed class Dog : Animal { }
    public sealed class Cat : Animal { }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto { }
    public sealed class CatDto : AnimalDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .Convert(_ => new AnimalDto());
            builder.Map<Dog, DogDto>()
                .Convert(_ => new DogDto());
            builder.Map<Cat, CatDto>()
                .Convert(_ => new CatDto());
        }
    }
}
""";
        var catSource = dogSource.Replace(
            ".ForDerived<Dog, DogDto>()",
            ".ForDerived<Cat, CatDto>()",
            StringComparison.Ordinal);

        var dog = GeneratorTestDriver.Run(
            "RuntimePolymorphismIncrementality",
            dogSource,
            LanguageVersion.CSharp9);
        var cat = GeneratorTestDriver.Run(
            "RuntimePolymorphismIncrementality",
            catSource,
            LanguageVersion.CSharp9,
            driver: dog.Driver);
        var restored = GeneratorTestDriver.Run(
            "RuntimePolymorphismIncrementality",
            dogSource,
            LanguageVersion.CSharp9,
            driver: cat.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(dog.EffectiveDiagnostics, Is.Empty);
            Assert.That(cat.EffectiveDiagnostics, Is.Empty);
            Assert.That(restored.EffectiveDiagnostics, Is.Empty);
            Assert.That(cat.TypeMapperSource, Is.Not.EqualTo(
                dog.TypeMapperSource));
            Assert.That(restored.TypeMapperSource, Is.EqualTo(
                dog.TypeMapperSource));
        });
    }
}
