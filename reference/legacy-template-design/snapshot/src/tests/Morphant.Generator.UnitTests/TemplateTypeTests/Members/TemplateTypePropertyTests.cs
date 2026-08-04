using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypePropertyTests
{
    [Test]
    public async Task Generates_assignable_properties()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int Id { get; set; }

        public string Name { get; init; } = null!;

        public bool Enabled { private get; set; }

        public int WriteOnly
        {
            set
            {
            }
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

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Enabled"/>.
        /// </summary>
        public global::Morphant.Members.Member<bool> Enabled
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.WriteOnly"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> WriteOnly
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Skips_properties_without_accessible_setter()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int ReadOnly { get; }

        public int PrivateSetter { get; private set; }

        public int ProtectedSetter { get; protected set; }

        public int PrivateProtectedSetter
        {
            get;
            private protected set;
        }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers: string.Empty,
            destinationDeclaration: "public class Destination");
    }

    [Test]
    public async Task Skips_static_properties_and_indexers()
    {
        // lang=c#
        const string destinationMembers =
"""
        public static int Static { get; set; }

        public int this[int index]
        {
            get => index;
            set
            {
            }
        }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers: string.Empty);
    }
}
