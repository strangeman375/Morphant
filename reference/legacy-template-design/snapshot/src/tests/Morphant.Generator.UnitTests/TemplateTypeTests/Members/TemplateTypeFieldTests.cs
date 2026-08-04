using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeFieldTests
{
    [Test]
    public async Task Generates_mutable_instance_fields()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int Id = 0;

        public string? Name = null;

        public volatile int Version = 0;
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
        public global::Morphant.Members.Member<string?>? Name
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Version"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Version
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Skips_static_const_and_readonly_fields()
    {
        // lang=c#
        const string destinationMembers =
"""
        public static int Static = 0;

        public const int Constant = 0;

        public readonly int ReadOnly = 0;
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers: string.Empty);
    }
}
