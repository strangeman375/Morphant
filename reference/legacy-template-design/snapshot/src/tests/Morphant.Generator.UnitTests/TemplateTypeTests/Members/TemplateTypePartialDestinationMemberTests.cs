using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypePartialDestinationMemberTests
{
    [Test]
    public async Task Generates_members_declared_in_all_partial_declarations()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int Id { get; set; }
""";

        // lang=c#
        const string additionalSource =
"""
    public sealed partial class Destination
    {
        public string Name { get; set; } = null!;
    }
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

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Name"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> Name
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers,
            additionalSource,
            destinationDeclaration:
                "public sealed partial class Destination");
    }
}
