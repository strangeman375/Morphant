namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

[TestFixture]
internal sealed class UpdateDestinationTests
{
    [Test]
    public void Accepts_warning_free_explicit_destinations_null_and_default()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class BaseDestination { }
    public sealed class DerivedDestination : BaseDestination { }
    public sealed class ChildSource { }
    public sealed class ChildDestination { }

    public readonly struct DestinationInput
    {
        public static implicit operator ChildDestination(
            DestinationInput value) => new();
    }

    public sealed class Source
    {
        public ChildSource Child { get; } = new();
        public DerivedDestination Derived { get; } = new();
        public DestinationInput Converted { get; }
        public int Number { get; set; }
    }

    public sealed class Destination
    {
        public BaseDestination? Reference { get; set; }
        public ChildDestination? NullReference { get; set; }
        public ChildDestination? UserDefined { get; set; }
        public int? NullValue { get; set; }
        public int DefaultValue { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    Reference = Update<BaseDestination>(
                        source.Child,
                        source.Derived),
                    NullReference = Update<ChildDestination>(
                        source.Child,
                        null),
                    UserDefined = Update<ChildDestination>(
                        source.Child,
                        source.Converted),
                    NullValue = Update<int?>(source.Number, null),
                    DefaultValue = Update<int>(source.Number, default)
                });
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

    [Test]
    public void Rejects_writable_and_foreign_standalone_member_proxies()
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

    public sealed class Target
    {
        public ChildDestination Writable { get; set; } = new();
        public ChildDestination ReadOnly { get; } = new();
    }

    public sealed class Foreign
    {
        public ChildDestination ReadOnly { get; } = new();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Target>()
                .Members((source, previous) =>
                {
                    var own = new global::TestCase.Morphant.Generated
                        .TargetMembers();
                    var foreign = new global::TestCase.Morphant.Generated
                        .ForeignMembers();
                    Update(source.Child, own.Writable);
                    Update(source.Child, foreign.ReadOnly);
                    return own;
                });
            builder.Map<Source, Foreign>()
                .MemberSelection(MemberSelection.Explicit);
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
                Is.EqualTo(new[] { "MORPH0046", "MORPH0046" }));
            Assert.That(
                result.NestedMappingDiagnostics.Select(diagnostic =>
                    NestedMappingDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "own.Writable",
                    "foreign.ReadOnly"
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
