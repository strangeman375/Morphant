using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Constructors;

[TestFixture]
internal sealed class TemplateTypeConstructorNullabilityTests
{
    [Test]
    public async Task Preserves_constructor_parameter_input_nullability_contract()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(
            string? nullableReference,
            DateTime? nullableValue,
            [global::System.Diagnostics.CodeAnalysis.AllowNull] string allowsNull,
            [global::System.Diagnostics.CodeAnalysis.DisallowNull] string? disallowsNull)
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
        /// Configures the <c>nullableReference</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string?>? nullableReference = null!;

        /// <summary>
        /// Configures the <c>nullableValue</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<global::System.DateTime?>? nullableValue = null!;

        /// <summary>
        /// Configures the <c>allowsNull</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string?>? allowsNull = null!;

        /// <summary>
        /// Configures the <c>disallowsNull</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> disallowsNull = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="nullableReference">Configures the <c>nullableReference</c> constructor argument.</param>
        /// <param name="nullableValue">Configures the <c>nullableValue</c> constructor argument.</param>
        /// <param name="allowsNull">Configures the <c>allowsNull</c> constructor argument.</param>
        /// <param name="disallowsNull">Configures the <c>disallowsNull</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<string?>? nullableReference,
            global::Morphant.Members.ConstructorMember<global::System.DateTime?>? nullableValue,
            global::Morphant.Members.ConstructorMember<string?>? allowsNull,
            global::Morphant.Members.ConstructorMember<string> disallowsNull)
        {
        }
""";

        // lang=c#
        const string additionalSource =
"""
    internal static class TemplateUsage
    {
        internal static global::TestCase.Morphant.Generated.DestinationMorphantTemplate Create() =>
            new(null, null, null, string.Empty);
    }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            additionalSource);
    }

    [Test]
    public async Task Preserves_oblivious_constructor_parameter_type()
    {
        // lang=c#
        const string constructors =
"""
#nullable disable annotations
        public Destination(string legacy)
        {
        }
#nullable enable annotations
""";

        // lang=c#
        const string constructorMembers =
"""
    /// <summary>
    /// Contains mappings for constructor arguments of <see cref="global::TestCase.Destination"/>.
    /// </summary>
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        #nullable disable annotations
        /// <summary>
        /// Configures the <c>legacy</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> legacy = null!;
        #nullable enable annotations
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="legacy">Configures the <c>legacy</c> constructor argument.</param>
        #nullable disable annotations
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<string> legacy)
        {
        }
        #nullable enable annotations
""";

        // lang=c#
        const string additionalSource =
"""
    internal static class TemplateUsage
    {
        internal static global::TestCase.Morphant.Generated.DestinationMorphantTemplate Create() =>
            new(legacy: null);
    }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            additionalSource);
    }

    [Test]
    public async Task Uses_null_only_as_omission_sentinel_for_optional_non_nullable_parameter()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(
            string mode = "default",
            string? nickname = null)
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
        /// Configures the <c>mode</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> mode = null!;

        /// <summary>
        /// Configures the <c>nickname</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string?>? nickname = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="mode">Configures the <c>mode</c> constructor argument. If omitted, the destination constructor default value <c>"default"</c> is used.</param>
        /// <param name="nickname">Configures the <c>nickname</c> constructor argument. If omitted, the destination constructor default value <c>null</c> is used.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<string> mode = null!,
            global::Morphant.Members.ConstructorMember<string?>? nickname = null)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors);
    }

    [Test]
    public async Task Scopes_oblivious_annotations_within_multi_parameter_constructor()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(
            string strict,
#nullable disable annotations
            string legacy,
#nullable enable annotations
            string? nullable)
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
        /// Configures the <c>strict</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> strict = null!;

        #nullable disable annotations
        /// <summary>
        /// Configures the <c>legacy</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> legacy = null!;
        #nullable enable annotations

        /// <summary>
        /// Configures the <c>nullable</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string?>? nullable = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="strict">Configures the <c>strict</c> constructor argument.</param>
        /// <param name="legacy">Configures the <c>legacy</c> constructor argument.</param>
        /// <param name="nullable">Configures the <c>nullable</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<string> strict,
            #nullable disable annotations
            global::Morphant.Members.ConstructorMember<string> legacy,
            #nullable enable annotations
            global::Morphant.Members.ConstructorMember<string?>? nullable)
        {
        }
""";

        // lang=c#
        const string additionalSource =
"""
    internal static class MixedTemplateUsage
    {
        internal static global::TestCase.Morphant.Generated.DestinationMorphantTemplate Create() =>
            new(strict: string.Empty, legacy: null, nullable: null);
    }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            additionalSource);
    }
}
