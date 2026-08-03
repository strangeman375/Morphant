using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Constructors;

[TestFixture]
internal sealed class TemplateTypeConstructorDefaultValueTests
{
    [Test]
    public async Task Documents_effective_constructor_default_values()
    {
        // lang=c#
        const string constructors =
"""
        public Destination(
            string? name = null,
            DateTime createdAt = default,
            DayOfWeek day = DayOfWeek.Monday,
            Mode mode = (Mode)7,
            decimal amount = 12.5m,
            char separator = '<',
            string text = "A < B & C")
        {
        }
""";

        // lang=c#
        const string additionalSource =
"""
    public enum Mode
    {
        None,
        Active
    }
""";

        // lang=c#
        const string constructorMembers =
"""
    /// <summary>
    /// Contains mappings for constructor arguments of <see cref="global::TestCase.Destination"/>.
    /// </summary>
    internal sealed class DestinationMorphantTemplateConstructorParameters
    {
        /// <summary>
        /// Configures the <c>name</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<string?>? name = null!;

        /// <summary>
        /// Configures the <c>createdAt</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::System.DateTime> createdAt = null!;

        /// <summary>
        /// Configures the <c>day</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::System.DayOfWeek> day = null!;

        /// <summary>
        /// Configures the <c>mode</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<global::TestCase.Mode> mode = null!;

        /// <summary>
        /// Configures the <c>amount</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<decimal> amount = null!;

        /// <summary>
        /// Configures the <c>separator</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<char> separator = null!;

        /// <summary>
        /// Configures the <c>text</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<string> text = null!;
    }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="name">Configures the <c>name</c> constructor argument. If omitted, the destination constructor default value <c>null</c> is used.</param>
        /// <param name="createdAt">Configures the <c>createdAt</c> constructor argument. If omitted, the destination constructor default value <c>default</c> is used.</param>
        /// <param name="day">Configures the <c>day</c> constructor argument. If omitted, the destination constructor default value <c>DayOfWeek.Monday</c> is used.</param>
        /// <param name="mode">Configures the <c>mode</c> constructor argument. If omitted, the destination constructor default value <c>(Mode)7</c> is used.</param>
        /// <param name="amount">Configures the <c>amount</c> constructor argument. If omitted, the destination constructor default value <c>12.5</c> is used.</param>
        /// <param name="separator">Configures the <c>separator</c> constructor argument. If omitted, the destination constructor default value <c>'&lt;'</c> is used.</param>
        /// <param name="text">Configures the <c>text</c> constructor argument. If omitted, the destination constructor default value <c>"A &lt; B &amp; C"</c> is used.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorParameter<string?>? name = null,
            global::Morphant.Members.ConstructorParameter<global::System.DateTime> createdAt = null!,
            global::Morphant.Members.ConstructorParameter<global::System.DayOfWeek> day = null!,
            global::Morphant.Members.ConstructorParameter<global::TestCase.Mode> mode = null!,
            global::Morphant.Members.ConstructorParameter<decimal> amount = null!,
            global::Morphant.Members.ConstructorParameter<char> separator = null!,
            global::Morphant.Members.ConstructorParameter<string> text = null!)
        {
        }
""";

        await RunAndAssert(
            constructors,
            constructorMembers,
            expectedConstructors,
            additionalSource);
    }
}
