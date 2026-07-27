using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Documentation;

[TestFixture]
internal sealed class TemplateSpecialConstructorDocumentationTests
{
    [Test]
    public async Task Documents_convention_and_factory_constructors()
    {
        // lang=c#
        const string constructors =
"""
        public Destination()
        {
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

        // lang=c#
        const string expectedByConventionConstructor =
"""
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }
""";

        // lang=c#
        const string expectedByFactoryConstructor =
"""
        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.IByFactoryMarker<global::TestCase.Destination> marker)
        {
        }
""";

        await RunAndAssert(
            constructors,
            string.Empty,
            expectedConstructors,
            expectedByConventionConstructor:
                expectedByConventionConstructor,
            expectedByFactoryConstructor:
                expectedByFactoryConstructor);
    }

    [Test]
    public async Task Documents_convention_constructor_members_parameter()
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

        // lang=c#
        const string expectedByConventionConstructor =
"""
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        /// <param name="members">Specifies optional mappings for constructor arguments.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Markers.ByConventionMarker marker,
            DestinationMorphantTemplateConstructorMembers? members = null)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            expectedByConventionConstructor:
                expectedByConventionConstructor);
    }

    [Test]
    public async Task Documents_convention_constructor_for_nonconstructible_destination()
    {
        // lang=c#
        const string expectedByConventionConstructor =
"""
        /// <summary>
        /// Configures convention-based mapping without selecting a destination constructor.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }
""";

        await RunAndAssert(
            constructors: string.Empty,
            constructorMembers: string.Empty,
            expectedConstructors: string.Empty,
            destinationDeclaration: "public abstract class Destination",
            canConstructDestination: false,
            expectedByConventionConstructor:
                expectedByConventionConstructor);
    }
}
