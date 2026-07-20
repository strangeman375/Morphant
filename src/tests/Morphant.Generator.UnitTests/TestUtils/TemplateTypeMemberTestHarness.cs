namespace Morphant.Generator.UnitTests.TestUtils;

internal static class TemplateTypeMemberTestHarness
{
    public static Task RunAndAssert(
        string destinationMembers,
        string expectedMembers,
        string additionalSource = "",
        string destinationDeclaration = "public sealed class Destination")
    {
        return TemplateTypeTestHarness.RunAndAssert(
            constructors: ParameterlessDestinationConstructor,
            constructorMembers: string.Empty,
            expectedConstructors: ExpectedParameterlessTemplateConstructor,
            additionalSource: additionalSource,
            destinationDeclaration: destinationDeclaration,
            destinationMembers: destinationMembers,
            expectedMembers: expectedMembers);
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
