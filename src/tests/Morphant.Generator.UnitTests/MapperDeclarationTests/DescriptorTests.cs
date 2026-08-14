using Microsoft.CodeAnalysis;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.UnitTests.MapperDeclarationTests;

[TestFixture]
internal sealed class DescriptorTests
{
    private const string HelpLinkBase =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

    public static IEnumerable<TestCaseData> Descriptors()
    {
        yield return Case(
            MapperDeclarationDiagnosticDescriptors.MissingTypeMapperBase,
            "MORPH0005",
            "Mapper must derive from TypeMapper",
            "Mapper '{0}' must derive from 'Morphant.TypeMapper'.");
        yield return Case(
            MapperDeclarationDiagnosticDescriptors.MapperMustBePartial,
            "MORPH0006",
            "Mapper must be partial",
            "Mapper '{0}' must be declared partial.");
        yield return Case(
            MapperDeclarationDiagnosticDescriptors.ContainingTypeMustBePartial,
            "MORPH0007",
            "Containing type must be partial",
            "Containing type '{0}' must be declared partial.");
        yield return Case(
            MapperDeclarationDiagnosticDescriptors.FileLocalType,
            "MORPH0008",
            "File-local mapper declaration is not supported",
            "File-local type '{0}' cannot declare or contain a Morphant mapper.");
        yield return Case(
            MapperDeclarationDiagnosticDescriptors.ExactContract,
            "MORPH0009",
            "Mapping is already implemented",
            "Mapping '{0}' is already implemented by mapper '{1}'. " +
            "Remove the interface declaration or the Map registration.");
        yield return Case(
            MapperDeclarationDiagnosticDescriptors.UnifiableContract,
            "MORPH0010",
            "Mapping may conflict with a declared interface",
            "Mapper '{1}' declares an interface that may conflict with " +
            "generated mapping '{0}'.");
        yield return Case(
            MapperDeclarationDiagnosticDescriptors.SupportsConflict,
            "MORPH0034",
            "Mapper member conflicts with generated Supports",
            "Mapper '{0}' declares 'Supports(System.Type, System.Type)', " +
            "which conflicts with the generated mapper.");
    }

    [TestCaseSource(nameof(Descriptors))]
    public void Declaration_descriptor_matches_the_public_contract(
        DiagnosticDescriptor descriptor,
        string id,
        string title,
        string message)
    {
        Assert.Multiple(() =>
        {
            Assert.That(descriptor.Id, Is.EqualTo(id));
            Assert.That(descriptor.Title.ToString(), Is.EqualTo(title));
            Assert.That(
                descriptor.MessageFormat.ToString(),
                Is.EqualTo(message));
            Assert.That(
                descriptor.Category,
                Is.EqualTo("Morphant.Declaration"));
            Assert.That(
                descriptor.DefaultSeverity,
                Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(descriptor.IsEnabledByDefault, Is.True);
            Assert.That(descriptor.Description.ToString(), Is.Empty);
            Assert.That(
                descriptor.HelpLinkUri,
                Is.EqualTo(HelpLinkBase + id + ".md"));
            Assert.That(descriptor.CustomTags, Is.Empty);
            Assert.That(
                descriptor.CustomTags,
                Does.Not.Contain(WellKnownDiagnosticTags.NotConfigurable));
        });
    }

    private static TestCaseData Case(
        DiagnosticDescriptor descriptor,
        string id,
        string title,
        string message)
    {
        return new TestCaseData(descriptor, id, title, message)
            .SetName(id + "_descriptor");
    }
}
