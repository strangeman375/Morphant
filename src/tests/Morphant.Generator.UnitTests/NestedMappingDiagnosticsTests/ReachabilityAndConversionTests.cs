namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

[TestFixture]
internal sealed class ReachabilityAndConversionTests
{
    [Test]
    public void Reports_missing_parameterless_source_inference()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildDestination { }
    public sealed class Source { }
    public sealed class Destination
    {
        public ChildDestination? Missing { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new() { Missing = Map() });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0044"));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "Map() could not find exactly one readable " +
                    "source member named 'Missing'"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Accepts_typed_null_and_warning_free_wider_targets()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public interface IChildDestination { }
    public sealed class ChildSource { }
    public sealed class ChildDestination : IChildDestination { }
    public sealed class Source { }

    public sealed class Destination
    {
        public IChildDestination? Child { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Child = Map<ChildDestination>((ChildSource?)null)
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.NestedMappingDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_explicit_null_for_a_non_nullable_value_destination()
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
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Value = Update<int>(source.Value, null)
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0046"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "null cannot be used for non-nullable destination type " +
                    "'int'"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Keeps_adaptive_Create_when_existing_Update_has_no_current_slot()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public interface IChildDestination { }
    public sealed class ChildSource { }
    public sealed class ChildDestination : IChildDestination { }

    public sealed class Source
    {
        public ChildSource Child { get; } = new();
    }

    public sealed class Destination
    {
        public Destination(IChildDestination value) => Stored = value;
        public IChildDestination Stored { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                    new(Map<ChildDestination>(source.Child)));
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        Assert.That(
            result.NestedMappingDiagnostics.Length,
            Is.EqualTo(1),
            string.Join(
                Environment.NewLine,
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0046"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "Map could not find the current destination for " +
                    "'value'"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith(
                    "Affected cases: Update with an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Keeps_parameterless_source_inference_when_only_current_is_invalid()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }
    public sealed class Source
    {
        public ChildSource value { get; } = new();
    }

    public sealed class Destination
    {
        public Destination(ChildDestination value) => Stored = value;
        public ChildDestination Stored { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                    new(Map<ChildDestination>()));
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0046"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "Map could not find the current destination for " +
                    "'value'"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith(
                    "Affected cases: Update with an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Ignores_an_invalid_adaptive_Update_when_only_Create_is_reachable()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }
    public sealed class Source
    {
        public ChildSource value { get; } = new();
    }

    public sealed class Destination
    {
        public Destination(ChildDestination value) => Stored = value;
        public ChildDestination Stored { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(MappingMode.Create)
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                    new(Map<ChildDestination>()));
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.NestedMappingDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_an_impossible_current_slot_but_accepts_a_wide_slot()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public interface IChildDestination { }
    public sealed class ChildSource { }
    public sealed class ChildDestination : IChildDestination { }

    public sealed class Source
    {
        public ChildSource Child { get; } = new();
    }

    public sealed class ImpossibleDestination
    {
        public ImpossibleDestination(IChildDestination value) { }
        public string Value { get; } = string.Empty;
    }

    public sealed class WideDestination
    {
        public WideDestination(IChildDestination value) => Value = value;
        public object Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ImpossibleDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                    new(Map<ChildDestination>(source.Child)));
            builder.Map<Source, WideDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                    new(Map<ChildDestination>(source.Child)));
        }
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        Assert.That(
            result.NestedMappingDiagnostics.Length,
            Is.EqualTo(1),
            string.Join(
                Environment.NewLine,
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0046"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "current destination of type 'string' cannot be used as " +
                    "'TestCase.ChildDestination'"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Rejects_a_numeric_current_slot_that_cannot_contain_the_pair()
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
        public int Number { get; set; }
    }

    public sealed class Destination
    {
        public long Number { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Number = Map<int>(source.Number)
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0046"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "current destination of type 'long' cannot be used as " +
                    "'int'"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith(
                    "Affected cases: Update with an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Keeps_runtime_result_Create_when_only_existing_Update_is_invalid()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }

    public sealed class RuntimeSlot
    {
        public static implicit operator RuntimeSlot(
            ChildDestination value) => new();
    }

    public sealed class Source
    {
        public ChildSource Child { get; } = new();
    }

    public sealed class Destination
    {
        public RuntimeSlot Child { get; set; } = new();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ResolveUsing((source, previous) =>
                    previous.HasValue ? previous.Value : new Destination())
                .Members(source => new()
                {
                    Child = Map<ChildDestination>(source.Child)
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0046"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "current destination of type 'TestCase.RuntimeSlot' " +
                    "cannot be used as 'TestCase.ChildDestination'"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith(
                    "Affected cases: Update with an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_one_ambiguous_adaptive_local_only_for_existing_Update()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }
    public sealed class Source
    {
        public ChildSource Child { get; } = new();
    }

    public sealed class Destination
    {
        public ChildDestination First { get; set; } = new();
        public ChildDestination Second { get; set; } = new();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source =>
                {
                    var child = Map(source.Child);
                    return new()
                    {
                        First = child,
                        Second = child
                    };
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        Assert.That(
            result.NestedMappingDiagnostics.Length,
            Is.EqualTo(1),
            string.Join(
                Environment.NewLine,
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        var diagnostic = result.NestedMappingDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0046"));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain(
                    "this Map call matches more than one current " +
                    "destination: First, Second"));
            Assert.That(
                diagnostic.AdditionalLocations.Take(2).Select(
                    NestedMappingDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "First", "Second" }));
            Assert.That(
                diagnostic.GetMessage(),
                Does.EndWith(
                    "Affected cases: Update with an existing destination."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Attributes_a_wrong_standalone_proxy_to_nested_Update()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }
    public sealed class Source
    {
        public ChildSource Child { get; } = new();
    }

    public sealed class Destination
    {
        public ChildDestination Child { get; } = new();
    }

    public sealed class SpoofedMembers
    {
        public global::Morphant.Members.Member<ChildDestination> Child =>
            null!;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) =>
                {
                    var members = new SpoofedMembers();
                    Update(source.Child, members.Child);
                    return new();
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0046" }));
            Assert.That(
                result.NestedMappingDiagnostics.Single().GetMessage(),
                Is.EqualTo(
                    "Destination for nested 'Update' is invalid in mapping " +
                    "'TestCase.Source -> TestCase.Destination': standalone " +
                    "Update requires a readable reference-type member " +
                    "selected through the generated Members callback " +
                    "result, such as members.Child. Affected cases: Create; " +
                    "Update without an existing destination; Update with " +
                    "an existing destination."));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    result.NestedMappingDiagnostics.Single().Location),
                Is.EqualTo("members.Child"));
            Assert.That(
                result.NestedMappingDiagnostics.Single()
                    .AdditionalLocations.Select(
                        NestedMappingDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Update" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Explains_why_a_standalone_Update_cannot_use_the_result_member()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }

    public sealed class Source
    {
        public ChildSource Child { get; } = new();
    }

    public sealed class Destination
    {
        public ChildDestination Child { get; } = new();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>();
            builder.Map<Source, Destination>()
                .Members((source, previous, result) =>
                {
                    Update(source.Child, result.Child);
                    return new();
                });
        }
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);
        Assert.That(result.EffectiveDiagnostics, Has.Length.EqualTo(1));
        var diagnostic = result.EffectiveDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0046"));
            Assert.That(diagnostic.Severity,
                Is.EqualTo(Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Destination for nested 'Update' is invalid in mapping " +
                    "'TestCase.Source -> TestCase.Destination': standalone " +
                    "Update requires a readable reference-type member " +
                    "selected through the generated Members callback " +
                    "result, such as members.Child. Affected cases: Create; " +
                    "Update without an existing destination; Update with " +
                    "an existing destination."));
            Assert.That(
                NestedMappingDiagnosticsGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("result.Child"));
            Assert.That(
                diagnostic.Location.SourceSpan,
                Is.EqualTo(new Microsoft.CodeAnalysis.Text.TextSpan(
                    source.IndexOf("result.Child", StringComparison.Ordinal),
                    "result.Child".Length)));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    NestedMappingDiagnosticsGeneratorTest.SourceText),
                Is.EqualTo(new[] { "Update" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Accepts_the_generated_get_only_proxy()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }
    public sealed class Source
    {
        public ChildSource Child { get; } = new();
    }

    public sealed class Destination
    {
        public ChildDestination Child { get; } = new();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) =>
                {
                    var members = new global::Morphant.Generated.Types.N_TestCase.Plans
                        .DestinationMembers();
                    Update(source.Child, members.Child);
                    return members;
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
