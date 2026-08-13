namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

using Microsoft.CodeAnalysis.CSharp;

[TestFixture]
internal sealed class DestinationOccupancyTests
{
    [Test]
    public void Counts_convention_Auto_Value_and_member_Ignore_rules()
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
        public int Conventional { get; set; }
        public int Automatic { get; set; }
        public int Explicit { get; set; }
        public int Unused { get; set; }
    }

    public sealed class Destination
    {
        public int Conventional { get; set; }
        public int Automatic { get; set; }
        public int Explicit { get; set; }
        public int Ignored { get; set; }
        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .Members(source => new()
                {
                    Automatic = Auto(),
                    Explicit = Value(source.Explicit),
                    Ignored = Ignore()
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
                Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
            Assert.That(
                result.CompletenessDiagnostics[0].GetMessage(),
                Does.Contain("global::TestCase.Source.Unused"));
            Assert.That(
                result.CompletenessDiagnostics[1].GetMessage(),
                Does.Contain("global::TestCase.Destination.Unmapped"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Omitted_and_constructor_Ignored_arguments_do_not_create_occupancy()
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
        public int Id { get; set; }
    }

    public sealed class OmittedDestination
    {
        public OmittedDestination(
            int id,
            string optional = "",
            params int[] rest)
        {
            Id = id;
            Optional = optional;
            Rest = rest;
        }

        public int Id { get; }
        public string Optional { get; }
        public int[] Rest { get; }
    }

    public sealed class IgnoredDestination
    {
        public IgnoredDestination(
            int id,
            string optional = "",
            params int[] rest)
        {
            Id = id;
            Optional = optional;
            Rest = rest;
        }

        public int Id { get; }
        public string Optional { get; }
        public int[] Rest { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, OmittedDestination>()
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination);

            builder.Map<Source, IgnoredDestination>()
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination)
                .Construct(source => new(
                    Auto(),
                    Ignore(),
                    Ignore()));
        }
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Length,
                Is.EqualTo(4));
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Destination member " +
                    "'global::TestCase.IgnoredDestination.Optional' is not " +
                    "mapped by mapping 'TestCase.Source -> " +
                    "TestCase.IgnoredDestination'.",
                    "Destination member " +
                    "'global::TestCase.IgnoredDestination.Rest' is not " +
                    "mapped by mapping 'TestCase.Source -> " +
                    "TestCase.IgnoredDestination'.",
                    "Destination member " +
                    "'global::TestCase.OmittedDestination.Optional' is not " +
                    "mapped by mapping 'TestCase.Source -> " +
                    "TestCase.OmittedDestination'.",
                    "Destination member " +
                    "'global::TestCase.OmittedDestination.Rest' is not " +
                    "mapped by mapping 'TestCase.Source -> " +
                    "TestCase.OmittedDestination'."
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Create_only_init_rule_occupies_the_pair_wide_member()
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
        public int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .Members(source => new() { Value = source.Value });
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
    public void SetsRequiredMembers_and_initializer_do_not_imply_occupancy()
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
        public Destination() { }

        public required int Required { get; init; }
        public int Initialized { get; set; } = 42;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination);
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(
            source,
            languageVersion: LanguageVersion.CSharp11);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Count(static diagnostic =>
                    diagnostic.GetMessage().Contains(
                        "global::TestCase.Destination.Required",
                        StringComparison.Ordinal)),
                Is.EqualTo(1));
            Assert.That(
                result.CompletenessDiagnostics.Count(static diagnostic =>
                    diagnostic.GetMessage().Contains(
                        "global::TestCase.Destination.Initialized",
                        StringComparison.Ordinal)),
                Is.EqualTo(1));
            Assert.That(
                result.EffectiveDiagnostics.Any(static diagnostic =>
                    diagnostic.Id == "MORPH0041"),
                Is.False);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Creation_only_rule_does_not_occupy_an_update_only_pair()
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
        public int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MappingMode(MappingMode.Update);
            builder.NullDestinationHandling(NullDestinationHandling.Throw);
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .Members(source => new() { Value = source.Value });
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
                Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
