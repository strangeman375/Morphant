using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeMemberRepresentationTests
{
    [Test]
    public async Task Preserves_member_type_shapes_and_nullability()
    {
        // lang=c#
        const string destinationMembers =
"""
        public string? Name { get; set; }

        public int? Count { get; set; }

        public UserModel? User { get; set; }

        public System.Collections.Generic.List<UserModel?> Items
        {
            get;
            set;
        } = null!;

        public UserModel?[]? Users { get; set; }
""";

        // lang=c#
        const string additionalSource =
"""
    public sealed class UserModel
    {
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Name"/>.
        /// </summary>
        public global::Morphant.Members.Member<string?> Name
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Count"/>.
        /// </summary>
        public global::Morphant.Members.Member<int?> Count
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.User"/>.
        /// </summary>
        public global::Morphant.Members.Member<global::TestCase.UserModel?> User
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Items"/>.
        /// </summary>
        public global::Morphant.Members.Member<global::System.Collections.Generic.List<global::TestCase.UserModel?>> Items
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Users"/>.
        /// </summary>
        public global::Morphant.Members.Member<global::TestCase.UserModel?[]?> Users
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers,
            additionalSource);
    }

    [Test]
    public async Task Preserves_property_and_field_declaration_order()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int FirstField = 0;

        public string SecondProperty { get; set; } = null!;

        public bool ThirdField = false;

        public decimal FourthProperty { get; set; }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.FirstField"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> FirstField
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.SecondProperty"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> SecondProperty
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.ThirdField"/>.
        /// </summary>
        public global::Morphant.Members.Member<bool> ThirdField
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.FourthProperty"/>.
        /// </summary>
        public global::Morphant.Members.Member<decimal> FourthProperty
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Escapes_keyword_member_names()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int @event { get; set; }

        public string @class = null!;
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.@event"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> @event
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.@class"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> @class
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }
}
