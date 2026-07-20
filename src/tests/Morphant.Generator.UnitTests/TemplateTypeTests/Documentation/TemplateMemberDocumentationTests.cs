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
}
