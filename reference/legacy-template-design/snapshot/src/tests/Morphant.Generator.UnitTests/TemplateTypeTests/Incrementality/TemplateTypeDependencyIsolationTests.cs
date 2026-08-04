using Microsoft.CodeAnalysis;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Incrementality;

[TestFixture]
internal sealed class TemplateTypeDependencyIsolationTests
{
    private const string DestinationAHintName =
        "Morphant.Generated.TemplateType.TestCase_DestinationA.g.cs";

    private const string DestinationBHintName =
        "Morphant.Generated.TemplateType.TestCase_DestinationB.g.cs";

    private const string ClassDestinationHintName =
        "Morphant.Generated.TemplateType.TestCase_ClassDestination.g.cs";

    private const string InterfaceDestinationHintName =
        "Morphant.Generated.TemplateType.TestCase_InterfaceDestination.g.cs";

    private const string NestedDestinationHintName =
        "Morphant.Generated.TemplateType." +
        "TestCase_Outer_1_NestedDestination.g.cs";

    [Test]
    public void Isolates_destinations_and_unrelated_declarations_in_same_file()
    {
        var mapperFile = SourceFile(
            "Mapper.cs",
            BuildMapperSource(
                "TestCase.DestinationA",
                "TestCase.DestinationB"));

        RunAndAssert(
            Step(
                "initial shared file",
                new[]
                {
                    mapperFile,
                    SourceFile(
                        "Destinations.cs",
                        BuildSharedDestinationSource("int", 1))
                },
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "unrelated declaration changed in shared file",
                new[]
                {
                    mapperFile,
                    SourceFile(
                        "Destinations.cs",
                        BuildSharedDestinationSource("int", 2))
                },
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Cached)),
            Step(
                "one destination changed in shared file",
                new[]
                {
                    mapperFile,
                    SourceFile(
                        "Destinations.cs",
                        BuildSharedDestinationSource("long", 2))
                },
                Expected(
                    DestinationAHintName,
                    IncrementalStepRunReason.Modified),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Cached)));
    }

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
    public void Rebuilds_nested_destination_when_containing_contract_changes()
    {
        var mapperFile = SourceFile(
            "Mapper.cs",
            BuildMapperSource(
                "TestCase.Outer<string>.NestedDestination",
                "TestCase.DestinationB"));

        var destinationBFile = SourceFile(
            "DestinationB.cs",
            DestinationBSource);

        RunAndAssert(
            Step(
                "initial containing contract",
                new[]
                {
                    mapperFile,
                    SourceFile(
                        "Outer.cs",
                        BuildNestedDestinationSource("class")),
                    destinationBFile
                },
                Expected(
                    NestedDestinationHintName,
                    IncrementalStepRunReason.New),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "containing contract changed",
                new[]
                {
                    mapperFile,
                    SourceFile(
                        "Outer.cs",
                        BuildNestedDestinationSource("notnull")),
                    destinationBFile
                },
                Expected(
                    NestedDestinationHintName,
                    IncrementalStepRunReason.Modified),
                Expected(
                    DestinationBHintName,
                    IncrementalStepRunReason.Cached)));
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
                    "Morphant.Generated.TemplateType.ExternalA_DestinationA.g.cs",
                    IncrementalStepRunReason.New),
                Expected(
                    "Morphant.Generated.TemplateType.ExternalB_DestinationB.g.cs",
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
                    "Morphant.Generated.TemplateType.ExternalA_DestinationA.g.cs",
                    IncrementalStepRunReason.Modified),
                Expected(
                    "Morphant.Generated.TemplateType.ExternalB_DestinationB.g.cs",
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

    private static string BuildSharedDestinationSource(
        string destinationAMemberType,
        int unrelatedVersion)
    {
        return SharedDestinationSourceTemplate
            .Replace(
                "__DESTINATION_A_MEMBER_TYPE__",
                destinationAMemberType)
            .Replace(
                "__UNRELATED_VERSION__",
                unrelatedVersion.ToString());
    }

    private static string BuildNestedDestinationSource(
        string typeParameterConstraint)
    {
        return NestedDestinationSourceTemplate.Replace(
            "__TYPE_PARAMETER_CONSTRAINT__",
            typeParameterConstraint);
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
    private const string SharedDestinationSourceTemplate =
"""
namespace TestCase
{
    public sealed class DestinationA
    {
        public __DESTINATION_A_MEMBER_TYPE__ Id { get; set; }
    }

    public sealed class DestinationB
    {
        public string Name { get; set; } = null!;
    }

    internal static class Unrelated
    {
        public static int GetVersion() => __UNRELATED_VERSION__;
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
    public sealed class ClassDestination : IntermediateDestination
    {
    }

    public class IntermediateDestination : BaseDestination
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
    public interface InterfaceDestination : IIntermediateDestination
    {
    }

    public interface IIntermediateDestination : IBaseDestination
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
    private const string NestedDestinationSourceTemplate =
"""
#nullable enable

namespace TestCase
{
    public sealed class Outer<T>
        where T : __TYPE_PARAMETER_CONSTRAINT__
    {
        public sealed class NestedDestination
        {
            public T Value { get; set; } = default!;
        }
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
