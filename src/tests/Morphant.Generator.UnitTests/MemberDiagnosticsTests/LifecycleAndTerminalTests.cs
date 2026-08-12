namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

[TestFixture]
internal sealed class LifecycleAndTerminalTests
{
    [Test]
    public void Reports_a_creation_time_rule_that_depends_on_result()
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
        public int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, previous, result) => new()
                {
                    Value = result.Value + 1
                });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0042"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Value"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MemberDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "result" }));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "creation-time member rule depends on result before it " +
                    "is created"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Allows_result_dependent_ordinary_setters()
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
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, previous, result) => new()
                {
                    Value = result.Value + 1
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
    public void Reports_a_result_dependent_required_setter_as_lifecycle()
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
        public required int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, previous, result) => new()
                {
                    Value = result.Value + 1
                });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(
            source,
            Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp11);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.MemberDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0042" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Allows_Ignore_after_a_runtime_result_policy()
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
        public int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ResolveUsing((source, previous) => new Destination())
                .Members(source => new() { Value = Ignore() });
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
    public void Keeps_null_member_plan_control_flow_path_sensitive()
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
        public bool Invalid { get; init; }
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
                .Members(source => source.Invalid
                    ? default!
                    : new() { Value = source.Value });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0043"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith(
                    "Create, Update without a previous destination, Update " +
                    "with a previous destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Empty_and_absent_member_plans_are_valid()
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

    public sealed class EmptyPlanDestination
    {
        public int Value { get; set; }
    }

    public sealed class AbsentPlanDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, EmptyPlanDestination>()
                .Members(source => new());
            builder.Map<Source, AbsentPlanDestination>();
        }
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
    public void Keeps_runtime_result_member_leaves_independent()
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
        public bool UseValid { get; init; }
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
        public int Missing { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructUsing(source => new Destination())
                .Members(source => source.UseValid
                    ? new() { Value = source.Value }
                    : new() { Missing = Auto() });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.MemberDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0040" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Keeps_result_dependent_null_leaf_independent()
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

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ResolveUsing((source, previous) => new Destination())
                .Members((source, previous, result) => result.Value >= 0
                    ? new() { Value = source.Value }
                    : default!);
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.MemberDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0043" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_creation_time_rule_selected_by_result_condition()
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

    public sealed class Destination
    {
        public int Initial { get; init; }
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, previous, result) => result.Value >= 0
                    ? new() { Initial = source.Value }
                    : new() { Value = source.Value });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0042"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Initial"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MemberDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "result" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
