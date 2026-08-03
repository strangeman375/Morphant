using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Constructors;

[TestFixture]
internal sealed class TemplateTypeConstructorParametersTests
{
    [Test]
    public async Task Does_not_generate_constructor_members_type_for_parameterless_constructor()
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
            constructorMembers: string.Empty,
            expectedConstructors: expectedConstructors);
    }

    [Test]
    public async Task Collects_unique_parameters_from_all_constructors_in_first_occurrence_order()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id)
        {
        }

        public Destination(int id, string name)
        {
        }

        public Destination(string name, bool enabled)
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

        /// <summary>
        /// Configures the <c>enabled</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<bool> enabled = null!;
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
        /// <param name="name">Configures the <c>name</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorParameter<int> id,
            global::Morphant.Members.ConstructorParameter<string> name)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="name">Configures the <c>name</c> constructor argument.</param>
        /// <param name="enabled">Configures the <c>enabled</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorParameter<string> name,
            global::Morphant.Members.ConstructorParameter<bool> enabled)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Disambiguates_same_parameter_name_by_type()
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

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Disambiguates_user_defined_types_with_same_simple_name()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(First.SomeUserType value)
        {
        }

        public Destination(Second.SomeUserType value)
        {
        }
""";

        // lang=c#
        const string additionalSource =
"""
    namespace First
    {
        public sealed class SomeUserType
        {
        }
    }

    namespace Second
    {
        public sealed class SomeUserType
        {
        }
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
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::TestCase.First.SomeUserType> valueSomeUserType = null!;

        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::TestCase.Second.SomeUserType> valueSomeUserType2 = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<global::TestCase.First.SomeUserType> value)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<global::TestCase.Second.SomeUserType> value)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            additionalSource);
    }

    [Test]
    public async Task Makes_field_names_unique_when_generated_names_collide()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int value)
        {
        }

        public Destination(Guid value)
        {
        }

        public Destination(string valueInt)
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
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<int> valueInt = null!;

        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::System.Guid> valueGuid = null!;

        /// <summary>
        /// Configures the <c>valueInt</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<string> valueInt2 = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<int> value)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<global::System.Guid> value)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="valueInt">Configures the <c>valueInt</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<string> valueInt)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Uses_next_available_numeric_suffix_when_constructor_member_names_collide()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(long valueInt)
        {
        }

        public Destination(string valueInt2)
        {
        }

        public Destination(int value)
        {
        }

        public Destination(Guid value)
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
        /// Configures the <c>valueInt</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<long> valueInt = null!;

        /// <summary>
        /// Configures the <c>valueInt2</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<string> valueInt2 = null!;

        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<int> valueInt3 = null!;

        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::System.Guid> valueGuid = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="valueInt">Configures the <c>valueInt</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<long> valueInt)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="valueInt2">Configures the <c>valueInt2</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<string> valueInt2)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<int> value)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<global::System.Guid> value)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors);
    }

    [Test]
    public async Task Escapes_keyword_constructor_member_name()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int @event)
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
        /// Configures the <c>event</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<int> @event = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="event">Configures the <c>event</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<int> @event)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Treats_constructor_parameter_names_as_case_sensitive()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id)
        {
        }

        public Destination(Guid Id)
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
        /// Configures the <c>Id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::System.Guid> Id = null!;
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
        /// <param name="Id">Configures the <c>Id</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<global::System.Guid> Id)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Deduplicates_same_parameter_type_written_with_different_aliases()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id)
        {
        }

        public Destination(System.Int32 id, string name)
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
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<int> id)
        {
        }

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

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Makes_field_names_unique_when_type_suffixes_collide()
    {
        // lang=c#
        const string constructors =
"""
        public sealed class IntArray
        {
        }

        public Destination(int[] value)
        {
        }

        public Destination(IntArray value)
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
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<int[]> valueIntArray = null!;

        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::TestCase.Destination.IntArray> valueIntArray2 = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<int[]> value)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<global::TestCase.Destination.IntArray> value)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Builds_distinct_field_names_for_array_ranks()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int[] values)
        {
        }

        public Destination(int[,] values)
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
        /// Configures the <c>values</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<int[]> valuesIntArray = null!;

        /// <summary>
        /// Configures the <c>values</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<int[,]> valuesIntArray2 = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="values">Configures the <c>values</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<int[]> values)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="values">Configures the <c>values</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorParameter<int[,]> values)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }
}
