using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateTypeTests;

[TestFixture]
internal sealed class TemplateTypeConstructorMembersTests
{
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
        /// <param name="name">Configures the <c>name</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> id,
            global::Morphant.Members.ConstructorMember<string> name)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="name">Configures the <c>name</c> constructor argument.</param>
        /// <param name="enabled">Configures the <c>enabled</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<string> name,
            global::Morphant.Members.ConstructorMember<bool> enabled)
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
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> idInt = null!;

        /// <summary>
        /// Configures the <c>id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<global::System.Guid> idGuid = null!;
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
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<global::System.Guid> id)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
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
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> valueInt = null!;

        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<global::System.Guid> valueGuid = null!;

        /// <summary>
        /// Configures the <c>valueInt</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> valueInt2 = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int> value)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<global::System.Guid> value)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="valueInt">Configures the <c>valueInt</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<string> valueInt)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
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
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>event</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> @event = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="event">Configures the <c>event</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int> @event)
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
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> id = null!;

        /// <summary>
        /// Configures the <c>Id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<global::System.Guid> Id = null!;
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
        /// <param name="Id">Configures the <c>Id</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<global::System.Guid> Id)
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
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int> id)
        {
        }

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
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int[]> valueIntArray = null!;

        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<global::TestCase.Destination.IntArray> valueIntArray2 = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int[]> value)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<global::TestCase.Destination.IntArray> value)
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
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>values</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int[]> valuesIntArray = null!;

        /// <summary>
        /// Configures the <c>values</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int[,]> valuesIntArray2 = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="values">Configures the <c>values</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int[]> values)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="values">Configures the <c>values</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int[,]> values)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    private static Task RunAndAssert(
        string constructors,
        string constructorMembers,
        string expectedConstructors)
    {
        return TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(constructors),
            "Morphant.TemplateType.TestCase_Destination.g.cs",
            BuildExpectedSource(constructorMembers, expectedConstructors));
    }

    private static string BuildSource(string constructors)
    {
        return SourceTemplate.Replace(ConstructorPlaceholder, constructors);
    }

    private static string BuildExpectedSource(
        string constructorMembers,
        string destinationConstructors)
    {
        var builder = new StringBuilder();
        builder.AppendLine(ExpectedFileStart);
        builder.AppendLine(constructorMembers);
        builder.AppendLine();
        builder.AppendLine(ExpectedTemplateTypeStart);
        builder.AppendLine(ExpectedByConventionConstructor);
        builder.AppendLine();
        builder.AppendLine(ExpectedByFactoryConstructor);
        builder.AppendLine();
        builder.AppendLine(destinationConstructors);
        builder.AppendLine();
        builder.AppendLine(ExpectedTemplateTypeEnd);

        return builder.ToString();
    }

    private const string ConstructorPlaceholder = "__DESTINATION_CONSTRUCTORS__";

    // lang=c#
    private const string SourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    /// <summary>
    /// Represents a destination model.
    /// </summary>
    public sealed class Destination
    {
__DESTINATION_CONSTRUCTORS__
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}
""";

    // lang=c#
    private const string ExpectedFileStart =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated
{
""";

    // lang=c#
    private const string ExpectedTemplateTypeStart =
"""
    /// <inheritdoc cref="global::TestCase.Destination"/>
    internal sealed record DestinationMorphantTemplate
    {
""";

    // lang=c#
    private const string ExpectedByConventionConstructor =
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

    // lang=c#
    private const string ExpectedByFactoryConstructor =
"""
        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Destination> marker)
        {
        }
""";

    // lang=c#
    private const string ExpectedTemplateTypeEnd =
"""
        public bool Equals(DestinationMorphantTemplate? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";
}
