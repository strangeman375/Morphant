namespace Morphant.Generator.UnitTests.CallbackDiagnosticsTests;

[TestFixture]
internal sealed class GrammarMutationAndMarkerTests
{
    [Test]
    public void Keeps_dead_slices_parenthesized_discards_and_terminal_locals_valid()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591, CS0162, CS0429

using System;
using Morphant;
using Morphant.Markers;

namespace TestCase
{
    public sealed class Source
    {
        public ChildSource Child { get; init; } = new();

        public int Probe => 1;

        public int Value { get; init; }
    }

    public sealed class ChildSource { }

    public sealed class ChildDestination { }

    public sealed class Destination
    {
        public ChildDestination Child { get; set; } = new();

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var unavailable = Environment.TickCount;

            builder.Map<ChildSource, ChildDestination>();
            builder.Map<Source, Destination>()
                .Members((source, previous, result) =>
                {
                    if (false)
                    {
                        result.Value++;
                        Observe(unavailable);
                    }

                    _ = (source.Probe);
                    var shared = Create<ChildDestination>(source.Child);
                    var selected = true
                        ? shared
                        : Create<ChildDestination>(source.Child);

                    return new()
                    {
                        Child = selected,
                        Value = false
                            ? Consume(Value<int>(source.Value))
                            : source.Value
                    };
                });
        }

        private static int Consume(ValueMarker<int> value) => 0;

        private static void Observe(int value) { }
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_each_outer_unsupported_structured_statement_once()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
#pragma warning disable CS0168

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source =>
                {
                    int missing;
                    var value = 0;
                    value = source.Value;
                    Observe(value);

                    for (var index = 0; index < 1; index++)
                    {
                        try
                        {
                            Observe(index);
                        }
                        finally
                        {
                        }
                    }

                    int Read() => source.Value;

                    return new() { Value = Read() };
                });

        private static void Observe(int value) { }
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0031", 5)));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    CallbackDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "missing",
                    "=",
                    "Observe(value)",
                    "for",
                    "Read"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    Message("uninitialized local"),
                    Message("assignment"),
                    Message("invocation statement"),
                    Message("for statement"),
                    Message("local function")
                }));
            Assert.That(
                diagnostics.Any(static diagnostic =>
                    diagnostic.GetMessage().Contains(
                        "try statement",
                        StringComparison.Ordinal)),
                Is.False,
                "The unsupported for statement owns its nested content.");
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_mutations_nested_in_otherwise_supported_expressions()
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
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source =>
                {
                    var value = 0;
                    return new()
                    {
                        Value = ++value +
                            (value += source.Value) +
                            Mutate(ref value)
                    };
                });

        private static int Mutate(ref int value) => value;
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0031", 3)));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    CallbackDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "++", "+=", "ref" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    Message("increment"),
                    Message("compound assignment"),
                    Message("ref argument")
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Traces_mutation_through_previous_result_aliases_and_ref()
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

    public sealed class APrevious
    {
        public APrevious(int value) => Value = value;

        public int Value { get; set; }
    }

    public sealed class BResultAlias
    {
        public int Value { get; set; }
    }

    public sealed class CResultRef
    {
        public int Value;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, APrevious>()
                .Resolve((source, previous) => new(
                    previous.Value.Value = source.Value));
            builder.Map<Source, BResultAlias>()
                .Members((source, previous, result) =>
                {
                    var alias = result;
                    return new() { Value = alias.Value++ };
                });
            builder.Map<Source, CResultRef>()
                .Members((source, previous, result) => new()
                {
                    Value = Mutate(ref result.Value, source.Value)
                });
        }

        private static int Mutate(ref int value, int replacement)
        {
            value = replacement;
            return replacement;
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
                Is.EqualTo(Enumerable.Repeat("MORPH0032", 3)));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    CallbackDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "=", "++", "ref" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    ReadOnlyMessage("previous", "APrevious"),
                    ReadOnlyMessage("result", "BResultAlias"),
                    ReadOnlyMessage("result", "CResultRef")
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Rejects_runtime_non_terminal_and_context_marker_values()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using Morphant.Markers;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class ANonTerminal
    {
        public ANonTerminal(int value) { }
    }

    public sealed class BContext
    {
        public BContext(MappingOperation operation) { }
    }

    public sealed class CValid
    {
        public CValid(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, object>()
                .Convert(source => (object)Auto<int>());
            builder.Map<Source, ANonTerminal>()
                .Construct(source => new(
                    Consume(Value<int>(source.Value))));
            builder.Map<Source, BContext>()
                .Construct((source, context) =>
                {
                    var marker = context;
                    return new(context.Operation);
                });
            builder.Map<Source, CValid>()
                .Construct(source => new(Value<int>(source.Value)));
        }

        private static int Consume(ValueMarker<int> marker) => 0;
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0033", 3)));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    CallbackDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "Value", "context", "Auto" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "'Value' cannot be used in this position within " +
                    "Construct for mapping 'TestCase.Source -> " +
                    "TestCase.ANonTerminal'.",
                    "'context' cannot be used in this position within " +
                    "Construct for mapping 'TestCase.Source -> " +
                    "TestCase.BContext'.",
                    "'Auto' cannot be used in this position within Convert " +
                    "for mapping 'TestCase.Source -> object'."
                }));
            Assert.That(
                diagnostics.Any(static diagnostic =>
                    diagnostic.GetMessage().Contains(
                        "CValid",
                        StringComparison.Ordinal)),
                Is.False);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static string Message(string syntax) =>
        "Members for mapping 'TestCase.Source -> TestCase.Destination' " +
        "contains unsupported syntax '" + syntax + "'.";

    private static string ReadOnlyMessage(
        string input,
        string destination) =>
        "'" + input + "' is read-only in mapping 'TestCase.Source -> " +
        "TestCase." + destination + "'.";
}
