using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeMemberAccessibilityTests
{
    [Test]
    public async Task Generates_members_accessible_from_generated_code()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int PublicProperty { get; set; }

        internal int InternalProperty { get; set; }

        protected internal int ProtectedInternalProperty { get; set; }

        private int PrivateProperty { get; set; }

        protected int ProtectedProperty { get; set; }

        private protected int PrivateProtectedProperty { get; set; }

        public int PublicField = 0;

        internal int InternalField = 0;

        protected internal int ProtectedInternalField = 0;

        private int PrivateField = 0;

        protected int ProtectedField = 0;

        private protected int PrivateProtectedField = 0;
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.PublicProperty"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> PublicProperty
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.InternalProperty"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> InternalProperty
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.ProtectedInternalProperty"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> ProtectedInternalProperty
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.PublicField"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> PublicField
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.InternalField"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> InternalField
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.ProtectedInternalField"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> ProtectedInternalField
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers,
            destinationDeclaration: "public class Destination");
    }

    [Test]
    public async Task Uses_setter_accessibility_when_selecting_properties()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int PublicSetter { get; set; }

        public int InternalSetter { get; internal set; }

        public int ProtectedInternalSetter
        {
            get;
            protected internal set;
        }

        public int PrivateSetter { get; private set; }

        public int ProtectedSetter { get; protected set; }

        public int PrivateProtectedSetter
        {
            get;
            private protected set;
        }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.PublicSetter"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> PublicSetter
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.InternalSetter"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> InternalSetter
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.ProtectedInternalSetter"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> ProtectedInternalSetter
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers,
            destinationDeclaration: "public class Destination");
    }
}
