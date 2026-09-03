namespace Morphant.Generator.UnitTests.MapperDeclarationTests;

[TestFixture]
internal sealed class InterfaceContractTests
{
    private const string MapperFile =
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

    [Test]
    public void Direct_exact_interface_reports_MORPH0009_at_Map()
    {
        // lang=c#
        const string source =
"""
using Morphant;
using Morphant.Context;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<Source, Destination>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0009"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapping 'TestCase.Source -> TestCase.Destination' is " +
                    "already implemented by mapper " +
                    "'TestCase.TestMapper'. Remove the interface " +
                    "declaration or the Map registration."));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(diagnostic.AdditionalLocations, Has.Count.EqualTo(1));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.AdditionalLocations[0]),
                Is.EqualTo("ITypeMapper<Source, Destination>"));
            Assert.That(result.HasGeneratedFile(MapperFile), Is.False);
        });
    }

    [Test]
    public void Transitive_interface_paths_report_all_direct_syntaxes()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

public interface IFirst : ITypeMapper<Source, Destination> { }
public interface ISecond : IFirst { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>, IFirst, ISecond
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0009"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MapperDeclarationGeneratorTest.SourceText),
                Is.EqualTo(new[] { "IFirst", "ISecond" }));
        });
    }

    [Test]
    public void Interface_locations_follow_partial_declaration_source_order()
    {
        // lang=c#
        const string firstPart =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }
public interface IFirst : ITypeMapper<Source, Destination> { }
public interface ISecond : ITypeMapper<Source, Destination> { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>, IFirst
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";
        // lang=c#
        const string secondPart =
"""
namespace TestCase;

public abstract partial class TestMapper : ISecond
{
}
""";

        var result = MapperDeclarationGeneratorTest.Run(
        [
            new MapperSourceFile("FirstPart.cs", firstPart),
            new MapperSourceFile("SecondPart.cs", secondPart)
        ]);
        var diagnostic = result.Diagnostics.Single();

        Assert.That(
            diagnostic.AdditionalLocations.Select(location =>
                (location.SourceTree!.FilePath,
                    MapperDeclarationGeneratorTest.SourceText(location))),
            Is.EqualTo(new[]
            {
                ("FirstPart.cs", "IFirst"),
                ("SecondPart.cs", "ISecond")
            }));
    }

    [Test]
    public void Exact_interface_inherited_only_through_a_base_class_is_allowed()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>,
    ITypeMapper<Source, Destination>
    where TMapper : BaseMapper<TMapper>
{
    public Destination Create(
        Source? source,
        global::Morphant.Context.MappingContext context) =>
        throw new System.NotSupportedException();

    public Destination Update(
        Source? source,
        Destination? destination,
        global::Morphant.Context.MappingContext context) =>
        throw new System.NotSupportedException();
}

[MorphantMapper]
public partial class TestMapper : BaseMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.HasGeneratedFile(MapperFile), Is.True);
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Generic_constraints_do_not_disprove_unification()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public abstract partial class TestMapper<T> : TypeMapper<TestMapper<T>>,
    ITypeMapper<T, Destination>
    where T : struct
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0010"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapper 'TestCase.TestMapper<T>' declares an " +
                    "interface that may conflict with generated mapping " +
                    "'TestCase.Source -> TestCase.Destination'."));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.AdditionalLocations.Single()),
                Is.EqualTo("ITypeMapper<T, Destination>"));
        });
    }

    [Test]
    public void Nested_constructed_interface_roots_can_unify()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Wrapper<T>
{
    public sealed class Nested
    {
    }
}

public sealed class Destination { }

[MorphantMapper]
public abstract partial class TestMapper<T> : TypeMapper<TestMapper<T>>,
    ITypeMapper<Wrapper<T>.Nested, Destination>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Wrapper<int>.Nested, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0010" }));
    }

    [Test]
    public void Non_unifiable_direct_interface_does_not_block_generation()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<int, Destination>
{
    public abstract Destination Create(
        int source,
        global::Morphant.Context.MappingContext context);

    public abstract Destination Update(
        int source,
        Destination? destination,
        global::Morphant.Context.MappingContext context);

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.HasGeneratedFile(MapperFile), Is.True);
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Exact_conflict_has_precedence_over_unifiable_candidates()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }
public interface IExact : ITypeMapper<Source, Destination> { }
public interface IGeneric<T> : ITypeMapper<T, Destination> { }

[MorphantMapper]
public abstract partial class TestMapper<T> : TypeMapper<TestMapper<T>>,
    IExact,
    IGeneric<T>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0009"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(
                    MapperDeclarationGeneratorTest.SourceText),
                Is.EqualTo(new[] { "IExact" }));
        });
    }

    [Test]
    public void Conflict_removes_only_its_pair_from_the_mapper_artifact()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class ConflictDestination { }
public sealed class IndependentDestination { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<Source, ConflictDestination>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, ConflictDestination>();
        builder.Map<Source, IndependentDestination>();
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var mapper = result.GeneratedFile(MapperFile);
        var surfaceSources = string.Join(
            Environment.NewLine,
            result.GeneratedSources
                .Where(generated => generated.HintName != MapperFile)
                .Select(generated => generated.SourceText.ToString()));

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0009" }));
            Assert.That(
                mapper,
                Does.Not.Contain(
                    "ITypeMapper<global::TestCase.Source, " +
                    "global::TestCase.ConflictDestination>"));
            Assert.That(
                mapper,
                Does.Contain(
                    "ITypeMapper<global::TestCase.Source, " +
                    "global::TestCase.IndependentDestination>"));
            Assert.That(
                surfaceSources,
                Does.Contain("ConflictDestination"));
        });
    }

    [Test]
    public void Unsupported_root_pair_is_still_checked_for_exact_conflict()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public interface IDestination { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<Source, IDestination>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, IDestination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0009" }));
            Assert.That(result.HasGeneratedFile(MapperFile), Is.False);
        });
    }

    [Test]
    public void Contract_message_uses_canonical_special_and_tuple_types()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

namespace TestCase;

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<object, (int, string)>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<dynamic, (int Value, string Name)>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.That(
            diagnostic.GetMessage(),
            Does.Contain(
                "object -> System.ValueTuple<int, string>"));
    }

    [Test]
    public void Contract_message_escapes_keyword_identifiers_and_nullability()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

namespace @class;

public sealed class @event { }
public sealed class @struct { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<@event, @struct>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<@event?, @struct?>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.That(
            diagnostic.GetMessage(),
            Does.Contain(
                "@class.@event -> @class.@struct"));
    }

    [Test]
    public void Repeated_canonical_pair_reports_once_at_the_first_Map()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public abstract partial class TestMapper : TypeMapper<TestMapper>,
    ITypeMapper<Source, Destination>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source?, Destination>();
        builder.Map<Source, Destination>();
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single(candidate =>
            candidate.Id == "MORPH0009");

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(candidate => candidate.Id),
                Is.EqualTo(new[] { "MORPH0009", "MORPH0013" }));
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0009"));
            Assert.That(
                diagnostic.Location.SourceTree!
                    .GetText()
                    .Lines
                    .GetLineFromPosition(diagnostic.Location.SourceSpan.Start)
                    .ToString(),
                Does.Contain("Map<Source?, Destination>"));
        });
    }
}
