namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class SourceUseAndDiscardTests
{
    [Test]
    public void Tracks_direct_chained_condition_and_local_reads_semantically()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Address
    {
        public int Number { get; set; }
    }

    public sealed class Source
    {
        public int Direct { get; set; }
        public Address Address { get; } = new();
        public bool Condition { get; set; }
        public int SymbolOnly { get; set; }
        public int Unused { get; set; }
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
                .UnmappedMemberValidation(UnmappedMemberValidation.Source)
                .Members(source =>
                {
                    var local = source.Direct;
                    return source.Condition &&
                           nameof(source.SymbolOnly).Length > 0
                        ? new()
                        {
                            Value = Value(source.Address.Number + local)
                        }
                        : new() { Value = Ignore() };
                });
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0047", "MORPH0047" }));
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Source member 'global::TestCase.Source.SymbolOnly' is " +
                    "not used by mapping 'TestCase.Source -> " +
                    "TestCase.Destination'.",
                    "Source member 'global::TestCase.Source.Unused' is not " +
                    "used by mapping 'TestCase.Source -> " +
                    "TestCase.Destination'."
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Whole_source_handoff_is_potential_use_but_not_destination_occupancy()
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
        public int First { get; set; }
        public int Second { get; set; }
    }

    public sealed class Destination
    {
        public int RuntimeOnly { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .ConstructUsing(source => Build(source));

        private static Destination Build(Source source) => new()
        {
            RuntimeOnly = source.First
        };
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0048" }));
            Assert.That(
                result.CompletenessDiagnostics.Single().GetMessage(),
                Does.Contain("global::TestCase.Destination.RuntimeOnly"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Runtime_callback_reads_source_but_does_not_reveal_destination_mapping()
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .ConstructUsing(source =>
                {
                    _ = source.Value;
                    return new Destination { Value = source.Value };
                });
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.That(
            result.CompletenessDiagnostics.Select(static diagnostic =>
                diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0048" }));
    }

    [Test]
    public void Scalar_source_has_no_supported_member_universe()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<string, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Source)
                .ConstructUsing(source => new Destination());
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.CompletenessDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Property_pattern_reads_only_its_root_source_member()
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
        public int Unused { get; set; }
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
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.Source)
                .Members(source => source is { Value: > 0 }
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
                Is.EqualTo(new[] { "MORPH0047" }));
            Assert.That(
                result.CompletenessDiagnostics.Single().GetMessage(),
                Does.Contain("global::TestCase.Source.Unused"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Compile_time_discards_cover_all_structured_callback_families()
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
        public int Legacy { get; set; }
    }

    public sealed class ConstructDestination { }
    public sealed class ResolveDestination { }
    public sealed class MembersDestination
    {
        public int Occupied { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Source)
                .Construct(source =>
                {
                    _ = source.Legacy;
                    return new();
                });

            builder.Map<Source, ResolveDestination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Source)
                .Resolve((source, previous) =>
                {
                    _ = source.Legacy;
                    return new();
                });

            builder.Map<Source, MembersDestination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Source)
                .Members(source =>
                {
                    _ = source.Legacy;
                    return new() { Occupied = Ignore() };
                });
        }
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.CompletenessDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.TypeMapperSource, Does.Not.Contain(".Legacy"));
        });
    }
}
