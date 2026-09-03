namespace Morphant.Generator.UnitTests.CallbackDiagnosticsTests;

[TestFixture]
internal sealed class TransferFailureTests
{
    [Test]
    public void Does_not_treat_named_argument_labels_as_captured_parameters()
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
        public Destination(int seed) => Seed = seed;

        public int Seed { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(seed: source.Value));
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
    public void Does_not_capture_the_runtime_value_named_by_nameof()
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

    public sealed class Destination
    {
        public Destination(string value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var runtime = Environment.TickCount;

            builder.Map<Source, Destination>()
                .Construct(source => new(nameof(runtime)));
        }
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
    public void Reports_configuration_time_values_in_every_callback_family()
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

    public sealed class AConstruct
    {
        public AConstruct(int value) { }
    }

    public sealed class BConstructUsing
    {
        public BConstructUsing(int value) { }
    }

    public sealed class CConvert
    {
        public CConvert(int value) { }
    }

    public sealed class DMembers
    {
        public int Value { get; set; }
    }

    public sealed class EResolve
    {
        public EResolve(int value) { }
    }

    public sealed class FResolveUsing
    {
        public FResolveUsing(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var runtime = Environment.TickCount;

            builder.Map<Source, AConstruct>()
                .Construct(source => new(runtime));
            builder.Map<Source, BConstructUsing>()
                .ConstructUsing(source => new(runtime));
            builder.Map<Source, CConvert>()
                .Convert(source => new(runtime));
            builder.Map<Source, DMembers>()
                .Members(source => new() { Value = runtime });
            builder.Map<Source, EResolve>()
                .Resolve((source, previous) => new(runtime));
            builder.Map<Source, FResolveUsing>()
                .ResolveUsing((source, previous) => new(runtime));
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
                Is.EqualTo(Enumerable.Repeat("MORPH0030", 6)));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    CallbackDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.All.EqualTo("runtime"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage().Split(' ')[0]),
                Is.EqualTo(new[]
                {
                    "Construct",
                    "ConstructUsing",
                    "Convert",
                    "Members",
                    "Resolve",
                    "ResolveUsing"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.All.Contains(
                    "value 'runtime' is only available while Configure runs"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Is.All.EqualTo(1));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    CallbackDiagnosticsGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations[0])),
                Is.All.EqualTo("runtime"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Distinguishes_deferred_destination_and_context_inputs()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace TestCase
{
    public sealed class Source { }

    public sealed class APrevious
    {
        public APrevious(Func<bool> read) => Read = read;

        public Func<bool> Read { get; }
    }

    public sealed class BResult
    {
        public Func<int> Read { get; set; } = () => 0;

        public int Value { get; } = 1;
    }

    public sealed class CContext
    {
        public CContext(Func<MappingOperation> read) => Read = read;

        public Func<MappingOperation> Read { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, APrevious>()
                .Resolve((source, previous) => new(
                    Value<Func<bool>>(() => previous.HasValue)));
            builder.Map<Source, BResult>()
                .Members((source, previous, result) => new()
                {
                    Read = Value<Func<int>>(() => result.Value)
                });
            builder.Map<Source, CContext>()
                .Construct((source, context) => new(
                    Value<Func<MappingOperation>>(
                        () => context.Operation)));
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
                Is.EqualTo(new[]
                {
                    "MORPH0030",
                    "MORPH0030",
                    "MORPH0030"
                }));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    CallbackDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "previous",
                    "result",
                    "context"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains(
                    "'previous' cannot be used inside a nested lambda or " +
                    "local function"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains(
                    "'result' cannot be used inside a nested lambda or " +
                    "local function"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains(
                    "'context.Operation' cannot be used inside a nested " +
                    "lambda or local function"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
