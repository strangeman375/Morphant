namespace Morphant.Generator.UnitTests.MappingSettingsDiagnosticsTests;

[TestFixture]
internal sealed class CSharpValueTests
{
    [Test]
    public void Accepts_every_declared_value_and_mapping_mode_combination()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        private const MappingMode Combined =
            MappingMode.Create | MappingMode.Update;

        protected override void Configure(MapperBuilder builder)
        {
            builder.MappingMode(MappingMode.Default);
            builder.MappingMode(MappingMode.Create);
            builder.MappingMode(MappingMode.Update);
            builder.MappingMode(MappingMode.CreateAndUpdate);
            builder.MappingMode(Combined);

            builder.NullSourceHandling(NullSourceHandling.Default);
            builder.NullSourceHandling(NullSourceHandling.ReturnNull);
            builder.NullSourceHandling(NullSourceHandling.ReturnDestination);
            builder.NullSourceHandling(NullSourceHandling.Throw);

            builder.NullDestinationHandling(NullDestinationHandling.Default);
            builder.NullDestinationHandling(NullDestinationHandling.Create);
            builder.NullDestinationHandling(NullDestinationHandling.Throw);

            builder.ConstructorSelection(ConstructorSelection.Default);
            builder.ConstructorSelection(ConstructorSelection.Explicit);
            builder.ConstructorSelection(ConstructorSelection.Parameterless);
            builder.ConstructorSelection(ConstructorSelection.Single);
            builder.ConstructorSelection(ConstructorSelection.Unambiguous);
            builder.ConstructorSelection(ConstructorSelection.Greediest);
            builder.ConstructorSelection(ConstructorSelection.Largest);

            builder.MemberSelection(MemberSelection.Default);
            builder.MemberSelection(MemberSelection.Auto);
            builder.MemberSelection(MemberSelection.Explicit);

            builder.Flattening(Flattening.Default);
            builder.Flattening(Flattening.Auto);
            builder.Flattening(Flattening.None);

            builder.UnmappedMemberValidation(UnmappedMemberValidation.Default);
            builder.UnmappedMemberValidation(UnmappedMemberValidation.None);
            builder.UnmappedMemberValidation(UnmappedMemberValidation.Source);
            builder.UnmappedMemberValidation(UnmappedMemberValidation.Destination);
            builder.UnmappedMemberValidation(UnmappedMemberValidation.Strict);

            builder.Map<Source, Destination>(Combined);
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_each_effective_nonconstant_or_unknown_value()
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
    public sealed class Destination1 { }
    public sealed class Destination2 { }
    public sealed class Destination3 { }
    public sealed class Destination4 { }
    public sealed class Destination5 { }
    public sealed class Destination6 { }
    public sealed class Destination7 { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var mode = MappingMode.Create;
            var nullSource = NullSourceHandling.Throw;
            var nullDestination = NullDestinationHandling.Throw;
            var constructor = ConstructorSelection.Greediest;
            var members = MemberSelection.Explicit;
            var flattening = Flattening.None;
            var unmapped = UnmappedMemberValidation.Strict;

            builder.Map<Source, Destination1>(mode);
            builder.Map<Source, Destination2>()
                .NullSourceHandling(nullSource);
            builder.Map<Source, Destination3>()
                .NullDestinationHandling(nullDestination);
            builder.Map<Source, Destination4>()
                .ConstructorSelection(constructor);
            builder.Map<Source, Destination5>()
                .MemberSelection(members);
            builder.Map<Source, Destination6>()
                .Flattening(flattening);
            builder.Map<Source, Destination7>()
                .UnmappedMemberValidation(unmapped);
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.Diagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0021", 7)));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    MappingSettingsDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "mode",
                    "nullSource",
                    "nullDestination",
                    "constructor",
                    "members",
                    "flattening",
                    "unmapped"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Setting 'MappingMode' must be a supported " +
                    "compile-time constant.",
                    "Setting 'NullSourceHandling' must be a " +
                    "supported compile-time constant.",
                    "Setting 'NullDestinationHandling' must be a " +
                    "supported compile-time constant.",
                    "Setting 'ConstructorSelection' must be a " +
                    "supported compile-time constant.",
                    "Setting 'MemberSelection' must be a supported " +
                    "compile-time constant.",
                    "Setting 'Flattening' must be a supported " +
                    "compile-time constant.",
                    "Setting 'UnmappedMemberValidation' must be a " +
                    "supported compile-time constant."
                }));
            Assert.That(diagnostics, Has.All.Property("AdditionalLocations").Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_negative_and_unknown_constants_for_every_setting()
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
    public sealed class Destination1 { }
    public sealed class Destination2 { }
    public sealed class Destination3 { }
    public sealed class Destination4 { }
    public sealed class Destination5 { }
    public sealed class Destination6 { }
    public sealed class Destination7 { }
    public sealed class Destination8 { }
    public sealed class Destination9 { }
    public sealed class Destination10 { }
    public sealed class Destination11 { }
    public sealed class Destination12 { }
    public sealed class Destination13 { }
    public sealed class Destination14 { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination1>((MappingMode)(-1));
            builder.Map<Source, Destination2>((MappingMode)4);
            builder.Map<Source, Destination3>()
                .NullSourceHandling((NullSourceHandling)(-1));
            builder.Map<Source, Destination4>()
                .NullSourceHandling((NullSourceHandling)int.MaxValue);
            builder.Map<Source, Destination5>()
                .NullDestinationHandling((NullDestinationHandling)(-1));
            builder.Map<Source, Destination6>()
                .NullDestinationHandling(
                    (NullDestinationHandling)int.MaxValue);
            builder.Map<Source, Destination7>()
                .ConstructorSelection((ConstructorSelection)(-1));
            builder.Map<Source, Destination8>()
                .ConstructorSelection((ConstructorSelection)int.MaxValue);
            builder.Map<Source, Destination9>()
                .MemberSelection((MemberSelection)(-1));
            builder.Map<Source, Destination10>()
                .MemberSelection((MemberSelection)int.MaxValue);
            builder.Map<Source, Destination11>()
                .Flattening((Flattening)(-1));
            builder.Map<Source, Destination12>()
                .Flattening((Flattening)int.MaxValue);
            builder.Map<Source, Destination13>()
                .UnmappedMemberValidation(
                    (UnmappedMemberValidation)(-1));
            builder.Map<Source, Destination14>()
                .UnmappedMemberValidation(
                    (UnmappedMemberValidation)int.MaxValue);
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0021", 14)));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage().Split('\'')[1]),
                Is.EqualTo(new[]
                {
                    "MappingMode",
                    "MappingMode",
                    "NullSourceHandling",
                    "NullSourceHandling",
                    "NullDestinationHandling",
                    "NullDestinationHandling",
                    "ConstructorSelection",
                    "ConstructorSelection",
                    "MemberSelection",
                    "MemberSelection",
                    "Flattening",
                    "Flattening",
                    "UnmappedMemberValidation",
                    "UnmappedMemberValidation"
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Final_Default_discards_an_earlier_invalid_call_at_each_CSharp_level()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class SourceA { }
    public sealed class DestinationA { }
    public sealed class SourceB { }
    public sealed class DestinationB { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalidRoot = MemberSelection.Auto;
            var invalidPair = NullSourceHandling.Throw;

            builder.MemberSelection(invalidRoot);
            builder.MemberSelection(MemberSelection.Default);
            builder.Map<SourceA, DestinationA>();

            builder.Map<SourceB, DestinationB>()
                .NullSourceHandling(invalidPair)
                .NullSourceHandling(NullSourceHandling.Default);
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Last_successfully_bound_call_wins_and_invalid_outer_origin_is_deduplicated()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class SourceA { }
    public sealed class DestinationA { }
    public sealed class SourceB { }
    public sealed class DestinationB { }
    public sealed class SourceC { }
    public sealed class DestinationC { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalid = MemberSelection.Auto;

            builder.MemberSelection(MemberSelection.Explicit);
            builder.MemberSelection(invalid);

            builder.Map<SourceA, DestinationA>();
            builder.Map<SourceB, DestinationB>();
            builder.Map<SourceC, DestinationC>()
                .MemberSelection(MemberSelection.Auto);
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0021"));
            Assert.That(
                MappingSettingsDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("invalid"));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Compiler_owned_binding_failures_and_same_named_APIs_are_excluded()
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
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MemberSelection(42);
            new OtherBuilder().MemberSelection(global::System.DateTime.Now);
            builder.Map<Source, Destination>();
        }
    }

    public sealed class OtherBuilder
    {
        public OtherBuilder MemberSelection(
            global::System.DateTime value) => this;
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                result.CompilerWarningsAndErrors.Select(
                    static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "CS1503" }));
        });
    }
}
