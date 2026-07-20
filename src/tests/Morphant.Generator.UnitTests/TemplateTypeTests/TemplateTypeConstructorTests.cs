using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeConstructorTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests;

[TestFixture]
internal sealed class TemplateTypeConstructorTests
{
    [Test]
    public async Task Generates_constructor_with_single_required_parameter()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id)
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
        /// Configures the <c>id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> id = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int> id)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Preserves_destination_constructor_declaration_order()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(string name)
        {
        }

        public Destination()
        {
        }

        public Destination(bool enabled)
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
        /// Configures the <c>name</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> name = null!;

        /// <summary>
        /// Configures the <c>enabled</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<bool> enabled = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="name">Configures the <c>name</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<string> name)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="enabled">Configures the <c>enabled</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<bool> enabled)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Does_not_generate_inherited_constructors()
    {
        // lang=c#
        const string additionalSource =
"""
    public class DestinationBase
    {
        public DestinationBase()
        {
        }

        public DestinationBase(int id)
        {
        }
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }
""";

        await RunAndAssert(
            constructors: string.Empty,
            constructorMembers: string.Empty,
            expectedConstructors: expectedConstructors,
            usage: additionalSource,
            destinationDeclaration:
                "public sealed class Destination : DestinationBase");
    }
}
