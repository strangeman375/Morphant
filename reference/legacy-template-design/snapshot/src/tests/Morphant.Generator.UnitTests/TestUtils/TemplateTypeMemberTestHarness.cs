namespace Morphant.Generator.UnitTests.TestUtils;

internal static class TemplateTypeMemberTestHarness
{
    public static Task RunAndAssert(
        string destinationMembers,
        string expectedMembers,
        string additionalSource = "",
        string destinationDeclaration = "public sealed class Destination",
        bool canConstructDestination = true)
    {
        return TemplateTypeTestHarness.RunAndAssert(
            constructors: canConstructDestination
                ? ParameterlessDestinationConstructor
                : string.Empty,
            constructorMembers: string.Empty,
            expectedConstructors: canConstructDestination
                ? ExpectedParameterlessTemplateConstructor
                : string.Empty,
            additionalSource: additionalSource,
            destinationDeclaration: destinationDeclaration,
            destinationMembers: destinationMembers,
            expectedMembers: expectedMembers,
            canConstructDestination: canConstructDestination);
    }

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
