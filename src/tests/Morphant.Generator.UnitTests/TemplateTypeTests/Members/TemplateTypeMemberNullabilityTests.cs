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
        public global::Morphant.Members.Member<string?> AllowsNull
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
}
