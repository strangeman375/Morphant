using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateTypeTests;

[TestFixture]
internal sealed class TemplateTypeConstructorAccessibilityTests
{
    [Test]
    public async Task Generates_only_constructors_accessible_from_generated_code()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(int publicValue)
        {
        }

        internal Destination(string internalValue)
        {
        }

        protected internal Destination(bool protectedInternalValue)
        {
        }

        private Destination(Guid privateValue)
        {
        }

        protected Destination(double protectedValue)
        {
        }

        private protected Destination(decimal privateProtectedValue)
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
        /// Configures the <c>publicValue</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> publicValue = null!;

        /// <summary>
        /// Configures the <c>internalValue</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> internalValue = null!;

        /// <summary>
        /// Configures the <c>protectedInternalValue</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<bool> protectedInternalValue = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="publicValue">Configures the <c>publicValue</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<int> publicValue)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="internalValue">Configures the <c>internalValue</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<string> internalValue)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="protectedInternalValue">Configures the <c>protectedInternalValue</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<bool> protectedInternalValue)
        {
        }
""";

        await RunAndAssert(constructors, constructorMembers, expectedConstructors);
    }

    [Test]
    public async Task Generates_no_destination_constructors_when_none_are_accessible()
    {
        // lang=c#
        const string constructors =
"""
        private Destination()
        {
        }

        protected Destination(int id)
        {
        }

        private protected Destination(string name)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers: string.Empty,
            expectedConstructors: string.Empty);
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
    public class Destination
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
