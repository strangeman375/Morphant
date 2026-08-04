using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeDocumentationTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Documentation;

[TestFixture]
internal sealed class TemplateTypeDocumentationTests
{
    [Test]
    public async Task Uses_inheritdoc_for_documented_destination_type()
    {
        // lang=c#
        const string destinationDocumentation =
"""
    /// <summary>
    /// Represents a destination exposed to API consumers.
    /// </summary>
    /// <remarks>
    /// Carries data produced by a mapping operation.
    /// </remarks>
""";

        // lang=c#
        const string expectedTemplateTypeDocumentation =
"""
    /// <inheritdoc cref="global::TestCase.Destination"/>
""";

        await RunAndAssert(
            destinationDocumentation: destinationDocumentation,
            expectedTemplateTypeDocumentation:
                expectedTemplateTypeDocumentation);
    }

    [Test]
    public async Task Generates_fallback_summary_for_undocumented_destination_type()
    {
        // lang=c#
        const string expectedTemplateTypeDocumentation =
"""
    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::TestCase.Destination"/>.
    /// </summary>
""";

        await RunAndAssert(
            destinationDocumentation: string.Empty,
            expectedTemplateTypeDocumentation:
                expectedTemplateTypeDocumentation);
    }

    [Test]
    public async Task Uses_inheritdoc_for_destination_documented_with_inheritdoc()
    {
        // lang=c#
        const string destinationDocumentation =
"""
    /// <inheritdoc cref="BaseDestination"/>
""";

        // lang=c#
        const string additionalSource =
"""
    public class BaseDestination
    {
    }
""";

        // lang=c#
        const string expectedTemplateTypeDocumentation =
"""
    /// <inheritdoc cref="global::TestCase.Destination"/>
""";

        await RunAndAssert(
            additionalSource: additionalSource,
            destinationDeclaration:
                "public sealed class Destination : BaseDestination",
            destinationDocumentation: destinationDocumentation,
            expectedTemplateTypeDocumentation:
                expectedTemplateTypeDocumentation);
    }
}
