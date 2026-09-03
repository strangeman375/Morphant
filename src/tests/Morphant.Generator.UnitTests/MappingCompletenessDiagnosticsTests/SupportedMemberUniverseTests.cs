namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class SupportedMemberUniverseTests
{
    [Test]
    public void Source_universe_contains_only_supported_readable_instance_members()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        private int _refValue;

        public int Property { get; set; }
        public int MutableField;
        public readonly int ReadonlyField;
        public static int StaticProperty { get; set; }
        public const int Constant = 1;
        public int this[int index] => index;
        public ref int RefReturn => ref _refValue;
        public int WriteOnly { set { } }
    }

    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Source);
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Source member 'TestCase.Source.Property' is " +
                    "not used by mapping 'TestCase.Source -> " +
                    "TestCase.Destination'.",
                    "Source member 'TestCase.Source.MutableField' " +
                    "is not used by mapping 'TestCase.Source -> " +
                    "TestCase.Destination'.",
                    "Source member 'TestCase.Source.ReadonlyField' " +
                    "is not used by mapping 'TestCase.Source -> " +
                    "TestCase.Destination'."
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Destination_universe_contains_only_assignable_instance_members()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        private int _refValue;

        public int Property { get; set; }
        public int InitOnly { get; init; }
        public int MutableField;
        public readonly int ReadonlyField;
        public int GetOnly { get; }
        public int PrivateSetter { get; private set; }
        public static int StaticProperty { get; set; }
        public const int Constant = 1;
        public int this[int index] { get => index; set { } }
        public ref int RefReturn => ref _refValue;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination);
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Destination member " +
                    "'TestCase.Destination.Property' is not mapped " +
                    "by mapping 'TestCase.Source -> TestCase.Destination'.",
                    "Destination member " +
                    "'TestCase.Destination.InitOnly' is not mapped " +
                    "by mapping 'TestCase.Source -> TestCase.Destination'.",
                    "Destination member " +
                    "'TestCase.Destination.MutableField' is not " +
                    "mapped by mapping 'TestCase.Source -> " +
                    "TestCase.Destination'."
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
