using Microsoft.CodeAnalysis;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Incrementality;

[TestFixture]
internal sealed class TemplateTypeDependencyIsolationTests
{
    private const string DestinationAHintName =
        "Morphant.TemplateType.TestCase_DestinationA.g.cs";

    private const string DestinationBHintName =
        "Morphant.TemplateType.TestCase_DestinationB.g.cs";

    private const string ClassDestinationHintName =
        "Morphant.TemplateType.TestCase_ClassDestination.g.cs";

    private const string InterfaceDestinationHintName =
        "Morphant.TemplateType.TestCase_InterfaceDestination.g.cs";

    [Test]
    public void Rebuilds_only_changed_partial_destination()
    {
        var mapperFile = SourceFile(
            "Mapper.cs",
            BuildMapperSource(
                "TestCase.DestinationA",
                "TestCase.DestinationB"));

        var declarationFile = SourceFile(
            "DestinationA.cs",
            DestinationADeclarationSource);

        var destinationBFile = SourceFile(
            "DestinationB.cs",
            DestinationBSource);

        RunAndAssert(
            Step(
                "initial destinations",
                new[]
                {
                    mapperFile,
                    declarationFile,
                    SourceFile(
                        "DestinationA.Members.cs",
                        BuildDestinationAPartialSource("int")),
                    destinationBFile
                },
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "one partial destination changed",
                new[]
                {
                    mapperFile,
                    declarationFile,
                    SourceFile(
                        "DestinationA.Members.cs",
                        BuildDestinationAPartialSource("long")),
                    destinationBFile
                },
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Modified),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Cached)));
    }

    [Test]
    public void Rebuilds_destination_when_inherited_contract_changes()
    {
        var stableFiles = new[]
        {
            SourceFile(
                "Mapper.cs",
                BuildMapperSource(
                    "TestCase.ClassDestination",
                    "TestCase.InterfaceDestination")),
            SourceFile(
                "ClassDestination.cs",
                ClassDestinationSource),
            SourceFile(
                "InterfaceDestination.cs",
                InterfaceDestinationSource)
        };

        RunAndAssert(
            Step(
                "initial inherited contracts",
                stableFiles
                    .Append(
                        SourceFile(
                            "BaseDestination.cs",
                            BuildBaseClassSource("int")))
                    .Append(
                        SourceFile(
                            "IBaseDestination.cs",
                            BuildBaseInterfaceSource("int")))
                    .ToArray(),
                Expected(
                    ClassDestinationHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    InterfaceDestinationHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "base class changed",
                stableFiles
                    .Append(
                        SourceFile(
                            "BaseDestination.cs",
                            BuildBaseClassSource("long")))
                    .Append(
                        SourceFile(
                            "IBaseDestination.cs",
                            BuildBaseInterfaceSource("int")))
                    .ToArray(),
                Expected(
                    ClassDestinationHintName,
                    IncrementalStepRunReason.Modified),
                Expected(
                    InterfaceDestinationHintName,
                    IncrementalStepRunReason.Cached)),
            Step(
                "base interface changed",
                stableFiles
                    .Append(
                        SourceFile(
                            "BaseDestination.cs",
                            BuildBaseClassSource("long")))
                    .Append(
                        SourceFile(
                            "IBaseDestination.cs",
                            BuildBaseInterfaceSource("long")))
                    .ToArray(),
                Expected(
                    ClassDestinationHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    InterfaceDestinationHintName,
                    IncrementalStepRunReason.Modified)));
    }

    [Test]
    public void Rebuilds_only_destination_from_changed_reference()
    {
        var initialDestinationAReference = CreateReference(
            "ExternalA",
            BuildReferencedDestinationSource(
                "ExternalA",
                "DestinationA",
                "int"));

        var updatedDestinationAReference = CreateReference(
            "ExternalA",
            BuildReferencedDestinationSource(
                "ExternalA",
                "DestinationA",
                "long"));

        var destinationBReference = CreateReference(
            "ExternalB",
            BuildReferencedDestinationSource(
                "ExternalB",
                "DestinationB",
                "string"));

        var sourceFiles = new[]
        {
            SourceFile(
                "Mapper.cs",
                BuildMapperSource(
                    "ExternalA.DestinationA",
                    "ExternalB.DestinationB"))
        };

        RunAndAssert(
            Step(
                "initial references",
                sourceFiles,
                new MetadataReference[]
                {
                    initialDestinationAReference,
                    destinationBReference
                },
                Expected(
                    "Morphant.TemplateType.ExternalA_DestinationA.g.cs",
                    IncrementalStepRunReason.New),
                Expected(
                    "Morphant.TemplateType.ExternalB_DestinationB.g.cs",
                    IncrementalStepRunReason.New)),
            Step(
                "one reference changed",
                sourceFiles,
                new MetadataReference[]
                {
                    updatedDestinationAReference,
                    destinationBReference
                },
                Expected(
                    "Morphant.TemplateType.ExternalA_DestinationA.g.cs",
                    IncrementalStepRunReason.Modified),
                Expected(
                    "Morphant.TemplateType.ExternalB_DestinationB.g.cs",
                    IncrementalStepRunReason.Cached)));
    }

    private static string BuildMapperSource(
        string firstDestination,
        string secondDestination)
    {
        return MapperSourceTemplate
            .Replace("__FIRST_DESTINATION__", firstDestination)
            .Replace("__SECOND_DESTINATION__", secondDestination);
    }

    private static string BuildDestinationAPartialSource(
        string memberType)
    {
        return DestinationAPartialSourceTemplate.Replace(
            "__MEMBER_TYPE__",
            memberType);
    }

    private static string BuildBaseClassSource(string memberType)
    {
        return BaseClassSourceTemplate.Replace(
            "__MEMBER_TYPE__",
            memberType);
    }

    private static string BuildBaseInterfaceSource(string memberType)
    {
        return BaseInterfaceSourceTemplate.Replace(
            "__MEMBER_TYPE__",
            memberType);
    }

    private static string BuildReferencedDestinationSource(
        string destinationNamespace,
        string destinationType,
        string memberType)
    {
        return ReferencedDestinationSourceTemplate
            .Replace("__NAMESPACE__", destinationNamespace)
            .Replace("__DESTINATION__", destinationType)
            .Replace("__MEMBER_TYPE__", memberType);
    }

    // lang=c#
    private const string MapperSourceTemplate =
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
            builder.Map<Source, __FIRST_DESTINATION__>();
            builder.Map<Source, __SECOND_DESTINATION__>();
        }
    }
}
""";

    // lang=c#
    private const string DestinationADeclarationSource =
"""
namespace TestCase
{
    public sealed partial class DestinationA
    {
    }
}
""";

    // lang=c#
    private const string DestinationAPartialSourceTemplate =
"""
namespace TestCase
{
    public sealed partial class DestinationA
    {
        public __MEMBER_TYPE__ Id { get; set; }
    }
}
""";

    // lang=c#
    private const string DestinationBSource =
"""
namespace TestCase
{
    public sealed class DestinationB
    {
        public string Name { get; set; } = null!;
    }
}
""";

    // lang=c#
    private const string ClassDestinationSource =
"""
namespace TestCase
{
    public sealed class ClassDestination : BaseDestination
    {
    }
}
""";

    // lang=c#
    private const string BaseClassSourceTemplate =
"""
namespace TestCase
{
    public class BaseDestination
    {
        public __MEMBER_TYPE__ Id { get; set; }
    }
}
""";

    // lang=c#
    private const string InterfaceDestinationSource =
"""
namespace TestCase
{
    public interface InterfaceDestination : IBaseDestination
    {
    }
}
""";

    // lang=c#
    private const string BaseInterfaceSourceTemplate =
"""
namespace TestCase
{
    public interface IBaseDestination
    {
        __MEMBER_TYPE__ Id { get; set; }
    }
}
""";

    // lang=c#
    private const string ReferencedDestinationSourceTemplate =
"""
#nullable enable

namespace __NAMESPACE__
{
    public sealed class __DESTINATION__
    {
        public __MEMBER_TYPE__ Value { get; set; } = default!;
    }
}
""";
}
