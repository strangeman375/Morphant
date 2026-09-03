using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class TupleBrokenEditRecoveryActualizationTests
{
    [Test]
    public void Rebuilds_tuple_artifacts_after_a_temporarily_broken_tuple_type()
    {
        var initial = GeneratorTestDriver.Run(
            "TupleBrokenEdit",
            BuildSource("(int Id, string Name)"),
            LanguageVersion.CSharp9);
        var broken = GeneratorTestDriver.Run(
            "TupleBrokenEdit",
            BrokenSource,
            LanguageVersion.CSharp9,
            driver: initial.Driver);
        var recovered = GeneratorTestDriver.Run(
            "TupleBrokenEdit",
            BuildSource("(int Code, string Name)"),
            LanguageVersion.CSharp9,
            driver: broken.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(initial.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(broken.CompilerWarningsAndErrors, Is.Not.Empty);
            Assert.That(
                broken.Diagnostics.Any(diagnostic =>
                    diagnostic.Id == "MORPH0057"),
                Is.False);
            Assert.That(recovered.Diagnostics, Is.Empty);
            Assert.That(recovered.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(recovered.TypeMapperSource, Does.Contain(".Code"));
        });
    }

    [Test]
    public void Keeps_tuple_symbols_aligned_with_recreated_compilation_references()
    {
        var firstReference = CreateCompilationReference(
            "ExternalTupleModels",
            BuildReferenceSource(1));
        var recreatedReference = CreateCompilationReference(
            "ExternalTupleModels",
            BuildReferenceSource(2));
        var initial = GeneratorTestDriver.Run(
            "TupleCompilationReference",
            CompilationReferenceConsumer,
            LanguageVersion.CSharp9,
            additionalReferences: [firstReference]);
        var recreated = GeneratorTestDriver.Run(
            "TupleCompilationReference",
            CompilationReferenceConsumer,
            LanguageVersion.CSharp9,
            driver: initial.Driver,
            additionalReferences: [recreatedReference]);

        Assert.Multiple(() =>
        {
            Assert.That(initial.Diagnostics, Is.Empty);
            Assert.That(initial.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(recreated.Diagnostics, Is.Empty);
            Assert.That(recreated.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(
                recreated.TypeMapperSource,
                Does.Contain("global::ExternalTupleModels.Value"));
        });
    }

    private static string BuildSource(string tupleType)
    {
        return SourceTemplate.Replace("__TUPLE_TYPE__", tupleType);
    }

    private static string BuildReferenceSource(int unrelatedValue)
    {
        return ReferenceSourceTemplate.Replace(
            "__UNRELATED_VALUE__",
            unrelatedValue.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
    }

    // lang=c#
    private const string SourceTemplate =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Id { get; init; }

        public int Code { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, __TUPLE_TYPE__>();
    }
}
""";

    // lang=c#
    private const string BrokenSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Id { get; init; }

        public int Code { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int Id, string Name>();
    }
}
""";

    // lang=c#
    private const string ReferenceSourceTemplate =
"""
#nullable enable
#pragma warning disable CS1591

namespace ExternalTupleModels
{
    public sealed class Value { }

    public static class Unrelated
    {
        public const int Value = __UNRELATED_VALUE__;
    }
}
""";

    // lang=c#
    private const string CompilationReferenceConsumer =
"""
#nullable enable
#pragma warning disable CS1591

using ExternalTupleModels;
using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public Value Item { get; init; } = new Value();

        public int Count { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (Value Item, int Count)>();
    }
}
""";
}
