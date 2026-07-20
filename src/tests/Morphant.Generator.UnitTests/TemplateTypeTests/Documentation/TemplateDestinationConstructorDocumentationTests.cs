using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Documentation;

[TestFixture]
internal sealed class TemplateDestinationConstructorDocumentationTests
{
    [Test]
    public async Task Documents_parameterless_destination_constructor()
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

        await RunAndAssert(
            constructors,
            string.Empty,
            expectedConstructors);
    }

    [Test]
    public async Task Documents_required_destination_constructor_parameters()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id, string @event)
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

        /// <summary>
        /// Configures the <c>event</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> @event = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        /// <param name="event">Configures the <c>event</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> id,
            global::Morphant.Members.ConstructorMember<string> @event)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors);
    }

    [Test]
    public async Task Documents_optional_default_values_and_escapes_xml()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(
            string text = "A < B & C",
            char separator = '<')
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
        /// Configures the <c>text</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> text = null!;

        /// <summary>
        /// Configures the <c>separator</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<char> separator = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="text">Configures the <c>text</c> constructor argument. If omitted, the destination constructor default value <c>"A &lt; B &amp; C"</c> is used.</param>
        /// <param name="separator">Configures the <c>separator</c> constructor argument. If omitted, the destination constructor default value <c>'&lt;'</c> is used.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<string>? text = null,
            global::Morphant.Members.ConstructorMember<char>? separator = null)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors);
    }

    [Test]
    public async Task Documents_params_parameter_without_claiming_a_default_value()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(params int[] values)
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
        /// Configures the <c>values</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int[]> values = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="values">Configures the <c>values</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int[]>? values = null)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors);
    }
}
