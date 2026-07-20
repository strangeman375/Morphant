using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeInheritedMemberTests
{
    [Test]
    public async Task Generates_accessible_members_inherited_from_base_type()
    {
        // lang=c#
        const string additionalSource =
"""
    public class BaseDestination
    {
        public int BaseProperty { get; set; }

        internal string? BaseField = null;

        protected int ProtectedProperty { get; set; }

        private int PrivateProperty { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.BaseDestination.BaseProperty"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> BaseProperty
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.BaseDestination.BaseField"/>.
        /// </summary>
        public global::Morphant.Members.Member<string?> BaseField
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            string.Empty,
            expectedMembers,
            additionalSource,
            destinationDeclaration:
                "public sealed class Destination : BaseDestination");
    }

    [Test]
    public async Task Generates_overridden_property_once()
    {
        // lang=c#
        const string destinationMembers =
"""
        public override int Value { get; set; }
""";

        // lang=c#
        const string additionalSource =
"""
    public class BaseDestination
    {
        public virtual int Value { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Value"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Value
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers,
            additionalSource,
            destinationDeclaration:
                "public sealed class Destination : BaseDestination");
    }

    [Test]
    public async Task Uses_most_derived_hidden_member()
    {
        // lang=c#
        const string destinationMembers =
"""
        public new string Value { get; set; } = null!;
""";

        // lang=c#
        const string additionalSource =
"""
    public class BaseDestination
    {
        public int Value { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Value"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> Value
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            destinationMembers,
            expectedMembers,
            additionalSource,
            destinationDeclaration:
                "public sealed class Destination : BaseDestination");
    }

    [Test]
    public async Task Generates_inherited_interface_member_once()
    {
        // lang=c#
        const string additionalSource =
"""
    public interface IBaseDestination
    {
        int Id { get; set; }
    }

    public interface ILeftDestination : IBaseDestination
    {
    }

    public interface IRightDestination : IBaseDestination
    {
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.IBaseDestination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            string.Empty,
            expectedMembers,
            additionalSource,
            destinationDeclaration:
                "public interface Destination : ILeftDestination, IRightDestination",
            canConstructDestination: false);
    }
}
