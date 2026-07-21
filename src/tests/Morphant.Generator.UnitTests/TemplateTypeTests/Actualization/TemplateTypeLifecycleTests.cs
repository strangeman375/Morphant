using Expected = Morphant.Generator.UnitTests.TestUtils.TemplateTypeActualizationExpectedSource;
using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeActualizationTest;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Actualization;

[TestFixture]
internal sealed class TemplateTypeLifecycleTests
{
    [Test]
    public void Adds_template_after_first_map_and_removes_it_after_last_map()
    {
        const string hintName =
            "Morphant.TemplateType.TestCase_Destination.g.cs";

        var expected = Expected.Build(
            destinationConstructors:
                BuildParameterlessConstructor(
                    "DestinationMorphantTemplate"));

        RunAndAssert(
            Step(
                "without map",
                BuildUsageSource(
                    string.Empty,
                    string.Empty)),
            Step(
                "first map added",
                BuildUsageSource(
                    "            builder.Map<Source, Destination>();",
                    string.Empty),
                (hintName, expected)),
            Step(
                "map in second mapper added",
                BuildUsageSource(
                    "            builder.Map<Source, Destination>();",
                    "            builder.Map<AlternativeSource, Destination>();"),
                (hintName, expected)),
            Step(
                "map in second mapper remains",
                BuildUsageSource(
                    string.Empty,
                    "            builder.Map<AlternativeSource, Destination>();"),
                (hintName, expected)),
            Step(
                "last map removed",
                BuildUsageSource(
                    string.Empty,
                    string.Empty)));
    }

    [Test]
    public void Replaces_template_when_map_destination_changes()
    {
        // lang=c#
        const string customerMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.CustomerDestination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        // lang=c#
        const string orderMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.OrderDestination.Number"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> Number
        {
            get => null!;
            set { }
        }
""";

        var customerExpected = Expected.Build(
            templateTypeName:
                "CustomerDestinationMorphantTemplate",
            templateTypeReference:
                "CustomerDestinationMorphantTemplate",
            destinationTypeName:
                "global::TestCase.CustomerDestination",
            destinationConstructors:
                BuildParameterlessConstructor(
                    "CustomerDestinationMorphantTemplate"),
            members: customerMember);

        var orderExpected = Expected.Build(
            templateTypeName:
                "OrderDestinationMorphantTemplate",
            templateTypeReference:
                "OrderDestinationMorphantTemplate",
            destinationTypeName:
                "global::TestCase.OrderDestination",
            destinationConstructors:
                BuildParameterlessConstructor(
                    "OrderDestinationMorphantTemplate"),
            members: orderMember);

        RunAndAssert(
            Step(
                "customer destination",
                BuildReplacementSource("CustomerDestination"),
                (
                    "Morphant.TemplateType." +
                    "TestCase_CustomerDestination.g.cs",
                    customerExpected
                )),
            Step(
                "order destination",
                BuildReplacementSource("OrderDestination"),
                (
                    "Morphant.TemplateType." +
                    "TestCase_OrderDestination.g.cs",
                    orderExpected
                )));
    }

    [Test]
    public void Removes_template_for_direct_destination_and_restores_it_when_switched_back()
    {
        const string hintName =
            "Morphant.TemplateType.TestCase_Destination.g.cs";

        var expected = Expected.Build(
            destinationConstructors:
                BuildParameterlessConstructor(
                    "DestinationMorphantTemplate"));

        RunAndAssert(
            Step(
                "generated destination",
                BuildDirectTransitionSource("Destination"),
                (hintName, expected)),
            Step(
                "direct destination",
                BuildDirectTransitionSource("int")),
            Step(
                "generated destination restored",
                BuildDirectTransitionSource("Destination"),
                (hintName, expected)));
    }

    [Test]
    public void Moves_template_when_destination_is_renamed_and_moved()
    {
        var initialExpected = Expected.Build(
            templateNamespace:
                "OldModels.Morphant.Generated",
            templateTypeName:
                "DestinationMorphantTemplate",
            templateTypeReference:
                "DestinationMorphantTemplate",
            destinationTypeName:
                "global::OldModels.Destination",
            destinationConstructors:
                BuildParameterlessConstructor(
                    "DestinationMorphantTemplate"));

        var updatedExpected = Expected.Build(
            templateNamespace:
                "NewModels.Morphant.Generated",
            templateTypeName:
                "RenamedDestinationMorphantTemplate",
            templateTypeReference:
                "RenamedDestinationMorphantTemplate",
            destinationTypeName:
                "global::NewModels.RenamedDestination",
            destinationConstructors:
                BuildParameterlessConstructor(
                    "RenamedDestinationMorphantTemplate"));

        RunAndAssert(
            Step(
                "initial name and namespace",
                BuildRenamedDestinationSource(
                    "OldModels",
                    "Destination"),
                (
                    "Morphant.TemplateType." +
                    "OldModels_Destination.g.cs",
                    initialExpected
                )),
            Step(
                "renamed and moved destination",
                BuildRenamedDestinationSource(
                    "NewModels",
                    "RenamedDestination"),
                (
                    "Morphant.TemplateType." +
                    "NewModels_RenamedDestination.g.cs",
                    updatedExpected
                )));
    }

    private static string BuildUsageSource(
        string firstMapperMapStatement,
        string secondMapperMapStatement)
    {
        return UsageSourceTemplate
            .Replace(
                "__FIRST_MAPPER_MAP_STATEMENT__",
                firstMapperMapStatement)
            .Replace(
                "__SECOND_MAPPER_MAP_STATEMENT__",
                secondMapperMapStatement);
    }

    private static string BuildReplacementSource(
        string mappedDestinationType)
    {
        return ReplacementSourceTemplate.Replace(
            "__MAPPED_DESTINATION__",
            mappedDestinationType);
    }

    private static string BuildDirectTransitionSource(
        string mappedDestinationType)
    {
        return DirectTransitionSourceTemplate.Replace(
            "__MAPPED_DESTINATION__",
            mappedDestinationType);
    }

    private static string BuildRenamedDestinationSource(
        string destinationNamespace,
        string destinationTypeName)
    {
        return RenamedDestinationSourceTemplate
            .Replace(
                "__DESTINATION_NAMESPACE__",
                destinationNamespace)
            .Replace(
                "__DESTINATION_TYPE__",
                destinationTypeName);
    }

    private static string BuildParameterlessConstructor(
        string templateTypeName)
    {
        return
$$"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public {{templateTypeName}}()
        {
        }
""";
    }

    // lang=c#
    private const string UsageSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class AlternativeSource
    {
    }

    public sealed class Destination
    {
    }

    [MorphantMapper]
    public partial class FirstMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
__FIRST_MAPPER_MAP_STATEMENT__
        }
    }

    [MorphantMapper]
    public partial class SecondMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
__SECOND_MAPPER_MAP_STATEMENT__
        }
    }
}
""";

    // lang=c#
    private const string DirectTransitionSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, __MAPPED_DESTINATION__>();
        }
    }
}
""";

    // lang=c#
    private const string ReplacementSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class CustomerDestination
    {
        public int Id { get; set; }
    }

    public sealed class OrderDestination
    {
        public string Number { get; set; } = null!;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, __MAPPED_DESTINATION__>();
        }
    }
}
""";

    // lang=c#
    private const string RenamedDestinationSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace __DESTINATION_NAMESPACE__
{
    public sealed class __DESTINATION_TYPE__
    {
    }
}

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
            builder.Map<
                Source,
                __DESTINATION_NAMESPACE__.__DESTINATION_TYPE__>();
        }
    }
}
""";
}
