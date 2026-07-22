using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeMemberNullabilityTests
{
    [Test]
    public async Task Preserves_oblivious_member_type()
    {
        // lang=c#
        const string destinationMembers =
"""
#nullable disable annotations
        public string LegacyName { get; set; }
#nullable enable annotations
""";

        // lang=c#
        const string expectedMembers =
"""
        #nullable disable annotations
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.LegacyName"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> LegacyName
        {
            get => null!;
            set { }
        }
        #nullable enable annotations
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Uses_property_input_nullability_contract()
    {
        // lang=c#
        const string destinationMembers =
"""
        [global::System.Diagnostics.CodeAnalysis.AllowNull]
        public string AllowsNull { get; set; } = null!;

        [global::System.Diagnostics.CodeAnalysis.DisallowNull]
        public string? DisallowsNull { get; set; }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.AllowsNull"/>.
        /// </summary>
        public global::Morphant.Members.Member<string?>? AllowsNull
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.DisallowsNull"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> DisallowsNull
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Accepts_null_literal_when_member_input_accepts_null()
    {
        // lang=c#
        const string destinationMembers =
"""
        public string? NullableProperty { get; set; }

        public int? NullableField;

        [global::System.Diagnostics.CodeAnalysis.AllowNull]
        public string AllowsNullField = null!;

        [global::System.Diagnostics.CodeAnalysis.DisallowNull]
        public string? DisallowsNullField;
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.NullableProperty"/>.
        /// </summary>
        public global::Morphant.Members.Member<string?>? NullableProperty
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.NullableField"/>.
        /// </summary>
        public global::Morphant.Members.Member<int?>? NullableField
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.AllowsNullField"/>.
        /// </summary>
        public global::Morphant.Members.Member<string?>? AllowsNullField
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.DisallowsNullField"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> DisallowsNullField
        {
            get => null!;
            set { }
        }
""";

        // lang=c#
        const string additionalSource =
"""
    internal static class TemplateUsage
    {
        internal static global::TestCase.Morphant.Generated.DestinationMorphantTemplate Create() =>
            new()
            {
                NullableProperty = null,
                NullableField = null,
                AllowsNullField = null
            };
    }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers,
            additionalSource);
    }
}
