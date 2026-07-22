using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Constructors;

[TestFixture]
internal sealed class TemplateTypeConstructorUsageTests
{
    [Test]
    public async Task Accepts_raw_values_and_named_arguments()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id, string name)
        {
        }
""";

        // lang=c#
        const string usage =
"""
    public static class Usage
    {
        public static object FromRawValues(int id, string name) =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(id, name);

        public static object FromNamedArguments(int id, string name) =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(name: name, id: id);
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
        /// Configures the <c>name</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> name = null!;
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
            global::Morphant.Members.ConstructorMember<int> id,
            global::Morphant.Members.ConstructorMember<string> name)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            usage);
    }

    [Test]
    public async Task Allows_optional_and_params_arguments_to_be_omitted()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(
            int id,
            bool enabled = true,
            params string[] tags)
        {
        }
""";

        // lang=c#
        const string usage =
"""
    public static class Usage
    {
        public static object RequiredOnly() =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(1);

        public static object WithNamedParams() =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(
                1,
                tags: new[] { "first", "second" });
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
        /// Configures the <c>enabled</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<bool> enabled = null!;

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
        /// <param name="enabled">Configures the <c>enabled</c> constructor argument. If omitted, the destination constructor default value <c>true</c> is used.</param>
        /// <param name="tags">Configures the <c>tags</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> id,
            global::Morphant.Members.ConstructorMember<bool> enabled = null!,
            global::Morphant.Members.ConstructorMember<string[]> tags = null!)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            usage);
    }

    [Test]
    public async Task Resolves_overloads_with_common_required_prefix()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id)
        {
        }

        public Destination(int id, bool enabled = true)
        {
        }
""";

        // lang=c#
        const string usage =
"""
    public static class Usage
    {
        public static object ShortOverload() =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(1);

        public static object LongOverload() =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(1, true);
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
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int> id)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        /// <param name="enabled">Configures the <c>enabled</c> constructor argument. If omitted, the destination constructor default value <c>true</c> is used.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> id,
            global::Morphant.Members.ConstructorMember<bool> enabled = null!)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            usage);
    }

    [Test]
    public async Task Allows_numeric_overloads_to_be_disambiguated()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id)
        {
        }

        public Destination(long id)
        {
        }
""";

        // lang=c#
        const string usage =
"""
    public static class Usage
    {
        // Numeric overloads require an explicit wrapper type because
        // both user-defined conversions are otherwise applicable.
        public static object IntOverload() =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(
                (global::Morphant.Members.ConstructorMember<int>)1);

        public static object LongOverload() =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(
                (global::Morphant.Members.ConstructorMember<long>)1L);
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
        public global::Morphant.Members.ConstructorMember<int> idInt = null!;

        /// <summary>
        /// Configures the <c>id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<long> idLong = null!;
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

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<long> id)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            usage);
    }
}
