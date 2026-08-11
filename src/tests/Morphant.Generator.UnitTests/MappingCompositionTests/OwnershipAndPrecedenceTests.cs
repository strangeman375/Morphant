using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.MappingCompositionTests;

[TestFixture]
internal sealed class OwnershipAndPrecedenceTests
{
    [Test]
    public void Settings_and_callback_bodies_are_not_plan_slots()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { public int Value { get; set; } }
public sealed class Destination { public int Value { get; set; } }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>()
            .NullSourceHandling(NullSourceHandling.Throw)
            .Convert(source =>
            {
                builder.Map<Source, Destination>()
                    .Construct(inner => new())
                    .Members(inner => new() { Value = inner.Value });

                new OtherBuilder()
                    .Construct(source!)
                    .Members(source!)
                    .Convert(source!);

                return new Destination();
            });
    }
}

public sealed class OtherBuilder
{
    public OtherBuilder Construct(Source source) => this;
    public OtherBuilder Members(Source source) => this;
    public OtherBuilder Convert(Source source) => this;
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Pair_settings_do_not_interrupt_duplicate_slot_order()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Construct(source => new())
            .NullSourceHandling(NullSourceHandling.Throw)
            .MemberSelection(MemberSelection.Explicit)
            .Resolve((source, previous) => new());
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0019"));
            Assert.That(
                MappingCompositionGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Resolve"));
            Assert.That(
                MappingCompositionGeneratorTest.SourceText(
                    diagnostic.AdditionalLocations.Single()),
                Is.EqualTo("Construct"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Compiler_owned_callback_errors_are_not_slot_occurrences()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>()
            .Construct(source => new())
            .Construct(default);

        builder.Map<Destination, Source>()
            .Construct(source => new())
            .Construct(source => 42);
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);
        var compilerIds = result.CompilerWarningsAndErrors
            .Select(static diagnostic => diagnostic.Id)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(compilerIds, Does.Contain("CS0121"));
            Assert.That(compilerIds, Does.Contain("CS0029"));
            Assert.That(compilerIds, Does.Contain("CS1662"));
        });
    }

    [Test]
    public void Discarded_registration_chain_has_no_composition_diagnostic()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>()
            .Construct(source => new());

        builder.Map<Source, Destination>()
            .Construct(source => new())
            .Resolve((source, previous) => new());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0013" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Authoritative_conflict_on_an_independent_pair_is_preserved()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class SourceA { }
public sealed class DestinationA { }
public sealed class SourceB { }
public sealed class DestinationB { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<SourceA, DestinationA>();
        builder.Map<SourceA, DestinationA>()
            .Construct(source => new())
            .Resolve((source, previous) => new());

        builder.Map<SourceB, DestinationB>()
            .Construct(source => new())
            .Resolve((source, previous) => new());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0013", "MORPH0019" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Imported_plan_does_not_mix_with_a_local_Convert()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public class BaseSource { public int Value { get; set; } }
public sealed class Source : BaseSource { }
public class BaseDestination { public int Value { get; set; } }
public sealed class Destination : BaseDestination { }

[MorphantMapper]
public partial class BaseMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<BaseSource, BaseDestination>()
            .Members(source => new() { Value = source.Value });
}

[MorphantMapper]
public partial class TestMapper : BaseMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Source, Destination>()
            .Convert(source =>
                new Destination { Value = source!.Value })
            .IncludeBase<BaseSource, BaseDestination>();
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Mapper_and_pair_flow_gates_suppress_composition_analysis()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class SourceA { }
public sealed class DestinationA { }
public sealed class SourceB { }
public sealed class DestinationB { }

[MorphantMapper]
public partial class RootFlowMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        Observe(builder);
        builder.Map<SourceA, DestinationA>()
            .Construct(source => new())
            .Resolve((source, previous) => new());
    }

    private static void Observe(MapperBuilder builder) { }
}

[MorphantMapper]
public partial class PairFlowMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        var mapping = builder.Map<SourceB, DestinationB>();
        mapping
            .Construct(source => new())
            .Resolve((source, previous) => new());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0017", "MORPH0018" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Structural_mapper_gate_suppresses_composition_analysis()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Construct(source => new())
            .Resolve((source, previous) => new());
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0006" }));
    }

    [Test]
    public void Declared_contract_gate_suppresses_composition_analysis()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;
using Morphant.Context;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper,
    ITypeMapper<Source, Destination>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Construct(source => new())
            .Resolve((source, previous) => new());

    Destination ITypeMapper<Source, Destination>.Create(
        Source? source,
        MappingContext context) => new();

    Destination ITypeMapper<Source, Destination>.Update(
        Source? source,
        Destination? destination,
        MappingContext context) => destination ?? new();
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0009" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Unsupported_root_gate_suppresses_composition_analysis()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<T, Destination>();
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0012" }));
    }

    [Test]
    public void Unifiable_contract_gate_suppresses_composition_analysis()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Box<T> { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Box<T>, Destination>()
            .Construct(source => new())
            .Resolve((source, previous) => new());

        builder.Map<Box<string>, Destination>()
            .Construct(source => new())
            .Resolve((source, previous) => new());
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0014" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Compatibility_gate_suppresses_all_later_analysis()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
        }
    }
}
""";

        var result = MappingCompositionGeneratorTest.Run(
            source,
            languageVersion: LanguageVersion.CSharp8);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0001" }));
    }
}
