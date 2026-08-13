namespace Morphant.Generator.UnitTests.MapperDeclarationTests;

[TestFixture]
internal sealed class SupportsConflictTests
{
    private const string MapperFile =
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

    [Test]
    public void Own_exact_signature_reports_MORPH0034_and_blocks_the_mapper()
    {
        // lang=c#
        const string source =
"""
using System;
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    private new int Supports(Type sourceType, Type destinationType) => 0;

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0034"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapper 'global::TestCase.TestMapper' declares " +
                    "'Supports(System.Type, System.Type)', which conflicts " +
                    "with the generated mapper."));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Supports"));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.HasGeneratedFile(MapperFile), Is.False);
            Assert.That(result.GeneratedSources, Is.Not.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Override_and_static_forms_still_conflict()
    {
        // lang=c#
        const string overrideSource =
"""
using System;
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override bool Supports(Type sourceType, Type destinationType) =>
        false;

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";
        // lang=c#
        const string staticSource =
"""
using System;
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    private static new bool Supports(
        Type sourceType,
        Type destinationType) => false;

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var overridden = MapperDeclarationGeneratorTest.Run(overrideSource);
        var staticMember = MapperDeclarationGeneratorTest.Run(staticSource);

        Assert.Multiple(() =>
        {
            Assert.That(
                overridden.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0034" }));
            Assert.That(
                staticMember.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0034" }));
        });
    }

    [Test]
    public void Multiple_conflicting_partial_members_share_one_diagnostic()
    {
        // lang=c#
        const string firstPart =
"""
using System;
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    private new bool Supports(Type sourceType, Type destinationType) => false;

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";
        // lang=c#
        const string secondPart =
"""
using System;

namespace TestCase;

public partial class TestMapper
{
    private static new int Supports(
        Type sourceType,
        Type destinationType) => 0;
}
""";

        var result = MapperDeclarationGeneratorTest.Run(
        [
            new MapperSourceFile("FirstPart.cs", firstPart),
            new MapperSourceFile("SecondPart.cs", secondPart)
        ]);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0034"));
            Assert.That(
                diagnostic.Location.SourceTree!.FilePath,
                Is.EqualTo("FirstPart.cs"));
            Assert.That(diagnostic.AdditionalLocations, Has.Count.EqualTo(1));
            Assert.That(
                diagnostic.AdditionalLocations[0].SourceTree!.FilePath,
                Is.EqualTo("SecondPart.cs"));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.AdditionalLocations[0]),
                Is.EqualTo("Supports"));
        });
    }

    [Test]
    public void Other_overloads_and_local_functions_are_allowed()
    {
        // lang=c#
        const string source =
"""
using System;
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    private new bool Supports<T>(Type sourceType, Type destinationType) => false;
    private new bool Supports(Type sourceType) => false;
    private new bool Supports(object sourceType, Type destinationType) => false;
    private new bool Supports(ref Type sourceType, Type destinationType) => false;
    private new bool Supports(Type sourceType, in Type destinationType) => false;
    private new bool Supports(
        Type sourceType,
        Type destinationType,
        Type otherType) => false;

    protected override void Configure(MapperBuilder builder)
    {
        bool Supports(Type sourceType, Type destinationType) => false;
        _ = Supports(typeof(Source), typeof(Destination));
        builder.Map<Source, Destination>();
    }
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
    public void Supports_conflict_suppresses_pair_contract_diagnostics()
    {
        // lang=c#
        const string source =
"""
using System;
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public abstract partial class TestMapper :
    TypeMapper,
    ITypeMapper<Source, Destination>
{
    private new bool Supports(Type sourceType, Type destinationType) => false;

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0034" }));
    }
}
