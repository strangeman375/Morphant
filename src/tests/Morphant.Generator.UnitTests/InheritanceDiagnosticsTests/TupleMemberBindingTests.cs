using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

[TestFixture]
internal sealed class TupleMemberBindingTests
{
    [Test]
    public void Callbacks_can_read_public_tuple_fields(
        [Values("Construct", "Members", "Convert")] string callback,
        [Values("Local", "Inherited", "GenericInherited")] string scope,
        [Values(false, true)] bool useItemNames)
    {
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;

namespace TestCase
{
    public sealed class Source<T> where T : class
    {
        public (T? Value, int Count) Data { get; init; }
    }

    __FAMILY__

    [MorphantMapper]
    public partial class TestMapper : __BASE__
    {
        protected override void Configure(MapperBuilder builder)
        {
            __CONFIGURATION__
        }
    }
}
""";
        const string family =
"""
    public abstract class Family<TMapper__PARAMETER__> : TypeMapper<TMapper>
        where TMapper : Family<TMapper__PARAMETER__>
        __CONSTRAINT__
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<__TYPE__>, (__TYPE__? Value, int Count)>()
                .__CALLBACK__;
    }
""";
        var expression = callback switch
        {
            "Construct" => "Construct(source => new(Value: source.Data.Value, Count: source.Data.Count))",
            "Members" => "Members(source => new() { Value = source.Data.Value, Count = source.Data.Count })",
            "Convert" => "Convert(source => (source!.Data.Value, source.Data.Count))",
            _ => throw new ArgumentOutOfRangeException(nameof(callback))
        };
        if (useItemNames)
            expression = expression.Replace("Data.Value", "Data.Item1")
                .Replace("Data.Count", "Data.Item2");

        var inherit = scope != "Local";
        var generic = scope == "GenericInherited";
        var baseType = inherit
            ? generic ? "Family<TestMapper, string>" : "Family<TestMapper>"
            : "TypeMapper<TestMapper>";
        var configuration = inherit
            ? "base.Configure(builder); builder.Map<Source<string>, (string? Value, int Count)>()" +
              ".IncludeBase<Source<string>, (string? Value, int Count)>();"
            : "builder.Map<Source<string>, (string? Value, int Count)>().__CALLBACK__;";
        var result = GeneratorTestDriver.Run(
            "InheritedTupleFields",
            source.Replace("__FAMILY__", inherit ? family : string.Empty)
                .Replace("__BASE__", baseType)
                .Replace("__PARAMETER__", generic ? ", T" : string.Empty)
                .Replace("__CONSTRAINT__", generic ? "where T : class" : string.Empty)
                .Replace("__TYPE__", generic ? "T" : "string")
                .Replace("__CONFIGURATION__", configuration)
                .Replace("__CALLBACK__", expression),
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Inherited_callbacks_can_read_nested_nullable_elements_of_a_long_tuple(
        [Values("Construct", "Members", "Convert")] string callback)
    {
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;

namespace TestCase
{
    public sealed class Source<T> where T : class
    {
        public (int A, int B, int C, int D, int E, int F, int G,
            (T? Value, int Count)? Tail) Data { get; init; }
    }

    public abstract class Family<TMapper, T> : TypeMapper<TMapper>
        where TMapper : Family<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, (T? Value, int Count)>()
                .__CALLBACK__;
    }

    [MorphantMapper]
    public partial class TestMapper : Family<TestMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<string>, (string? Value, int Count)>()
                .IncludeBase<Source<string>, (string? Value, int Count)>();
        }
    }
}
""";
        var expression = callback switch
        {
            "Construct" => "Construct(source => new(Value: source.Data.Tail?.Value, Count: source.Data.Item8?.Item2 ?? 0))",
            "Members" => "Members(source => new() { Value = source.Data.Tail?.Value, Count = source.Data.Item8?.Item2 ?? 0 })",
            "Convert" => "Convert(source => (source!.Data.Tail?.Value, source.Data.Item8?.Item2 ?? 0))",
            _ => throw new ArgumentOutOfRangeException(nameof(callback))
        };
        var result = GeneratorTestDriver.Run(
            "InheritedLongTupleFields",
            source.Replace("__CALLBACK__", expression),
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
