namespace Morphant.Generator.UnitTests.CallbackDiagnosticsTests;

[TestFixture]
internal sealed class CallbackClassificationTests
{
    [Test]
    public void Requires_inline_lambdas_only_for_structured_callbacks()
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

    public sealed class ConstructDestination { }

    public sealed class ResolveDestination { }

    public sealed class MembersDestination
    {
        public int Value { get; set; }
    }

    public sealed class RuntimeDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(BuildConstruction);
            builder.Map<Source, ResolveDestination>()
                .Resolve(BuildResolution);
            builder.Map<Source, MembersDestination>()
                .Members(BuildMembers);
            builder.Map<Source, RuntimeDestination>()
                .ConstructUsing(BuildRuntime);
        }

        private static global::Morphant.Generated.Types.N_TestCase.Plans.ConstructDestinationConstruction
            BuildConstruction(Source source) => new();

        private static global::Morphant.Generated.Types.N_TestCase.Plans.ResolveDestinationConstruction
            BuildResolution(
                Source source,
                Option<ResolveDestination> previous) => new();

        private static global::Morphant.Generated.Types.N_TestCase.Plans.MembersDestinationMembers
            BuildMembers(Source source) => new();

        private static RuntimeDestination BuildRuntime(Source source) => new();
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0029",
                    "MORPH0029",
                    "MORPH0029"
                }));
            Assert.That(
                diagnostics.Select(diagnostic =>
                    CallbackDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "BuildConstruction",
                    "BuildMembers",
                    "BuildResolution"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Construct for mapping 'TestCase.Source -> " +
                    "TestCase.ConstructDestination' must use an inline " +
                    "lambda.",
                    "Members for mapping 'TestCase.Source -> " +
                    "TestCase.MembersDestination' must use an inline " +
                    "lambda.",
                    "Resolve for mapping 'TestCase.Source -> " +
                    "TestCase.ResolveDestination' must use an inline lambda."
                }));
            Assert.That(
                diagnostics.SelectMany(static diagnostic =>
                    diagnostic.AdditionalLocations),
                Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Keeps_wrapped_inline_lambdas_and_runtime_delegates_valid()
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

    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Construct(((source => new())!));
            builder.Map<Source, int>()
                .Convert(true ? First : Second);
        }

        private static readonly global::Morphant.Delegates.Convert<Source?, int>
            First = source => 1;

        private static readonly global::Morphant.Delegates.Convert<Source?, int>
            Second = source => 2;
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
