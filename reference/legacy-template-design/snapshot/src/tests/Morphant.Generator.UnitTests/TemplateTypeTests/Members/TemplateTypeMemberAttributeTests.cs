using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeMemberAttributeTests
{
    [Test]
    public async Task Copies_Obsolete_attribute_to_template_members()
    {
        // lang=c#
        const string destinationMembers =
"""
        [Obsolete("Use CurrentProperty instead.")]
        public int LegacyProperty { get; set; }

        [Obsolete("LegacyField was removed.", true)]
        public int LegacyField = 0;
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.LegacyProperty"/>.
        /// </summary>
        [global::System.ObsoleteAttribute("Use CurrentProperty instead.")]
        public global::Morphant.Members.Member<int> LegacyProperty
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.LegacyField"/>.
        /// </summary>
        [global::System.ObsoleteAttribute("LegacyField was removed.", true)]
        public global::Morphant.Members.Member<int> LegacyField
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }
}
