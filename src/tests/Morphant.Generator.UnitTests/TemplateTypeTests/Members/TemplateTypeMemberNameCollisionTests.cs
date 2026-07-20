using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeMemberNameCollisionTests
{
    [Test]
    public async Task Skips_members_whose_names_conflict_with_template_record()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int Id { get; set; }

        public int Clone { get; set; }

        public int EqualityContract { get; set; }

        public int PrintMembers { get; set; }

        public new int Equals { get; set; }

        public new int GetHashCode { get; set; }

        public new int ToString { get; set; }

        public int DestinationMorphantTemplate { get; set; }
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

        await RunAndAssert(destinationMembers, expectedMembers);
    }
}
