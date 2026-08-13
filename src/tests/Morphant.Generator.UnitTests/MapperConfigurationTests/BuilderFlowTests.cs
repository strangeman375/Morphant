namespace Morphant.Generator.UnitTests.MapperConfigurationTests;

[TestFixture]
internal sealed class BuilderFlowTests
{
    [Test]
    public void Accepts_linear_root_pair_and_callback_flow()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class ConstructDestination { }
public sealed class ResolveDestination { }
public sealed class MembersDestination { public int Value { get; set; } }
public sealed class ConstructUsingDestination { }
public sealed class ResolveUsingDestination { }
public sealed class ConvertDestination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    private static bool SelectFirst => true;

    protected override void Configure(MapperBuilder builder)
    {
        global::Morphant.Delegates.ConstructUsing<
            Source,
            ConstructUsingDestination> constructUsing =
            source => new ConstructUsingDestination();
        global::Morphant.Delegates.ResolveUsing<
            Source,
            ResolveUsingDestination,
            ResolveUsingDestination> resolveUsingFirst =
            (source, previous) => new ResolveUsingDestination();
        global::Morphant.Delegates.ResolveUsing<
            Source,
            ResolveUsingDestination,
            ResolveUsingDestination> resolveUsingSecond =
            (source, previous) => new ResolveUsingDestination();
        global::Morphant.Delegates.Convert<Source?, ConvertDestination>
            convertFirst = source => new ConvertDestination();
        global::Morphant.Delegates.Convert<Source?, ConvertDestination>
            convertSecond = source => new ConvertDestination();

        _ = 1;
        ((builder!).MappingMode(MappingMode.CreateAndUpdate))!
            .Map<Source, ConstructDestination>()
            .Construct(source =>
            {
                _ = builder;
                return new();
            });
        builder.Map<Source, ResolveDestination>()
            .Resolve((source, previous) =>
            {
                _ = builder;
                return new();
            });
        builder.Map<Source, MembersDestination>()
            .Members(source =>
            {
                _ = builder;
                return new();
            });
        builder.Map<Source, ConstructUsingDestination>()
            .ConstructUsing(constructUsing);
        builder.Map<Source, ResolveUsingDestination>()
            .ResolveUsing(
                SelectFirst ? resolveUsingFirst : resolveUsingSecond);
        builder.Map<Source, ConvertDestination>()
            .Convert(SelectFirst ? convertFirst : convertSecond);
#if NEVER_DEFINED
        builder.Map<int, int>();
#endif
        return;
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);
        var diagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id is
                "MORPH0015" or
                "MORPH0016" or
                "MORPH0017" or
                "MORPH0018")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_root_alias_once_and_preserves_its_visible_registration()
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
        var alias = builder;
        alias.Map<Source, Destination>();
        _ = alias;
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0017" }));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    result.Diagnostics.Single().Location),
                Is.EqualTo("builder"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_each_independent_root_escape_at_the_builder_value()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using System;
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    private static void Consume(MapperBuilder value) { }

    protected override void Configure(MapperBuilder builder)
    {
        Consume(builder);
        Func<MapperBuilder> deferred = () => builder;
        _ = deferred;

        void Local()
        {
            _ = builder;
        }

        _ = (Action)Local;
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0017",
                    "MORPH0017",
                    "MORPH0017"
                }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MapperConfigurationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "builder", "builder", "builder" }));
        });
    }

    [Test]
    public void Reports_a_third_party_root_method_at_its_name()
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

public static class Extensions
{
    public static MapperBuilder Tap(this MapperBuilder builder) => builder;
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Tap().Map<Source, Destination>();
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0017"));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Tap"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_conditional_and_early_transfer_at_Map()
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
    private static bool Condition => true;

    protected override void Configure(MapperBuilder builder)
    {
        if (Condition)
        {
            builder.Map<SourceA, DestinationA>();
        }

        if (Condition)
        {
            return;
        }

        builder.Map<SourceB, DestinationB>();
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0017", "MORPH0017" }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MapperConfigurationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "Map", "Map" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Conditional_root_access_reports_Map_not_the_parameter()
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
        builder?.Map<Source, Destination>();
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0017"));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_pair_alias_at_the_authoritative_Map_only()
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
        var mapping = builder.Map<Source, Destination>();
        mapping.NullSourceHandling(NullSourceHandling.Throw);
        mapping.NullDestinationHandling(NullDestinationHandling.Create);
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0018"));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Morphant cannot analyze configuration for mapping " +
                    "'TestCase.Source -> TestCase.Destination' in mapper " +
                    "'global::TestCase.TestMapper'."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_a_third_party_pair_method_at_its_name()
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

public static class Extensions
{
    public static MapperBuilder<TSource, TDestination> Tap<
        TSource,
        TDestination>(
        this MapperBuilder<TSource, TDestination> builder) => builder;
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Tap()
            .NullSourceHandling(NullSourceHandling.Throw);
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0018"));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Tap"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Conditional_pair_fragment_is_pair_local()
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
        builder.Map<Source, Destination>()?
            .NullSourceHandling(NullSourceHandling.Throw);
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0018" }));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    result.Diagnostics.Single().Location),
                Is.EqualTo("Map"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Same_named_unrelated_api_does_not_participate()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class OtherBuilder
{
    public OtherBuilder Map<TSource, TDestination>() => this;
    public OtherBuilder Members(System.Action action) => this;
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        var other = new OtherBuilder();
        other.Map<int, string>().Members(() => { });
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void Materialized_callbacks_and_local_functions_own_builder_capture()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class DestinationA { }
public sealed class DestinationB { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        global::Morphant.Delegates.Convert<Source?, DestinationA> callback =
            source =>
            {
                _ = builder;
                return new DestinationA();
            };

        DestinationB Local(Source? source)
        {
            _ = builder;
            return new DestinationB();
        }

        builder.Map<Source, DestinationA>().Convert(callback);
        builder.Map<Source, DestinationB>().Convert(Local);
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);
        var categoryFour = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id is
                "MORPH0017" or "MORPH0018")
            .Select(diagnostic =>
                $"{diagnostic.Id}:" +
                MapperConfigurationGeneratorTest.SourceText(
                    diagnostic.Location) + ":" +
                diagnostic.Location.GetLineSpan().StartLinePosition.Line)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(categoryFour, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_loop_switch_and_try_registrations_as_root_flow()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS0162, CS1591

namespace TestCase;

public sealed class SourceA { }
public sealed class DestinationA { }
public sealed class SourceB { }
public sealed class DestinationB { }
public sealed class SourceC { }
public sealed class DestinationC { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        while (false)
        {
            builder.Map<SourceA, DestinationA>();
        }

        switch (1)
        {
            case 1:
                builder.Map<SourceB, DestinationB>();
                break;
        }

        try
        {
            builder.Map<SourceC, DestinationC>();
        }
        finally
        {
        }
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0017",
                    "MORPH0017",
                    "MORPH0017"
                }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MapperConfigurationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "Map", "Map", "Map" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
