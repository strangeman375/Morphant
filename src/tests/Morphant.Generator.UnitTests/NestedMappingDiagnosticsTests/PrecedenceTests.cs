using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

[TestFixture]
internal sealed class PrecedenceTests
{
    [Test]
    public void Nested_failure_owns_a_derivative_required_obligation()
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
        public required string Text { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    Text = Map<int>(source.Value)
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(
            source,
            LanguageVersion.CSharp11);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0045" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Callback_grammar_owns_a_non_terminal_nested_marker()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }
    public sealed class Source
    {
        public ChildSource Child { get; } = new();
    }

    public sealed class Destination
    {
        public ChildDestination? Child { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Child = Consume(
                        Map<ChildDestination>(source.Child))
                });

        private static ChildDestination Consume(object value) => new();
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.NestedMappingDiagnostics, Is.Empty);
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0033" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_independent_result_and_explicit_destination_failures()
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
        public string Text { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Text = Update<int>(source.Value, new object())
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.NestedMappingDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0045", "MORPH0046" }));
            Assert.That(
                result.NestedMappingDiagnostics.Select(diagnostic =>
                    NestedMappingDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "int", "new object()" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Retains_one_exact_inherited_origin_diagnostic()
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
        public string Text { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper<BaseMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Text = Map<int>(source.Value)
                });
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase<Source, Destination>();
        }
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.NestedMappingDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0045" }));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    result.NestedMappingDiagnostics.Single().Location),
                Is.EqualTo("int"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Local_same_member_rule_discards_an_inherited_nested_origin()
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
        public string Text { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper<BaseMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Text = Map<int>(source.Value)
                });
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase<Source, Destination>()
                .Members(source => new() { Text = source.Value.ToString() });
        }
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.NestedMappingDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
