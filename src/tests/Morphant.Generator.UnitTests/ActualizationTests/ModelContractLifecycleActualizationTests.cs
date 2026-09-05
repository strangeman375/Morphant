using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class ModelContractLifecycleActualizationTests
{
    private static readonly string[] GeneratedFiles =
    [
        "Morphant.Generated.Construction.TestCase_Destination.g.cs",
        "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.Member.TestCase_Destination.g.cs",
        "Morphant.Generated.MemberExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs",
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs"
    ];

    [Test]
    public void Actualizes_added_removed_and_restored_partial_contracts()
    {
        var mapper = SourceFile("Mapper.cs", MapperSource);
        var coreModels = SourceFile("CoreModels.cs", PartialCoreModelsSource);
        var additionalModels = SourceFile(
            "AdditionalModels.cs",
            PartialAdditionalModelsSource);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "both partial declarations present",
                [
                    coreModels,
                    additionalModels,
                    mapper,
                    SourceFile("Scenario.cs", PartialWideScenarioSource)
                ],
                GeneratedFiles,
                "TestCase.Scenario"),
            ExecutableStep(
                "additional partial declarations removed",
                [
                    coreModels,
                    mapper,
                    SourceFile("Scenario.cs", NarrowScenarioSource)
                ],
                GeneratedFiles,
                "TestCase.Scenario"),
            ExecutableStep(
                "additional partial declarations restored",
                [
                    coreModels,
                    additionalModels,
                    mapper,
                    SourceFile("Scenario.cs", PartialWideScenarioSource)
                ],
                GeneratedFiles,
                "TestCase.Scenario"));
    }

    [Test]
    public void Actualizes_added_removed_and_restored_inherited_members()
    {
        var mapper = SourceFile("Mapper.cs", MapperSource);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "base members present",
                [
                    SourceFile("Models.cs", InheritedWideModelsSource),
                    mapper,
                    SourceFile("Scenario.cs", InheritedWideScenarioSource)
                ],
                GeneratedFiles,
                "TestCase.Scenario"),
            ExecutableStep(
                "base members removed",
                [
                    SourceFile("Models.cs", InheritedNarrowModelsSource),
                    mapper,
                    SourceFile("Scenario.cs", NarrowScenarioSource)
                ],
                GeneratedFiles,
                "TestCase.Scenario"),
            ExecutableStep(
                "base members restored",
                [
                    SourceFile("Models.cs", InheritedWideModelsSource),
                    mapper,
                    SourceFile("Scenario.cs", InheritedWideScenarioSource)
                ],
                GeneratedFiles,
                "TestCase.Scenario"));
    }

    // lang=c#
    private const string MapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string PartialCoreModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed partial class Source
    {
        public int Value { get; init; }
    }

    public sealed partial class Destination
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string PartialAdditionalModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed partial class Source
    {
        public int Extra { get; init; }
    }

    public sealed partial class Destination
    {
        public int Extra { get; set; }
    }
}
""";

    // lang=c#
    private const string InheritedWideModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public class SourceBase
    {
        public int Extra { get; init; }
    }

    public sealed class Source : SourceBase
    {
        public int Value { get; init; }
    }

    public class DestinationBase
    {
        public int Extra { get; set; }
    }

    public sealed class Destination : DestinationBase
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string InheritedNarrowModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public class SourceBase { }

    public sealed class Source : SourceBase
    {
        public int Value { get; init; }
    }

    public class DestinationBase { }

    public sealed class Destination : DestinationBase
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string NarrowScenarioSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var destination = mapper.Create(new Source { Value = 17 });

            if (destination.Value != 17)
            {
                throw new InvalidOperationException(
                    "The narrow model contract was not actualized.");
            }
        }
    }
}
""";

    // lang=c#
    private const string PartialWideScenarioSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var destination = mapper.Create(new Source
            {
                Value = 17,
                Extra = 29
            });

            if (destination.Value != 17 || destination.Extra != 29)
            {
                throw new InvalidOperationException(
                    "The partial model contract was not actualized.");
            }
        }
    }
}
""";

    // lang=c#
    private const string InheritedWideScenarioSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var destination = mapper.Create(new Source
            {
                Value = 17,
                Extra = 29
            });

            if (destination.Value != 17 || destination.Extra != 29)
            {
                throw new InvalidOperationException(
                    "The inherited model contract was not actualized.");
            }
        }
    }
}
""";
}
