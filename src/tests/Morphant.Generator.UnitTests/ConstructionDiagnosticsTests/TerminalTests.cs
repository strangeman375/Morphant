namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class TerminalTests
{
    [Test]
    public void Tracks_previous_and_null_through_structured_local_aliases()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class APrevious { }
    public sealed class BNull { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, APrevious>()
                .Resolve((source, previous) =>
                {
                    var first = previous;
                    var second = first;
                    return second;
                });
            builder.Map<Source, BNull>()
                .Construct(source =>
                {
                    Morphant.Generated.BNullConstruction omitted = null!;
                    var alias = omitted;
                    return alias;
                });
        }
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.ConstructionDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0038", "MORPH0039" }));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    ConstructionDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "second", "null" }));
            Assert.That(
                diagnostics[0].AdditionalLocations.Select(
                    ConstructionDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "previous", "first" }));
            Assert.That(
                diagnostics[1].AdditionalLocations.Select(
                    ConstructionDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "omitted", "alias" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Resolve_reports_independent_previous_and_null_leaves_by_path()
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
        public bool Omit { get; init; }
    }

    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                    source.Omit ? null! : previous);
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.ConstructionDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0038", "MORPH0039" }));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    ConstructionDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "previous", "null" }));
            Assert.That(
                diagnostics[0].GetMessage(),
                Does.EndWith(
                    "Create; Update without an existing destination."));
            Assert.That(
                diagnostics[1].GetMessage(),
                Does.EndWith(
                    "Create; Update without an existing destination; Update " +
                    "with an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Guarded_previous_throw_and_runtime_null_are_valid_terminals()
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

    public sealed class GuardedDestination { }
    public sealed class RuntimeDestination { }
    public sealed class ThrowDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, GuardedDestination>()
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue)
                        return previous;

                    return new();
                });
            builder.Map<Source, RuntimeDestination>()
                .ConstructUsing(source => null!);
            builder.Map<Source, ThrowDestination>()
                .Construct(source => throw new InvalidOperationException());
        }
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.ConstructionDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
