using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

[TestFixture]
internal sealed class RuleAndRequiredTests
{
    [Test]
    public void Reports_a_typed_Value_mismatch_that_passes_CSharp_binding()
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
        public object Value { get; set; } = new();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Value = (object)Value<int>(42)
                });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0040"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Value"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Rule for destination member 'Value' is invalid in " +
                    "mapping 'TestCase.Source -> TestCase.Destination': " +
                    "specified type 'int' does not match member type " +
                    "'object'."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Leaves_a_compiler_rejected_marker_mismatch_to_CSharp()
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
        public string Value { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new() { Value = Value<int>(42) });
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.MemberDiagnostics, Is.Empty);
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                result.CompilerWarningsAndErrors.Select(static diagnostic =>
                    diagnostic.Id),
                Does.Contain("CS0029"));
        });
    }

    [Test]
    public void Does_not_report_unwritten_or_successful_Auto_rules()
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

    public sealed class AutomaticDestination
    {
        public int Missing { get; set; }
    }

    public sealed class ExplicitDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AutomaticDestination>();
            builder.Map<Source, ExplicitDestination>()
                .Members(source => new() { Value = Auto() });
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
    public void Reports_an_imported_rule_hidden_by_a_derived_member()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class BaseSource
    {
        public int Value { get; init; }
    }

    public sealed class DerivedSource : BaseSource { }

    public class BaseDestination
    {
        public int Value { get; set; }
    }

    public sealed class DerivedDestination : BaseDestination
    {
        public new int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<BaseSource, BaseDestination>()
                .Members(source => new() { Value = source.Value });
            builder.Map<DerivedSource, DerivedDestination>()
                .IncludeBase<BaseSource, BaseDestination>();
        }
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.MemberDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0040"));
            Assert.That(
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("IncludeBase"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MemberDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Value", "Value" }));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Rule for destination member 'Value' is invalid in " +
                    "mapping 'TestCase.DerivedSource -> " +
                    "TestCase.DerivedDestination': IncludeBase rule for " +
                    "destination member 'TestCase.BaseDestination.Value', " +
                    "which is hidden by " +
                    "'TestCase.DerivedDestination.Value' " +
                    "in the current destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Local_same_name_rule_removes_an_imported_hidden_origin()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class BaseSource
    {
        public int Value { get; init; }
    }

    public sealed class DerivedSource : BaseSource { }

    public class BaseDestination
    {
        public int Value { get; set; }
    }

    public sealed class DerivedDestination : BaseDestination
    {
        public new int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<BaseSource, BaseDestination>()
                .Members(source => new() { Value = source.Value });
            builder.Map<DerivedSource, DerivedDestination>()
                .IncludeBase<BaseSource, BaseDestination>()
                .Members(source => new() { Value = source.Value + 1 });
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
    public void Explicit_Ignore_owns_the_required_member_failure()
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
        public required int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new() { Value = Ignore() });
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
                MemberDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Ignore"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void SetsRequiredMembers_removes_the_required_obligation()
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
    public sealed class Source { }

    public sealed class Destination
    {
        [SetsRequiredMembers]
        public Destination() => Value = 17;

        public required int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit);
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(
            source,
            LanguageVersion.CSharp11);

        Assert.Multiple(() =>
        {
            Assert.That(result.MemberDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
