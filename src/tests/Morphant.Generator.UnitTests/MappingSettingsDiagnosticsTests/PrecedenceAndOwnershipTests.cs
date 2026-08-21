namespace Morphant.Generator.UnitTests.MappingSettingsDiagnosticsTests;

[TestFixture]
internal sealed class PrecedenceAndOwnershipTests
{
    [Test]
    public void Included_pair_setting_precedes_invalid_current_root()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class BaseSource { }
    public sealed class Source : BaseSource { }
    public class BaseDestination { }
    public sealed class Destination : BaseDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalidMode = MappingMode.Create;
            var invalidNullSource = NullSourceHandling.Throw;
            var invalidNullDestination = NullDestinationHandling.Throw;
            var invalidUnknown = UnknownDerivedTypeHandling.Throw;
            var invalidConstructor = ConstructorSelection.Greediest;
            var invalidRoot = MemberSelection.Auto;
            var invalidValidation = UnmappedMemberValidation.Strict;

            builder.MappingMode(invalidMode);
            builder.NullSourceHandling(invalidNullSource);
            builder.NullDestinationHandling(invalidNullDestination);
            builder.UnknownDerivedTypeHandling(invalidUnknown);
            builder.ConstructorSelection(invalidConstructor);
            builder.MemberSelection(invalidRoot);
            builder.UnmappedMemberValidation(invalidValidation);

