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
    public async Task Generates_required_nullable_and_optional_parameters()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(
            int id,
            string? displayName,
            DateTime? createdAt,
            bool enabled = true,
            string mode = "default")
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
        /// Configures the <c>displayName</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string?> displayName = null!;

        /// <summary>
        /// Configures the <c>createdAt</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<global::System.DateTime?> createdAt = null!;

        /// <summary>
        /// Configures the <c>enabled</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<bool> enabled = null!;

        /// <summary>
        /// Configures the <c>mode</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> mode = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        /// <param name="displayName">Configures the <c>displayName</c> constructor argument.</param>
        /// <param name="createdAt">Configures the <c>createdAt</c> constructor argument.</param>
        /// <param name="enabled">Configures the <c>enabled</c> constructor argument. If omitted, the destination constructor default value <c>true</c> is used.</param>
        /// <param name="mode">Configures the <c>mode</c> constructor argument. If omitted, the destination constructor default value <c>"default"</c> is used.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> id,
            global::Morphant.Members.ConstructorMember<string?> displayName,
            global::Morphant.Members.ConstructorMember<global::System.DateTime?> createdAt,
            global::Morphant.Members.ConstructorMember<bool>? enabled = null,
            global::Morphant.Members.ConstructorMember<string>? mode = null)
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
    public async Task Treats_params_parameter_as_optional()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(
            int id,
            params string[] tags)
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
        /// Configures the <c>tags</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string[]> tags = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        /// <param name="tags">Configures the <c>tags</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> id,
            global::Morphant.Members.ConstructorMember<string[]>? tags = null)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Skips_constructors_with_by_reference_parameters()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(ref int id)
        {
        }

        public Destination(in string name)
        {
        }

        public Destination(out bool enabled)
        {
            enabled = false;
        }
""";

        await RunAndAssert(constructors, constructorMembers: string.Empty, expectedConstructors: string.Empty);
    }
}
