namespace Morphant.Generator.UnitTests.MappingSettingsDiagnosticsTests;

[TestFixture]
internal sealed class MsBuildValueTests
{
    [Test]
    public void Accepts_trimmed_case_insensitive_named_values_and_Default()
    {
        var result = MappingSettingsDiagnosticsGeneratorTest.Run(
            DeclarativeSource,
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.MorphantMappingMode"] =
                    " createandupdate ",
                ["build_property.MorphantNullSourceHandling"] = " throw ",
                ["build_property.MorphantNullDestinationHandling"] =
                    " create ",
                ["build_property.MorphantUnknownDerivedTypeHandling"] =
                    " usebasemapping ",
                ["build_property.MorphantConstructorSelection"] =
                    " unambiguous ",
                ["build_property.MorphantMemberSelection"] = " auto ",
                ["build_property.MorphantFlattening"] = " auto ",
                ["build_property.MorphantUnmappedMemberValidation"] =
                    " default "
            });

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Empty_and_Default_properties_continue_to_library_values()
    {
        var result = MappingSettingsDiagnosticsGeneratorTest.Run(
            DeclarativeSource,
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.MorphantMappingMode"] = string.Empty,
                ["build_property.MorphantNullSourceHandling"] = "   ",
                ["build_property.MorphantNullDestinationHandling"] =
                    "Default",
                ["build_property.MorphantUnknownDerivedTypeHandling"] =
                    " default ",
                ["build_property.MorphantConstructorSelection"] =
                    string.Empty,
                ["build_property.MorphantMemberSelection"] = " default ",
                ["build_property.MorphantFlattening"] = "\t",
                ["build_property.MorphantUnmappedMemberValidation"] = "\t"
            });

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_each_applicable_invalid_property_once_in_ordinal_order()
    {
        var result = MappingSettingsDiagnosticsGeneratorTest.Run(
            DeclarativeSource,
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.MorphantMappingMode"] = "CreateAndUpdate",
                ["build_property.MorphantNullSourceHandling"] =
                    " Unexpected ",
                ["build_property.MorphantNullDestinationHandling"] = "2",
                ["build_property.MorphantUnknownDerivedTypeHandling"] =
                    "Fallback",
                ["build_property.MorphantConstructorSelection"] =
                    "ConstructorSelection.Greediest",
                ["build_property.MorphantMemberSelection"] = "Auto, Explicit",
                ["build_property.MorphantFlattening"] = "Enabled",
                ["build_property.MorphantUnmappedMemberValidation"] = "-1"
            });

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0022", 7)));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "MSBuild property 'MorphantConstructorSelection' has " +
                    "unsupported value 'ConstructorSelection.Greediest'.",
                    "MSBuild property 'MorphantFlattening' has unsupported " +
                    "value 'Enabled'.",
                    "MSBuild property 'MorphantMemberSelection' has " +
                    "unsupported value 'Auto, Explicit'.",
                    "MSBuild property 'MorphantNullDestinationHandling' " +
                    "has unsupported value '2'.",
                    "MSBuild property 'MorphantNullSourceHandling' has " +
                    "unsupported value 'Unexpected'.",
                    "MSBuild property 'MorphantUnknownDerivedTypeHandling' " +
                    "has unsupported value 'Fallback'.",
                    "MSBuild property 'MorphantUnmappedMemberValidation' " +
                    "has unsupported value '-1'."
                }));
            Assert.That(
                result.Diagnostics,
                Has.All.Property("Location").EqualTo(
                    Microsoft.CodeAnalysis.Location.None));
            Assert.That(
                result.Diagnostics,
                Has.All.Property("AdditionalLocations").Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void More_specific_valid_value_hides_an_invalid_property()
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
            builder.Map<SourceA, DestinationA>(MappingMode.CreateAndUpdate)
                .NullSourceHandling(NullSourceHandling.ReturnNull)
                .NullDestinationHandling(NullDestinationHandling.Create)
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.UseBaseMapping)
                .ConstructorSelection(ConstructorSelection.Unambiguous)
                .MemberSelection(MemberSelection.Explicit)
                .Flattening(Flattening.None)
                .UnmappedMemberValidation(UnmappedMemberValidation.None);
            builder.Map<SourceB, DestinationB>(MappingMode.CreateAndUpdate)
                .NullSourceHandling(NullSourceHandling.ReturnDestination)
                .NullDestinationHandling(NullDestinationHandling.Throw)
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw)
                .ConstructorSelection(ConstructorSelection.Parameterless)
                .MemberSelection(MemberSelection.Auto)
                .Flattening(Flattening.Auto)
                .UnmappedMemberValidation(UnmappedMemberValidation.Source);
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
                ["build_property.MorphantFlattening"] = "Unexpected",
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
    public void Invalid_property_is_deduplicated_across_pairs_and_ignored_by_manual_model()
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<SourceA, DestinationA>();
            builder.Map<SourceB, DestinationB>();
            builder.Map<SourceC, DestinationC>()
                .Convert(source => new DestinationC());
        }
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(
            source,
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.MorphantMemberSelection"] = "Unexpected"
            });
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0022"));
            Assert.That(diagnostic.Location, Is.EqualTo(
                Microsoft.CodeAnalysis.Location.None));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("MorphantMemberSelection"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Invalid_mapping_mode_property_is_owned_even_by_manual_mapping()
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
                .Convert(source => new Destination());
    }
}
""";

        var result = MappingSettingsDiagnosticsGeneratorTest.Run(
            source,
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.MorphantMappingMode"] = "Create, Update"
            });
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0022"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("MorphantMappingMode"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    // lang=c#
    private const string DeclarativeSource =
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
            builder.Map<Source, Destination>();
    }
}
""";
}
