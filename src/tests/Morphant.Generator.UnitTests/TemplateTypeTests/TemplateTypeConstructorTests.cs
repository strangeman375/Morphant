using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

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
        public global::Morphant.Members.ConstructorMember<int> id = default!;
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
        public global::Morphant.Members.ConstructorMember<int> id = default!;

        /// <summary>
        /// Configures the <c>displayName</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string?> displayName = default!;

        /// <summary>
        /// Configures the <c>createdAt</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<global::System.DateTime?> createdAt = default!;

        /// <summary>
        /// Configures the <c>enabled</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<bool> enabled = default!;

        /// <summary>
        /// Configures the <c>mode</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> mode = default!;
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
        public global::Morphant.Members.ConstructorMember<string> name = default!;

        /// <summary>
        /// Configures the <c>enabled</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<bool> enabled = default!;
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
        public global::Morphant.Members.ConstructorMember<int> id = default!;

        /// <summary>
        /// Configures the <c>tags</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string[]> tags = default!;
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

    [Test]
    public async Task Escapes_keyword_parameter_name()
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
        public global::Morphant.Members.ConstructorMember<int> @event = default!;
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
        var hasConstructorMembers = !string.IsNullOrEmpty(constructorMembers);

        var builder = new StringBuilder();
        builder.AppendLine(ExpectedFileStart);

        if (hasConstructorMembers)
        {
            builder.AppendLine(constructorMembers);
            builder.AppendLine();
        }

        builder.AppendLine(ExpectedTemplateTypeStart);

        builder.AppendLine(hasConstructorMembers
            ? ExpectedByConventionConstructorWithMembers
            : ExpectedByConventionConstructorWithoutMembers);

        builder.AppendLine();
        builder.AppendLine(ExpectedByFactoryConstructor);

        if (!string.IsNullOrEmpty(destinationConstructors))
        {
            builder.AppendLine();
            builder.AppendLine(destinationConstructors);
        }

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
    private const string ExpectedByConventionConstructorWithoutMembers =
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
    private const string ExpectedByConventionConstructorWithMembers =
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
