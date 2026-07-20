using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeDocumentationTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Documentation;

[TestFixture]
internal sealed class TemplateMemberDocumentationTests
{
    [Test]
    public async Task Uses_inheritdoc_for_documented_property()
    {
        // lang=c#
        const string destinationMembers =
"""
        /// <summary>
        /// Gets or sets the destination identifier.
        /// </summary>
        public int Id { get; set; }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <inheritdoc cref="global::TestCase.Destination.Id"/>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Uses_inheritdoc_for_documented_field()
    {
        // lang=c#
        const string destinationMembers =
"""
        /// <summary>
        /// Stores the destination name.
        /// </summary>
        public string Name = null!;
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <inheritdoc cref="global::TestCase.Destination.Name"/>
        public global::Morphant.Members.Member<string> Name
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Places_documentation_before_copied_Obsolete_attribute()
    {
        // lang=c#
        const string destinationMembers =
"""
        /// <summary>
        /// Gets or sets the legacy destination identifier.
        /// </summary>
        [Obsolete("Use Value instead.")]
        public int Id { get; set; }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <inheritdoc cref="global::TestCase.Destination.Id"/>
        [global::System.ObsoleteAttribute("Use Value instead.")]
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Generates_fallback_summary_for_undocumented_property()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int Id { get; set; }
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

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Generates_fallback_summary_for_undocumented_field()
    {
        // lang=c#
        const string destinationMembers =
"""
        public string Name = null!;
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Name"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> Name
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(destinationMembers, expectedMembers);
    }

    [Test]
    public async Task Uses_inheritdoc_for_documented_inherited_member()
    {
        // lang=c#
        const string additionalSource =
"""
    public class BaseDestination
    {
        /// <summary>
        /// Gets or sets the destination identifier.
        /// </summary>
        public int Id { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <inheritdoc cref="global::TestCase.BaseDestination.Id"/>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            expectedMembers: expectedMembers,
            additionalSource: additionalSource,
            destinationDeclaration:
                "public sealed class Destination : BaseDestination");
    }

    [Test]
    public async Task Uses_inheritdoc_for_documented_inherited_interface_member()
    {
        // lang=c#
        const string additionalSource =
"""
    public interface IBaseDestination
    {
        /// <summary>
        /// Gets or sets the destination identifier.
        /// </summary>
        int Id { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <inheritdoc cref="global::TestCase.IBaseDestination.Id"/>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            expectedMembers: expectedMembers,
            additionalSource: additionalSource,
            destinationDeclaration:
                "public interface Destination : IBaseDestination",
            constructors: string.Empty,
            expectedConstructors: string.Empty,
            canConstructDestination: false);
    }

    [Test]
    public async Task Generates_fallback_summary_for_undocumented_inherited_member()
    {
        // lang=c#
        const string additionalSource =
"""
    public class BaseDestination
    {
        public int Id { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.BaseDestination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            expectedMembers: expectedMembers,
            additionalSource: additionalSource,
            destinationDeclaration:
                "public sealed class Destination : BaseDestination");
    }

    [Test]
    public async Task Uses_inheritdoc_for_member_documented_with_inheritdoc()
    {
        // lang=c#
        const string destinationMembers =
"""
        /// <inheritdoc/>
        public override int Value { get; set; }
""";

        // lang=c#
        const string additionalSource =
"""
    public class BaseDestination
    {
        /// <summary>
        /// Gets or sets the destination value.
        /// </summary>
        public virtual int Value { get; set; }
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <inheritdoc cref="global::TestCase.Destination.Value"/>
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
    public async Task Uses_open_generic_definition_in_member_cref()
    {
        // lang=c#
        const string additionalSource =
"""
    public sealed class Payload
    {
    }

    public class BaseDestination<T>
    {
        /// <summary>
        /// Gets or sets the payload.
        /// </summary>
        public T Value { get; set; } = default!;
    }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <inheritdoc cref="global::TestCase.BaseDestination&lt;T&gt;.Value"/>
        public global::Morphant.Members.Member<global::TestCase.Payload> Value
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            expectedMembers: expectedMembers,
            additionalSource: additionalSource,
            destinationDeclaration:
                "public sealed class Destination : BaseDestination<Payload>");
    }

    [Test]
    public async Task Uses_inheritdoc_for_positional_record_members()
    {
        // lang=c#
        const string destinationDocumentation =
"""
    /// <summary>
    /// Represents a destination model.
    /// </summary>
    /// <param name="Id">The destination identifier.</param>
    /// <param name="Name">The destination name.</param>
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
        /// Configures the <c>Id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> Id = null!;

        /// <summary>
        /// Configures the <c>Name</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> Name = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="Id">Configures the <c>Id</c> constructor argument.</param>
        /// <param name="Name">Configures the <c>Name</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> Id,
            global::Morphant.Members.ConstructorMember<string> Name)
        {
        }
""";

        // lang=c#
        const string expectedMembers =
"""
        /// <inheritdoc cref="global::TestCase.Destination.Id"/>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }

        /// <inheritdoc cref="global::TestCase.Destination.Name"/>
        public global::Morphant.Members.Member<string> Name
        {
            get => null!;
            set { }
        }
""";

        await RunAndAssert(
            expectedMembers: expectedMembers,
            destinationDeclaration:
                "public sealed record Destination(int Id, string Name)",
            destinationDocumentation: destinationDocumentation,
            constructors: string.Empty,
            constructorMembers: constructorMembers,
            expectedConstructors: expectedConstructors);
    }
}
