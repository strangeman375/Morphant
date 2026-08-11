namespace Morphant.Generator.UnitTests.MappingSettingsDiagnosticsTests;

[TestFixture]
internal sealed class ApplicabilityTests
{
    [Test]
    public void Every_explicit_non_mode_setting_is_inapplicable_to_local_Convert()
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(MappingMode.Create)
                .NullSourceHandling(NullSourceHandling.Default)
                .NullDestinationHandling(NullDestinationHandling.Throw)
                .ConstructorSelection((ConstructorSelection)int.MaxValue)
                .MemberSelection(MemberSelection.Auto)
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .Convert(source => new Destination());
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0023", 5)));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingSettingsDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "ConstructorSelection",
                    "MemberSelection",
                    "NullDestinationHandling",
                    "NullSourceHandling",
                    "UnmappedMemberValidation"
                }));
            Assert.That(
                result.Diagnostics.SelectMany(static diagnostic =>
                    diagnostic.AdditionalLocations).Select(location =>
                    MappingSettingsDiagnosticsGeneratorTest.SourceText(
                        location)),
                Is.EqualTo(Enumerable.Repeat("Convert", 5)));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.All.Contain("manual Convert"));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.All.Contain(
                    "global::Morphant.ITypeMapper<global::TestCase.Source, " +
                    "global::TestCase.Destination>"));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.All.Contain("global::TestCase.TestMapper"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Explicit_ConstructorSelection_is_inapplicable_without_structured_construction_surface()
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

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, int>()
                .ConstructorSelection(ConstructorSelection.Default);
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0023"));
            Assert.That(
                MappingSettingsDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("ConstructorSelection"));
            Assert.That(
                MappingSettingsDiagnosticsGeneratorTest.SourceText(
                    diagnostic.AdditionalLocations.Single()),
                Is.EqualTo("int"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapping setting 'ConstructorSelection' is not " +
                    "applicable to mapping without structured construction " +
                    "capability for contract " +
                    "'global::Morphant.ITypeMapper<global::TestCase.Source, " +
                    "int>' in mapper 'global::TestCase.TestMapper'."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Root_settings_are_harmless_no_ops_for_manual_and_opaque_pairs()
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

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalidConstructor = ConstructorSelection.Greediest;

            builder.ConstructorSelection(invalidConstructor);

            builder.Map<SourceA, DestinationA>()
                .Convert(source => new DestinationA());
            builder.Map<SourceB, int>();
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
    public void ConstructorSelection_is_diagnosed_only_for_reachable_convention_paths()
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
    public sealed class ExplicitDestination { }
    public sealed class RuntimeDestination { }
    public sealed class ConventionDestination { }
    public sealed class ByConventionDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalid = ConstructorSelection.Greediest;

            builder.Map<Source, ExplicitDestination>()
                .ConstructorSelection(invalid)
                .Construct(source => new());

            builder.Map<Source, RuntimeDestination>()
                .ConstructorSelection(invalid)
                .ConstructUsing(source => new RuntimeDestination());

            builder.Map<Source, ConventionDestination>()
                .ConstructorSelection(invalid);

            builder.Map<Source, ByConventionDestination>()
                .ConstructorSelection(invalid)
                .Construct(source => new(ByConvention()));
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0021", "MORPH0021" }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingSettingsDiagnosticsGeneratorTest.Line(
                        diagnostic.Location)),
                Is.EqualTo(new[] { 30, 33 }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingSettingsDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "invalid", "invalid" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void NullDestinationHandling_requires_Update_but_other_declarative_settings_do_not()
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
    public sealed class CreateOnlyDestination { }
    public sealed class UpdateDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalidNullDestination = NullDestinationHandling.Create;
            var invalidMemberSelection = MemberSelection.Auto;

            builder.Map<Source, CreateOnlyDestination>(MappingMode.Create)
                .NullDestinationHandling(invalidNullDestination)
                .MemberSelection(invalidMemberSelection);

            builder.Map<Source, UpdateDestination>(MappingMode.Update)
                .NullDestinationHandling(invalidNullDestination);
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Mapping setting 'MemberSelection' must be a supported " +
                    "compile-time constant.",
                    "Mapping setting 'NullDestinationHandling' must be a " +
                    "supported compile-time constant."
                }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingSettingsDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "invalidMemberSelection",
                    "invalidNullDestination"
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Invalid_MappingMode_suppresses_dependent_value_diagnostics()
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalidMode = MappingMode.Create;
            var invalidMembers = MemberSelection.Auto;

            builder.Map<Source, Destination>(invalidMode)
                .MemberSelection(invalidMembers);
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
                Is.EqualTo("invalidMode"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Mixed_manual_and_declarative_plan_keeps_only_mapping_mode_ownership()
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalidMode = MappingMode.Create;
            var invalidMembers = MemberSelection.Auto;

            builder.Map<Source, Destination>(invalidMode)
                .MemberSelection(invalidMembers)
                .Construct(source => new())
                .Convert(source => new Destination());
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0020", "MORPH0021" }));
            Assert.That(
                MappingSettingsDiagnosticsGeneratorTest.SourceText(
                    result.Diagnostics[1].Location),
                Is.EqualTo("invalidMode"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
