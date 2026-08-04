using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeNonConstructibleDestinationMemberTests
{
    [Test]
    public async Task Generates_members_for_abstract_destination()
    {
        // lang=c#
        const string destinationMembers =
"""
        public abstract int Id { get; set; }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers,
            destinationDeclaration: "public abstract class Destination",
            canConstructDestination: false);
    }
}
