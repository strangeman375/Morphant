using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.CallbackDiagnosticsTests;

[TestFixture]
internal sealed class BindingFailureTests
{
    [Test]
    public void Keeps_a_source_visible_warning_compiler_owned()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert(source => Build(source));

        [Obsolete("intentional source warning")]
        private static Destination Build(Source? source) => new();
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                result.CompilerWarningsAndErrors.Select(
                    static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "CS0618" }));
        });
    }

    [Test]
    public void Reports_file_local_symbols_at_the_effective_reference()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public Destination(int value) { }
    }

    file static class HiddenHelper
    {
        public static int Read(int value) => value;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(
                    HiddenHelper.Read(source.Value)));
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(
            source,
            LanguageVersion.CSharp11);
        var diagnostic = result.EffectiveDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0030"));
            Assert.That(
                CallbackDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Read"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("file-local symbol"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith("is inaccessible."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Distinguishes_extension_method_group_and_custom_query_binding()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using TestCase.QueryApi;

namespace TestCase.QueryApi
{
    public sealed class Sequence<T> { }

    public static class QueryOperators
    {
        public static int Measure(this string value) => value.Length;

        public static Sequence<T> Where<T>(
            this Sequence<T> source,
            Func<T, bool> predicate) => source;

        public static Sequence<TResult> Select<T, TResult>(
            this Sequence<T> source,
            Func<T, TResult> selector) => new();
    }
}

namespace TestCase
{
    public sealed class Source
    {
        public string Text { get; set; } = string.Empty;

        public Sequence<int> Values { get; set; } = new();
    }

    public sealed class AMethodGroup
    {
        public AMethodGroup(Func<int> read) { }
    }

    public sealed class BQuery
    {
        public BQuery(Sequence<int> values) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AMethodGroup>()
                .Construct(source => new(
                    Value<Func<int>>(source.Text.Measure)));
            builder.Map<Source, BQuery>()
                .Construct(source => new(
                    from value in source.Values
                    where value > 0
                    select value));
        }
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0030", "MORPH0030" }));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    CallbackDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "Measure", "from" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains(
                    "extension method group is not supported"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains(
                    "custom query pattern is not supported"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
