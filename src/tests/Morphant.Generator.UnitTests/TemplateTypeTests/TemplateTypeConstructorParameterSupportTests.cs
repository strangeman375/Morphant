using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateTypeTests;

[TestFixture]
internal sealed class TemplateTypeConstructorParameterSupportTests
{
    [Test]
    public async Task Keeps_supported_constructors_when_another_constructor_has_by_reference_parameters()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id)
        {
        }

        public Destination(ref string name)
        {
        }

        public Destination(Guid token)
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
        /// Configures the <c>token</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<global::System.Guid> token = null!;
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
        /// <param name="token">Configures the <c>token</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<global::System.Guid> token)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Skips_constructor_with_byref_like_parameter_without_affecting_supported_constructors()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int id)
        {
        }

        public Destination(Span<int> values)
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
