using Microsoft.CodeAnalysis;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class DescriptorTests
{
    private const string HelpLinkBase =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

    public static IEnumerable<TestCaseData> Descriptors()
    {
        yield return Case(
            MappingRegistrationDiagnosticDescriptors
                .UnavailableMappingType,
            "MORPH0011",
            "Mapping type is inaccessible",
            "The {0} type '{1}' is not accessible to the generated mapper.");
        yield return Case(
            MappingRegistrationDiagnosticDescriptors
                .UnsupportedMappingRoot,
            "MORPH0012",
            "Unsupported mapping type",
            "The {0} type '{1}' cannot be used in Map because it is {2}.");
        yield return Case(
            MappingRegistrationDiagnosticDescriptors.DuplicateRegistration,
            "MORPH0013",
            "Duplicate mapping registration",
            "Mapping '{0}' is registered more than once in mapper " +
            "'{1}'.");
        yield return Case(
            MappingRegistrationDiagnosticDescriptors.UnifiableContracts,
            "MORPH0014",
            "Mappings may become identical",
            "Mappings '{0}' and '{1}' may become identical for some generic " +
            "type arguments in mapper '{2}'.");
        yield return Case(
            MappingRegistrationDiagnosticDescriptors
                .ConflictingTuplePresentation,
            "MORPH0056",
            "Tuple presentation is conflicting",
            "Mapping '{0}' uses tuple presentation '{1}', which conflicts " +
            "with the presentation '{2}' of the same underlying mapping " +
            "pair.");
        yield return Case(
            MappingRegistrationDiagnosticDescriptors
                .MapperFamilyParameterMissingFromPair,
            "MORPH0060",
            "Mapper family parameter is absent from mapping",
            "Mapper family type parameter '{0}' must occur in the source " +
            "or destination type of mapping '{1}'.");
    }

    [TestCaseSource(nameof(Descriptors))]
    public void Registration_descriptor_matches_the_public_contract(
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
                Is.EqualTo("Morphant.Registration"));
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
