using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class DiagnosticsTests
{
    [Test]
    public void Reports_self_duplicate_and_incompatible_links_at_their_origins()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class Animal { }
    public sealed class Dog : Animal { }
    public sealed class Unrelated { }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto { }
    public sealed class UnrelatedDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .ForDerived<Dog, DogDto>()
                .ForDerived<Unrelated, UnrelatedDto>()
                .Convert(_ => new AnimalDto());
        }
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "RuntimePolymorphismDiagnostics",
            source,
            LanguageVersion.CSharp9);
        var diagnostics = result.EffectiveDiagnostics
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0052",
                    "MORPH0053",
                    "MORPH0054",
                    "MORPH0054"
                }));
            Assert.That(
                GeneratorTestDriver.GetSourceText(
                    diagnostics[0].Location),
                Is.EqualTo("Animal"));
            Assert.That(
                GeneratorTestDriver.GetSourceText(
                    diagnostics[1].Location),
                Is.EqualTo("ForDerived"));
            Assert.That(
                diagnostics[1].AdditionalLocations,
                Has.Count.EqualTo(1));
            Assert.That(
                GeneratorTestDriver.GetSourceText(
                    diagnostics[1].AdditionalLocations[0]),
                Is.EqualTo("ForDerived"));
            Assert.That(
                diagnostics.Skip(2).Select(diagnostic =>
                    GeneratorTestDriver.GetSourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "Unrelated", "UnrelatedDto" }));
            Assert.That(
                result.CompilerWarningsAndErrors.Select(
                    static diagnostic => diagnostic.Id),
                Does.Contain("CS0311"));
        });
    }

    [Test]
    public void Reports_file_local_links_inaccessible_from_generated_code()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class Animal { }
    public class AnimalDto { }
    file sealed class FileDog : Animal { }
    file sealed class FileDogDto : AnimalDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<FileDog, FileDogDto>()
                .Convert(_ => new AnimalDto());
        }
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "RuntimePolymorphismAccessibility",
            source,
            LanguageVersion.CSharp11);
        var diagnostics = result.EffectiveDiagnostics
            .Where(static diagnostic => diagnostic.Id == "MORPH0055")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Has.Length.EqualTo(2));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    GeneratorTestDriver.GetSourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "FileDog", "FileDogDto" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "ForDerived source type " +
                    "'TestCase.FileDog' is inaccessible " +
                    "from generated mapper 'TestCase.TestMapper'.",
                    "ForDerived destination type " +
                    "'TestCase.FileDogDto' is inaccessible " +
                    "from generated mapper 'TestCase.TestMapper'."
                }));
        });
    }

    [Test]
    public void Uses_the_standard_invalid_setting_diagnostic()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class Animal { }
    public sealed class Dog : Animal { }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .UnknownDerivedTypeHandling(
                    (UnknownDerivedTypeHandling)42)
                .Convert(_ => new AnimalDto());
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "RuntimePolymorphismInvalidSetting",
            source,
            LanguageVersion.CSharp9);
        var diagnostic = result.EffectiveDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0021"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Setting 'UnknownDerivedTypeHandling' must be a " +
                    "supported compile-time constant."));
            Assert.That(
                GeneratorTestDriver.GetSourceText(diagnostic.Location),
                Is.EqualTo("(UnknownDerivedTypeHandling)42"));
        });
    }

    [Test]
    public void Uses_the_standard_invalid_fluent_flow_diagnostic()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class Animal { }
    public sealed class Dog : Animal { }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var mapping = builder.Map<Animal, AnimalDto>();
            mapping.ForDerived<Dog, DogDto>();
        }
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "RuntimePolymorphismInvalidFlow",
            source,
            LanguageVersion.CSharp9);
        var diagnostic = result.EffectiveDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0018"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("cannot analyze configuration"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
