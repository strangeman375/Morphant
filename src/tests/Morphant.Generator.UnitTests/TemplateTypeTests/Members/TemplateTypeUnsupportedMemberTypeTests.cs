using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeUnsupportedMemberTypeTests
{
    [Test]
    public async Task Skips_ref_like_member_type()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int Id { get; set; }

        public Span<int> Buffer
        {
            get => default;
            set { }
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
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }
}