            builder.Map<BaseSource, BaseDestination>(
                    MappingMode.CreateAndUpdate)
                .NullSourceHandling(NullSourceHandling.ReturnNull)
                .NullDestinationHandling(NullDestinationHandling.Create)
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.UseBaseMapping)
                .ConstructorSelection(ConstructorSelection.Unambiguous)
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.None);
            builder.Map<Source, Destination>()
                .IncludeBase<BaseSource, BaseDestination>();
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
    public void Invalid_connected_root_is_deduplicated_after_current_root_Default()
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
    public partial class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalidNullSource = NullSourceHandling.Throw;
            var invalidNullDestination = NullDestinationHandling.Throw;
            var invalidUnknown = UnknownDerivedTypeHandling.Throw;
            var invalidConstructor = ConstructorSelection.Greediest;
            var invalidBase = MemberSelection.Auto;
            var invalidValidation = UnmappedMemberValidation.Strict;

            builder.MappingMode(MappingMode.CreateAndUpdate);
            builder.NullSourceHandling(invalidNullSource);
            builder.NullDestinationHandling(invalidNullDestination);
            builder.UnknownDerivedTypeHandling(invalidUnknown);
            builder.ConstructorSelection(invalidConstructor);
            builder.MemberSelection(invalidBase);
            builder.UnmappedMemberValidation(invalidValidation);
        }
    }

    [MorphantMapper]
    public partial class TestMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.MappingMode(MappingMode.Default);
            builder.NullSourceHandling(NullSourceHandling.Default);
            builder.NullDestinationHandling(NullDestinationHandling.Default);
            builder.UnknownDerivedTypeHandling(
                UnknownDerivedTypeHandling.Default);
            builder.ConstructorSelection(ConstructorSelection.Default);
            builder.MemberSelection(MemberSelection.Default);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Default);
            builder.Map<SourceA, DestinationA>();
            builder.Map<SourceB, DestinationB>();
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);
        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0021", 6)));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingSettingsDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "invalidNullSource",
                    "invalidNullDestination",
                    "invalidUnknown",
                    "invalidConstructor",
                    "invalidBase",
                    "invalidValidation"
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Invalid_connected_mapping_mode_is_deduplicated_across_pairs()
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
    public partial class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalidMode = MappingMode.Create;
            builder.MappingMode(invalidMode);
        }
    }

    [MorphantMapper]
    public partial class TestMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.MappingMode(MappingMode.Default);
            builder.Map<SourceA, DestinationA>();
            builder.Map<SourceB, DestinationB>();
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
    public void Current_root_value_overrides_invalid_connected_root()
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
    public partial class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalidMode = MappingMode.Create;
            var invalidNullSource = NullSourceHandling.Throw;
            var invalidNullDestination = NullDestinationHandling.Throw;
            var invalidUnknown = UnknownDerivedTypeHandling.Throw;
            var invalidConstructor = ConstructorSelection.Greediest;
            var invalidBase = MemberSelection.Auto;
            var invalidValidation = UnmappedMemberValidation.Strict;

            builder.MappingMode(invalidMode);
            builder.NullSourceHandling(invalidNullSource);
            builder.NullDestinationHandling(invalidNullDestination);
            builder.UnknownDerivedTypeHandling(invalidUnknown);
            builder.ConstructorSelection(invalidConstructor);
            builder.MemberSelection(invalidBase);
            builder.UnmappedMemberValidation(invalidValidation);
        }
    }

    [MorphantMapper]
    public partial class TestMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.MappingMode(MappingMode.CreateAndUpdate);
            builder.NullSourceHandling(NullSourceHandling.ReturnNull);
            builder.NullDestinationHandling(NullDestinationHandling.Create);
            builder.UnknownDerivedTypeHandling(
                UnknownDerivedTypeHandling.UseBaseMapping);
            builder.ConstructorSelection(ConstructorSelection.Unambiguous);
            builder.MemberSelection(MemberSelection.Explicit);
            builder.UnmappedMemberValidation(UnmappedMemberValidation.None);
            builder.Map<Source, Destination>();
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
    public void Connected_root_values_override_invalid_assembly_properties()
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
    public partial class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MappingMode(MappingMode.CreateAndUpdate);
            builder.NullSourceHandling(NullSourceHandling.ReturnNull);
            builder.NullDestinationHandling(NullDestinationHandling.Create);
            builder.UnknownDerivedTypeHandling(
                UnknownDerivedTypeHandling.UseBaseMapping);
            builder.ConstructorSelection(ConstructorSelection.Unambiguous);
            builder.MemberSelection(MemberSelection.Auto);
            builder.UnmappedMemberValidation(UnmappedMemberValidation.None);
        }
    }

    [MorphantMapper]
    public partial class TestMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.MappingMode(MappingMode.Default);
            builder.NullSourceHandling(NullSourceHandling.Default);
            builder.NullDestinationHandling(NullDestinationHandling.Default);
            builder.UnknownDerivedTypeHandling(
                UnknownDerivedTypeHandling.Default);
            builder.ConstructorSelection(ConstructorSelection.Default);
            builder.MemberSelection(MemberSelection.Default);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Default);
            builder.Map<Source, Destination>();
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(
            source,
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.MorphantMappingMode"] = "Unexpected",
                ["build_property.MorphantNullSourceHandling"] = "Unexpected",
                ["build_property.MorphantNullDestinationHandling"] =
                    "Unexpected",
                ["build_property.MorphantUnknownDerivedTypeHandling"] =
                    "Unexpected",
                ["build_property.MorphantConstructorSelection"] =
                    "Unexpected",
                ["build_property.MorphantMemberSelection"] = "Unexpected",
                ["build_property.MorphantUnmappedMemberValidation"] =
                    "Unexpected"
            });

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Discarded_registration_and_unsupported_pair_flow_do_not_create_setting_diagnostics()
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var discarded = MemberSelection.Auto;
            var escaped = MemberSelection.Auto;

            builder.Map<SourceA, DestinationA>();
            builder.Map<SourceA, DestinationA>()
                .MemberSelection(discarded);

            var mapping = builder.Map<SourceB, DestinationB>();
            mapping.MemberSelection(escaped);
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0013", "MORPH0018" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Inapplicable_setting_owns_an_invalid_value_without_MORPH0021()
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
            builder.Map<Source, Destination>()
                .MemberSelection((MemberSelection)int.MaxValue)
                .Convert(source => new Destination());
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
                Is.EqualTo("MemberSelection"));
            Assert.That(
                MappingSettingsDiagnosticsGeneratorTest.SourceText(
                    diagnostic.AdditionalLocations.Single()),
                Is.EqualTo("Convert"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Final_Default_is_the_single_inapplicable_manual_setting()
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
            builder.Map<Source, Destination>()
                .MemberSelection((MemberSelection)int.MaxValue)
                .MemberSelection(MemberSelection.Default)
                .Convert(source => new Destination());
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0023"));
            Assert.That(
                MappingSettingsDiagnosticsGeneratorTest.Line(
                    diagnostic.Location),
                Is.EqualTo(17));
            Assert.That(
                MappingSettingsDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("MemberSelection"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
