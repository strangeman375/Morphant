using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeMemberTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Members;

[TestFixture]
internal sealed class TemplateTypeMemberSelectionTests
{
    [Test]
    public async Task Skips_explicit_interface_implementations()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int Id { get; set; }

        int IHasValue.Value { get; set; }
""";

        // lang=c#
        const string additionalSource =
"""
    public interface IHasValue
    {
        int Value { get; set; }
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
                "public sealed class Destination : IHasValue");
    }

    [Test]
    public async Task Ignores_events_methods_and_nested_types()
    {
        // lang=c#
        const string destinationMembers =
"""
        public int Id { get; set; }

#pragma warning disable CS0067
        public event EventHandler? Changed;
#pragma warning restore CS0067

        public void Reset()
        {
        }

        public sealed class Nested
        {
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

        await RunAndAssert(destinationMembers, expectedMembers);
    }
}
