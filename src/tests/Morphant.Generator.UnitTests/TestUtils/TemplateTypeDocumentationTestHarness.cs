namespace Morphant.Generator.UnitTests.TestUtils;

internal static class TemplateTypeDocumentationTestHarness
{
    public static Task RunAndAssert(
        string destinationMembers = "",
        string expectedMembers = "",
        string additionalSource = "",
        string destinationDeclaration = "public sealed class Destination",
        string destinationDocumentation = DefaultDestinationDocumentation,
        string expectedTemplateTypeDocumentation = DefaultExpectedTemplateTypeDocumentation,
        string constructors = ParameterlessDestinationConstructor,
        string constructorMembers = "",
        string expectedConstructors = ExpectedParameterlessTemplateConstructor,
        bool canConstructDestination = true)
    {
        return TemplateTypeTestHarness.RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            additionalSource,
            destinationDeclaration,
            destinationMembers,
            expectedMembers,
            canConstructDestination,
            destinationDocumentation: destinationDocumentation,
            expectedTemplateTypeDocumentation:
                expectedTemplateTypeDocumentation);
    }

    // lang=c#
    private const string DefaultDestinationDocumentation =
"""
    /// <summary>
    /// Represents a destination model.
    /// </summary>
""";

    // lang=c#
    private const string DefaultExpectedTemplateTypeDocumentation =
"""
    /// <inheritdoc cref="global::TestCase.Destination"/>
""";

    // lang=c#
    private const string ParameterlessDestinationConstructor =
"""
        public Destination()
        {
        }
""";

    // lang=c#
    private const string ExpectedParameterlessTemplateConstructor =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }
""";
}
