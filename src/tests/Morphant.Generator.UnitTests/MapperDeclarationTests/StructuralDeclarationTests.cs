namespace Morphant.Generator.UnitTests.MapperDeclarationTests;

[TestFixture]
internal sealed class StructuralDeclarationTests
{
    private const string MapperFile =
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

    [Test]
    public void Attribute_on_an_unrelated_class_reports_only_MORPH0005()
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
public abstract class WrongMapper : ITypeMapper<Source, Destination>
{
    private bool Supports(Type sourceType, Type destinationType) => false;
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0005"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mapper 'global::TestCase.WrongMapper' must derive " +
                    "from 'Morphant.TypeMapper'."));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("MorphantMapper"));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void Broken_base_type_is_left_to_the_CSharp_compiler()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

[MorphantMapper]
public partial class WrongMapper : MissingBase
{
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                result.CompilerErrors.Select(error => error.Id),
                Does.Contain("CS0246"));
            Assert.That(result.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void Single_non_partial_mapper_reports_MORPH0006()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0006"));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("TestMapper"));
            Assert.That(
                result.GeneratedSources.Any(generated =>
                    generated.HintName.Contains(
                        ".TypeMapper.",
                        StringComparison.Ordinal)),
                Is.False);
            Assert.That(result.GeneratedSources, Is.Not.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Consistent_partial_declarations_generate_normally()
    {
        // lang=c#
        const string configuredPart =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";
        // lang=c#
        const string secondPart =
"""
namespace TestCase;

public partial class TestMapper
{
}
""";

        var result = MapperDeclarationGeneratorTest.Run(
        [
            new MapperSourceFile("ConfiguredPart.cs", configuredPart),
            new MapperSourceFile("SecondPart.cs", secondPart)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.HasGeneratedFile(MapperFile), Is.True);
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void Inconsistent_partial_declarations_are_left_to_the_compiler()
    {
        // lang=c#
        const string configuredPart =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";
        // lang=c#
        const string brokenPart =
"""
namespace TestCase;

public class TestMapper
{
}
""";

        var result = MapperDeclarationGeneratorTest.Run(
        [
            new MapperSourceFile("ConfiguredPart.cs", configuredPart),
            new MapperSourceFile("BrokenPart.cs", brokenPart)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                result.CompilerErrors.Select(error => error.Id),
                Does.Contain("CS0260"));
            Assert.That(result.HasGeneratedFile(MapperFile), Is.False);
        });
    }

    [Test]
    public void Shared_non_partial_container_reports_one_MORPH0007()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

public class Outer
{
    [MorphantMapper]
    public partial class MapperOne : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class MapperTwo : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Destination, Source>();
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0007"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Containing type 'global::TestCase.Outer' must be " +
                    "declared partial so Morphant can generate nested " +
                    "mapper contracts."));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Outer"));
            Assert.That(
                result.GeneratedSources.Any(generated =>
                    generated.HintName.Contains(
                        ".TypeMapper.",
                        StringComparison.Ordinal)),
                Is.False);
        });
    }

    [Test]
    public void Every_non_partial_ancestor_reports_its_own_MORPH0007()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

public class Outer
{
    public class Inner
    {
        [MorphantMapper]
        public partial class TestMapper : TypeMapper
        {
            protected override void Configure(MapperBuilder builder) =>
                builder.Map<Source, Destination>();
        }
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0007", "MORPH0007" }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MapperDeclarationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "Outer", "Inner" }));
        });
    }

    [Test]
    public void All_legal_partial_container_kinds_generate_nested_mappers()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

public partial class ClassContainer
{
    [MorphantMapper]
    public partial class ClassMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}

public partial record RecordContainer
{
    [MorphantMapper]
    public partial class RecordMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}

public partial struct StructContainer
{
    [MorphantMapper]
    public partial class StructMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}

public partial interface InterfaceContainer
{
    [MorphantMapper]
    public partial class InterfaceMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                result.GeneratedSources.Count(generated =>
                    generated.HintName.Contains(
                        ".TypeMapper.",
                        StringComparison.Ordinal)),
                Is.EqualTo(4));
            Assert.That(result.CompilerErrors, Is.Empty);
        });
    }

    [Test]
    public void File_local_mapper_reports_MORPH0008_on_the_file_keyword()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

file sealed class Source { }
file sealed class Destination { }

[MorphantMapper]
file partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0008"));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("file"));
            Assert.That(
                result.GeneratedSources.Any(generated =>
                    generated.HintName.Contains(
                        ".TypeMapper.",
                        StringComparison.Ordinal)),
                Is.False);
        });
    }

    [Test]
    public void Shared_file_local_container_reports_one_MORPH0008()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

file partial class Outer
{
    [MorphantMapper]
    public partial class MapperOne : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class MapperTwo : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Destination, Source>();
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0008"));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("file"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.Contain("global::TestCase.Outer"));
        });
    }

    [Test]
    public void Independent_structural_problems_are_reported_together()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

file class Outer
{
    [MorphantMapper]
    public class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0006",
                    "MORPH0007",
                    "MORPH0008"
                }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MapperDeclarationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "TestMapper", "Outer", "file" }));
            Assert.That(
                result.GeneratedSources.Any(generated =>
                    generated.HintName.Contains(
                        ".TypeMapper.",
                        StringComparison.Ordinal)),
                Is.False);
        });
    }
}
