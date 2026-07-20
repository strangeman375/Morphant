using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Constructors;

[TestFixture]
internal sealed class TemplateTypeConstructorAccessibilityTests
{
    [Test]
    public async Task Generates_only_constructors_accessible_from_generated_code()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int publicValue)
        {
        }

        internal Destination(string internalValue)
        {
        }

        protected internal Destination(bool protectedInternalValue)
        {
        }

        private Destination(Guid privateValue)
        {
        }

        protected Destination(double protectedValue)
        {
        }

        private protected Destination(decimal privateProtectedValue)
        {
        }
""";

        // lang=c#
        const string constructorMembers =
"""
    /// <summary>
    /// Contains mappings for constructor arguments of <see cref="global::TestCase.Destination"/>.
    /// </summary>
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>publicValue</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> publicValue = null!;

        /// <summary>
        /// Configures the <c>internalValue</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> internalValue = null!;

        /// <summary>
        /// Configures the <c>protectedInternalValue</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<bool> protectedInternalValue = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="publicValue">Configures the <c>publicValue</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int> publicValue)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="internalValue">Configures the <c>internalValue</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<string> internalValue)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="protectedInternalValue">Configures the <c>protectedInternalValue</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<bool> protectedInternalValue)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            destinationDeclaration: "public class Destination");
    }

    [Test]
    public async Task Generates_no_destination_constructors_when_none_are_accessible()
    {
        // lang=c#
        const string constructors =
"""
        private Destination()
        {
        }

        protected Destination(int id)
        {
        }

        private protected Destination(string name)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers: string.Empty,
            expectedConstructors: string.Empty,
            destinationDeclaration: "public class Destination");
    }
}
