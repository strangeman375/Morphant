using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class TemplateTypeTestHarness
{
    public static Task RunAndAssert(
        string constructors,
        string constructorMembers,
        string expectedConstructors,
        string additionalSource = "",
        string destinationDeclaration = "public sealed class Destination",
        string destinationMembers = "",
        string expectedMembers = "",
        bool canConstructDestination = true,
        string destinationDocumentation = DefaultDestinationDocumentation,
        string expectedTemplateTypeDocumentation = DefaultExpectedTemplateTypeDocumentation,
        string? expectedByConventionConstructor = null,
        string? expectedByFactoryConstructor = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp9)
    {
        return TemplateTypeGeneratorTest.RunAndAssert(
            languageVersion,
            BuildSource(
                constructors,
                additionalSource,
                destinationDeclaration,
                destinationMembers,
                destinationDocumentation),
            "Morphant.TemplateType.TestCase_Destination.g.cs",
            BuildExpectedSource(
                constructorMembers,
                expectedConstructors,
                expectedMembers,
                canConstructDestination,
                expectedTemplateTypeDocumentation,
                expectedByConventionConstructor,
                expectedByFactoryConstructor));
    }

    private static string BuildSource(
        string constructors,
        string additionalSource,
        string destinationDeclaration,
        string destinationMembers,
        string destinationDocumentation)
    {
        return SourceTemplate
            .Replace(ConstructorPlaceholder, constructors)
            .Replace(DestinationMembersPlaceholder, destinationMembers)
            .Replace(AdditionalSourcePlaceholder, additionalSource)
            .Replace(
                DestinationDeclarationPlaceholder,
                destinationDeclaration)
            .Replace(
                DestinationDocumentationPlaceholder,
                destinationDocumentation);
    }

    private static string BuildExpectedSource(
        string constructorMembers,
        string destinationConstructors,
        string expectedMembers,
        bool canConstructDestination,
        string expectedTemplateTypeDocumentation,
        string? expectedByConventionConstructor,
        string? expectedByFactoryConstructor)
    {
        var hasConstructorMembers =
            !string.IsNullOrEmpty(constructorMembers);

        var builder = new StringBuilder();
        builder.AppendLine(ExpectedFileStart);

        if (hasConstructorMembers)
        {
            builder.AppendLine(constructorMembers);
            builder.AppendLine();
        }

        builder.AppendLine(expectedTemplateTypeDocumentation);
        builder.AppendLine(ExpectedTemplateTypeStart);

        expectedByConventionConstructor ??=
            !canConstructDestination
                ? ExpectedByConventionConstructorWithoutDestinationConstructor
                : hasConstructorMembers
                    ? ExpectedByConventionConstructorWithMembers
                    : ExpectedByConventionConstructorWithoutMembers;

        builder.AppendLine(expectedByConventionConstructor);
        builder.AppendLine();
        builder.AppendLine(
            expectedByFactoryConstructor ??
            ExpectedByFactoryConstructor);

        if (!string.IsNullOrEmpty(destinationConstructors))
        {
            builder.AppendLine();
            builder.AppendLine(destinationConstructors);
        }

        if (!string.IsNullOrEmpty(expectedMembers))
        {
            builder.AppendLine();
            builder.AppendLine(expectedMembers);
        }

        builder.AppendLine();
        builder.AppendLine(ExpectedTemplateTypeEnd);

        return builder.ToString();
    }

    private const string ConstructorPlaceholder =
        "__DESTINATION_CONSTRUCTORS__";

    private const string DestinationMembersPlaceholder =
        "__DESTINATION_MEMBERS__";

    private const string AdditionalSourcePlaceholder =
        "__ADDITIONAL_SOURCE__";

    private const string DestinationDeclarationPlaceholder =
        "__DESTINATION_DECLARATION__";

    private const string DestinationDocumentationPlaceholder =
        "__DESTINATION_DOCUMENTATION__";

    // lang=c#
    private const string SourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

__DESTINATION_DOCUMENTATION__
    __DESTINATION_DECLARATION__
    {
__DESTINATION_CONSTRUCTORS__

__DESTINATION_MEMBERS__
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }

__ADDITIONAL_SOURCE__
}
""";

    // lang=c#
    private const string DefaultDestinationDocumentation =
"""
    /// <summary>
    /// Represents a destination model.
    /// </summary>
""";

    // lang=c#
    private const string ExpectedFileStart =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated
{
""";

    // lang=c#
    private const string DefaultExpectedTemplateTypeDocumentation =
"""
    /// <inheritdoc cref="global::TestCase.Destination"/>
""";

    // lang=c#
    private const string ExpectedTemplateTypeStart =
"""
    internal sealed record DestinationMorphantTemplate
    {
""";

    // lang=c#
    private const string ExpectedByConventionConstructorWithoutMembers =
"""
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }
""";

    // lang=c#
    private const string ExpectedByConventionConstructorWithoutDestinationConstructor =
"""
        /// <summary>
        /// Configures convention-based mapping without selecting a destination constructor.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }
""";

    // lang=c#
    private const string ExpectedByConventionConstructorWithMembers =
"""
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        /// <param name="members">Specifies optional mappings for constructor arguments.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Markers.ByConventionMarker marker,
            DestinationMorphantTemplateConstructorMembers? members = null)
        {
        }
""";

    // lang=c#
    private const string ExpectedByFactoryConstructor =
"""
        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Destination> marker)
        {
        }
""";

    // lang=c#
    private const string ExpectedTemplateTypeEnd =
"""
        public bool Equals(DestinationMorphantTemplate? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";
}
