namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

[TestFixture]
internal sealed class IncludeBaseTests
{
    [Test]
    public void Reports_each_extra_IncludeBase_without_dependent_diagnostics()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class Animal
    {
    }

    public sealed class Dog : Animal
    {
    }

    public class AnimalDto
    {
    }

    public sealed class DogDto : AnimalDto
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>();
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .IncludeBase<Dog, AnimalDto>()
                .IncludeBase<Animal, DogDto>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0025", "MORPH0025" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.All.EqualTo(
                    "IncludeBase is configured more than once for mapping " +
                    "'TestCase.Dog -> TestCase.DogDto' in mapper " +
                    "'TestCase.TestMapper'."));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Has.All.EqualTo("IncludeBase"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Has.All.EqualTo(1));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations[0])),
                Has.All.EqualTo("IncludeBase"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Distinguishes_a_missing_pair_from_two_incompatible_relations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class Animal
    {
    }

    public class AnimalDto
    {
    }

    public sealed class UnrelatedSource
    {
    }

    public sealed class UnrelatedDestination
    {
    }

    [MorphantMapper]
    public partial class MissingMapper : TypeMapper<MissingMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<UnrelatedSource, UnrelatedDestination>()
                .IncludeBase<Animal, AnimalDto>();
    }

    [MorphantMapper]
    public partial class IncompatibleMapper : TypeMapper<IncompatibleMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>();
            builder.Map<UnrelatedSource, UnrelatedDestination>()
                .IncludeBase<Animal, AnimalDto>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0026",
                    "MORPH0027",
                    "MORPH0027"
                }));
            Assert.That(
                InheritanceDiagnosticsGeneratorTest.SourceText(
                    diagnostics[0].Location),
                Is.EqualTo("IncludeBase"));
            Assert.That(
                diagnostics.Skip(1).Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "Animal", "AnimalDto" }));
            Assert.That(
                diagnostics.Skip(1).Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations.Single())),
                Is.EqualTo(new[]
                {
                    "UnrelatedSource",
                    "UnrelatedDestination"
                }));
            Assert.That(
                diagnostics[1].GetMessage(),
                Does.StartWith(
                    "The source type 'TestCase.UnrelatedSource' is not " +
                    "compatible with included source type " +
                    "'TestCase.Animal'"));
            Assert.That(
                diagnostics[2].GetMessage(),
                Does.StartWith(
                    "The destination type " +
                    "'TestCase.UnrelatedDestination' is not compatible " +
                    "with included destination type " +
                    "'TestCase.AnimalDto'"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Leaves_a_registered_category_3_candidate_to_its_origin()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Dog
    {
    }

    public class AnimalDto
    {
    }

    public sealed class DogDto : AnimalDto
    {
    }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<T, AnimalDto>();
            builder.Map<Dog, DogDto>()
                .IncludeBase<T, AnimalDto>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0012" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
