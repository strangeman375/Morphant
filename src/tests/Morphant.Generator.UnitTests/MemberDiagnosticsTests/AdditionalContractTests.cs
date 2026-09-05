using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

[TestFixture]
internal sealed class AdditionalContractTests
{
    [Test]
    public void Accepts_exact_source_types_and_effective_input_nullability()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System.Diagnostics.CodeAnalysis;
using Morphant;

namespace TestCase
{
    public readonly struct Output { }
    public sealed class Source { }

    public sealed class Destination
    {
        public Output Value { get; set; }

        [AllowNull]
        public string Text { get; set; } = string.Empty;

        [DisallowNull]
        public string? Strict { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Value = Value<Output>(new Output()),
                    Text = Value<string?>(null),
                    Strict = Value<string>(string.Empty)
                });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.MemberDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_typed_Auto_and_Ignore_mismatches_after_binding()
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

    public sealed class Destination
    {
        public object Automatic { get; set; } = new();
        public object Ignored { get; set; } = new();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Automatic = (object)Auto<int>(),
                    Ignored = (object)Ignore<int>()
                });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.MemberDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0040", "MORPH0040" }));
            Assert.That(
                result.MemberDiagnostics.Select(diagnostic =>
                    MemberDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "Auto", "Ignore" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_required_member_only_for_the_omitting_branch()
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
        public bool Assign { get; init; }
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public required int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => source.Assign
                    ? new() { Value = source.Value }
                    : new());
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(
            source,
            LanguageVersion.CSharp11);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0041"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith(
                    "Create; Update without an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Aggregates_ResolveUsing_init_failure_across_all_paths()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { public int Value { get; init; } }
    public sealed class Destination { public int Value { get; init; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ResolveUsing((source, previous) => new Destination())
                .Members(source => new() { Value = source.Value });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0042"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith(
                    "Create; Update without an existing destination; " +
                    "Update with an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_the_null_plan_producer_and_alias_use()
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
    public sealed class Destination { public int Value { get; set; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source =>
                {
                    global::Morphant.Generated.Types.A_MemberDiagnosticsConsumer.N_TestCase.Plans.DestinationMembers
                        plan = default!;
                    return plan;
                });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0043"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("default"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MemberDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "plan" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
