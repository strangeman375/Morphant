using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

[TestFixture]
internal sealed class TupleMemberBindingTests
{
    [Test]
    public void Callbacks_can_read_public_tuple_fields(
        [Values("Construct", "Members", "Convert")] string callback,
        [Values(false, true)] bool substituteTypeParameter,
        [Values(false, true)] bool inherit)
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

    public abstract class Family<TMapper, T> : TypeMapper<TMapper>
        where TMapper : Family<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<__TYPE__>, (__TYPE__? Value, int Count)>()
                .__CALLBACK__;
    }

    [MorphantMapper]
    public partial class TestMapper : Family<TestMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            __CONFIGURATION__
        }
    }
}
""";
        var expression = callback switch
        {
            "Construct" => "Construct(source => new(Value: source.Data.Value, Count: source.Data.Count))",
            "Members" => "Members(source => new() { Value = source.Data.Value, Count = source.Data.Count })",
            "Convert" => "Convert(source => (source!.Data.Value, source.Data.Count))",
            _ => throw new ArgumentOutOfRangeException(nameof(callback))
        };
        var configuration = inherit
            ? "base.Configure(builder); builder.Map<Source<string>, (string? Value, int Count)>()" +
              ".IncludeBase<Source<string>, (string? Value, int Count)>();"
            : "builder.Map<Source<string>, (string? Value, int Count)>().__CALLBACK__;";
        var result = GeneratorTestDriver.Run(
            "InheritedTupleFields",
            source.Replace("__TYPE__", substituteTypeParameter ? "T" : "string")
                .Replace("__CONFIGURATION__", configuration)
                .Replace("__CALLBACK__", expression),
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
