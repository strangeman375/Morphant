using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Incrementality;

[TestFixture]
internal sealed class TemplateTypeSemanticDependencyTests
{
    private const string DestinationHintName =
        "Morphant.TemplateType.TestCase_Destination.g.cs";

    private const string StableDestinationHintName =
        "Morphant.TemplateType.TestCase_StableDestination.g.cs";

    [Test]
    public void Rebuilds_only_affected_template_when_global_alias_changes()
    {
        var stableFiles = new[]
        {
            SourceFile("Mapper.cs", MapperSource),
            SourceFile("Destinations.cs", AliasedDestinationSource),
            SourceFile("Values.cs", ValueTypesSource)
        };

        RunAndAssert(
            LanguageVersion.CSharp10,
            Step(
                "initial global alias",
                stableFiles.Append(
                        SourceFile(
                            "GlobalUsings.cs",
                            BuildGlobalAliasSource("FirstValue")))
                    .ToArray(),
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    StableDestinationHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "global alias changed",
                stableFiles.Append(
                        SourceFile(
                            "GlobalUsings.cs",
                            BuildGlobalAliasSource("SecondValue")))
                    .ToArray(),
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.Modified),
                Expected(
                    StableDestinationHintName,
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Rebuilds_only_affected_template_when_external_constant_changes()
    {
        var stableFiles = new[]
        {
            SourceFile("Mapper.cs", MapperSource),
            SourceFile(
                "Destinations.cs",
                DefaultValueDestinationSource)
        };

        RunAndAssert(
            Step(
                "initial external constant",
                stableFiles.Append(
                        SourceFile(
                            "Defaults.cs",
                            BuildDefaultsSource(1)))
                    .ToArray(),
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    StableDestinationHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "external constant changed",
                stableFiles.Append(
                        SourceFile(
                            "Defaults.cs",
                            BuildDefaultsSource(2)))
                    .ToArray(),
                Expected(
                    DestinationHintName,
                    IncrementalStepRunReason.Modified),
                Expected(
                    StableDestinationHintName,
                    IncrementalStepRunReason.Cached)));
    }

    private static string BuildGlobalAliasSource(string valueType)
    {
        return GlobalAliasSourceTemplate.Replace(
            "__VALUE_TYPE__",
            valueType);
    }

    private static string BuildDefaultsSource(int value)
    {
        return DefaultsSourceTemplate.Replace(
            "__DEFAULT_VALUE__",
            value.ToString());
    }

    // lang=c#
    private const string MapperSource =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, StableDestination>();
        }
    }
}
""";

    // lang=c#
    private const string AliasedDestinationSource =
"""
#nullable enable

namespace TestCase
{
    public sealed class Destination
    {
        public DestinationValue Value { get; set; } = null!;
    }

    public sealed class StableDestination
    {
        public int Id { get; set; }
    }
}
""";

    // lang=c#
    private const string ValueTypesSource =
"""
namespace TestCase
{
    public sealed class FirstValue
    {
    }

    public sealed class SecondValue
    {
    }
}
""";

    // lang=c#
    private const string GlobalAliasSourceTemplate =
"""
global using DestinationValue = TestCase.__VALUE_TYPE__;
""";

    // lang=c#
    private const string DefaultValueDestinationSource =
"""
namespace TestCase
{
    public sealed class Destination
    {
        public Destination(int value = Defaults.Value)
        {
        }
    }

    public sealed class StableDestination
    {
        public int Id { get; set; }
    }
}
""";

    // lang=c#
    private const string DefaultsSourceTemplate =
"""
namespace TestCase
{
    public static class Defaults
    {
        public const int Value = __DEFAULT_VALUE__;
    }
}
""";
}
