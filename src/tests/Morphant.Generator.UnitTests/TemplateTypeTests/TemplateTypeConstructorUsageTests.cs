using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateTypeTests;

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
            usage,
            constructorMembers,
            expectedConstructors);
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
            global::Morphant.Members.ConstructorMember<bool>? enabled = null,
            global::Morphant.Members.ConstructorMember<string[]>? tags = null)
        {
        }
""";

        await RunAndAssert(
            constructors,
            usage,
            constructorMembers,
            expectedConstructors);
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
            global::Morphant.Members.ConstructorMember<bool>? enabled = null)
        {
        }
""";

        await RunAndAssert(
            constructors,
            usage,
            constructorMembers,
            expectedConstructors);
    }

    [Test]
    public async Task Resolves_numeric_overloads_for_raw_values()
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
        public static object IntOverload() =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(1);

        public static object LongOverload() =>
            new global::TestCase.Morphant.Generated.DestinationMorphantTemplate(1L);
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
            usage,
            constructorMembers,
            expectedConstructors);
    }

    private static Task RunAndAssert(
        string constructors,
        string usage,
        string constructorMembers,
        string expectedConstructors)
    {
        return TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(constructors, usage),
            "Morphant.TemplateType.TestCase_Destination.g.cs",
            BuildExpectedSource(constructorMembers, expectedConstructors));
    }

    private static string BuildSource(
        string constructors,
        string usage)
    {
        return SourceTemplate
            .Replace(ConstructorPlaceholder, constructors)
            .Replace(UsagePlaceholder, usage);
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
    private const string UsagePlaceholder = "__USAGE__";

    // lang=c#
    private const string SourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

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

__USAGE__
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
