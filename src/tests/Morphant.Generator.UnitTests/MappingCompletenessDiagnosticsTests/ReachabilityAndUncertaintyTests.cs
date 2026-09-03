namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class ReachabilityAndUncertaintyTests
{
    [Test]
    public void Root_type_test_does_not_count_as_a_source_member_use()
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
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.Source)
                .Members(source => source is Source
                    ? new() { Value = Ignore() }
                    : new() { Value = Ignore() });
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0047" }),
                string.Join(
                    Environment.NewLine,
                    result.EffectiveDiagnostics.Select(static diagnostic =>
                        diagnostic.Id + ": " + diagnostic.GetMessage())));
            Assert.That(
                result.CompletenessDiagnostics.Single().GetMessage(),
                Does.Contain("TestCase.Source.Value"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Disabled_create_callback_does_not_create_source_participation()
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
        public Destination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MappingMode(MappingMode.Update);
            builder.NullDestinationHandling(NullDestinationHandling.Throw);
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Source)
                .Construct(source => new(source.Value));
        }
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0047" }),
                string.Join(
                    Environment.NewLine,
                    result.EffectiveDiagnostics.Select(static diagnostic =>
                        diagnostic.Id + ": " + diagnostic.GetMessage())));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Invalid_member_rule_suppresses_only_its_target_warning()
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
        public int InvalidTarget { get; set; }
        public int Independent { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination)
                .Members(source => new() { InvalidTarget = Auto() });
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0040", "MORPH0048" }));
            Assert.That(
                result.CompletenessDiagnostics.Single().GetMessage(),
                Does.Contain("TestCase.Destination.Independent"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Conditional_participation_is_pair_wide_and_not_duplicated()
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
        public bool Condition { get; set; }
        public int Value { get; set; }
        public int Unused { get; set; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .Members(source => source.Condition
                    ? new() { Value = Value(source.Value) }
                    : new() { Value = Ignore() });
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
            Assert.That(
                result.CompletenessDiagnostics[0].GetMessage(),
                Does.Contain("TestCase.Source.Unused"));
            Assert.That(
                result.CompletenessDiagnostics[1].GetMessage(),
                Does.Contain("TestCase.Destination.Unmapped"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
