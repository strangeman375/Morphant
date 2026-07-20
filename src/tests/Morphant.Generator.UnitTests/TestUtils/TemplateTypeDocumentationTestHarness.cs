namespace Morphant.Generator.UnitTests.TestUtils;

internal static class TemplateTypeDocumentationTestHarness
{
    public static Task RunAndAssert(
        string destinationMembers = "",
        string expectedMembers = "",
        string additionalSource = "",
        string destinationDeclaration = "public sealed class Destination",
        string destinationDocumentation = DefaultDestinationDocumentation,
        string expectedTemplateTypeDocumentation = DefaultExpectedTemplateTypeDocumentation)
    {
        return TemplateTypeTestHarness.RunAndAssert(
            ParameterlessDestinationConstructor,
            string.Empty,
            ExpectedParameterlessTemplateConstructor,
            additionalSource,
            destinationDeclaration,
            destinationMembers,
            expectedMembers,
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
