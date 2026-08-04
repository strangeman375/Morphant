using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Documentation;

[TestFixture]
internal sealed class TemplateConstructorParametersDocumentationTests
{
    [Test]
    public async Task Documents_constructor_members_type_and_fields()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id, string name)
        {
        }
""";

        // lang=c#
        const string constructorMembers =
"""
    /// <summary>
    /// Contains mappings for constructor arguments of <see cref="global::TestCase.Destination"/>.
    /// </summary>
    internal sealed class DestinationMorphantTemplateConstructorParameters
    {
        /// <summary>
        /// Configures the <c>id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<int> id = null!;

        /// <summary>
        /// Configures the <c>name</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<string> name = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        /// <param name="name">Configures the <c>name</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorParameter<int> id,
            global::Morphant.Members.ConstructorParameter<string> name)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors);
    }

    [Test]
    public async Task Uses_original_parameter_name_when_field_name_is_disambiguated()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id)
        {
        }

        public Destination(Guid id)
        {
        }
""";

        // lang=c#
        const string constructorMembers =
"""
    /// <summary>
    /// Contains mappings for constructor arguments of <see cref="global::TestCase.Destination"/>.
    /// </summary>
    internal sealed class DestinationMorphantTemplateConstructorParameters
    {
        /// <summary>
        /// Configures the <c>id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<int> idInt = null!;

        /// <summary>
        /// Configures the <c>id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::System.Guid> idGuid = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<int> id)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<global::System.Guid> id)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors);
    }
}
