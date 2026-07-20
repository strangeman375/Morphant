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

    [Test]
    public async Task Preserves_base_first_declaration_order_for_class_members()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int DerivedFirst { get; set; }

        public string DerivedSecond { get; set; } = null!;
""";

        // lang=c#
        const string additionalSource =
"""
    public class BaseDestination
    {
        public bool BaseFirst { get; set; }

        public decimal BaseSecond { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.BaseDestination.BaseFirst"/>.
        /// </summary>
        public global::Morphant.Members.Member<bool> BaseFirst
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.BaseDestination.BaseSecond"/>.
        /// </summary>
        public global::Morphant.Members.Member<decimal> BaseSecond
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.DerivedFirst"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> DerivedFirst
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.DerivedSecond"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> DerivedSecond
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
    public async Task Preserves_base_first_declaration_order_for_interface_members()
    {
        // lang=c#
        const string destinationMembers =
"""
        int DerivedFirst { get; set; }

        string DerivedSecond { get; set; }
""";

        // lang=c#
        const string additionalSource =
"""
    public interface IBaseDestination
    {
        bool BaseFirst { get; set; }

        decimal BaseSecond { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.IBaseDestination.BaseFirst"/>.
        /// </summary>
        public global::Morphant.Members.Member<bool> BaseFirst
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.IBaseDestination.BaseSecond"/>.
        /// </summary>
        public global::Morphant.Members.Member<decimal> BaseSecond
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.DerivedFirst"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> DerivedFirst
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.DerivedSecond"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> DerivedSecond
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
                "public interface Destination : IBaseDestination",
            canConstructDestination: false);
    }

    [Test]
    public async Task Skips_ambiguous_members_from_unrelated_interfaces()
    {
        // lang=c#
        const string destinationMembers =
"""
        int Id { get; set; }
""";

        // lang=c#
        const string additionalSource =
"""
    public interface IIntValue
    {
        int Value { get; set; }
    }

    public interface IStringValue
    {
        string Value { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
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
                "public interface Destination : IIntValue, IStringValue",
            canConstructDestination: false);
    }

}
