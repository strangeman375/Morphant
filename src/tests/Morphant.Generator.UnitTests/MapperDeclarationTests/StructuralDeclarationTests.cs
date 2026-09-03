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
                    "Mapper 'TestCase.WrongMapper' must derive " +
                    "from 'Morphant.TypeMapper<TestCase.WrongMapper>'."));
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
public class TestMapper : TypeMapper<TestMapper>
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
public partial class TestMapper : TypeMapper<TestMapper>
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
public partial class TestMapper : TypeMapper<TestMapper>
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
    public partial class MapperOne : TypeMapper<MapperOne>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class MapperTwo : TypeMapper<MapperTwo>
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
                    "Containing type 'TestCase.Outer' must be " +
                    "declared partial."));
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
        public partial class TestMapper : TypeMapper<TestMapper>
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
    public partial class ClassMapper : TypeMapper<ClassMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}

public partial record RecordContainer
{
    [MorphantMapper]
    public partial class RecordMapper : TypeMapper<RecordMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}

public partial struct StructContainer
{
    [MorphantMapper]
    public partial class StructMapper : TypeMapper<StructMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}

public partial interface InterfaceContainer
{
    [MorphantMapper]
    public partial class InterfaceMapper : TypeMapper<InterfaceMapper>
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
    public void Inaccessible_nested_mappers_report_MORPH0059_without_CS0122()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public partial class Container
{
    [MorphantMapper]
    private partial class PrivateMapper : TypeMapper<PrivateMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int X, int Y), int>();
    }

    [MorphantMapper]
    protected partial class ProtectedMapper : TypeMapper<ProtectedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int X, int Y), long>();
    }

    [MorphantMapper]
    private protected partial class PrivateProtectedMapper :
        TypeMapper<PrivateProtectedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int X, int Y), short>();
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
                    "MORPH0059",
                    "MORPH0059",
                    "MORPH0059"
                }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MapperDeclarationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "private", "protected", "private" }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "Type 'TestCase.Container.PrivateMapper' cannot declare " +
                    "or contain a Morphant mapper because it is not " +
                    "accessible to generated namespace-level code.",
                    "Type 'TestCase.Container.ProtectedMapper' cannot " +
                    "declare or contain a Morphant mapper because it is " +
                    "not accessible to generated namespace-level code.",
                    "Type 'TestCase.Container.PrivateProtectedMapper' " +
                    "cannot declare or contain a Morphant mapper because " +
                    "it is not accessible to generated namespace-level " +
                    "code."
                }));
            Assert.That(result.CompilerErrors, Is.Empty);
            Assert.That(result.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void Inaccessible_container_reports_MORPH0059_on_the_container()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public partial class Outer
{
    private partial class Hidden
    {
        [MorphantMapper]
        public partial class TestMapper : TypeMapper<TestMapper>
        {
            protected override void Configure(MapperBuilder builder) =>
                builder.Map<(int X, int Y), int>();
        }
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0059"));
            Assert.That(
                MapperDeclarationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("private"));
            Assert.That(
                diagnostic.GetMessage(),
                Does.StartWith("Type 'TestCase.Outer.Hidden'"));
            Assert.That(result.CompilerErrors, Is.Empty);
            Assert.That(result.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void Protected_internal_nested_mapper_is_namespace_accessible()
    {
        // lang=c#
        const string source =
"""
using Morphant;

namespace TestCase;

public sealed class Source
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public partial class Container
{
    [MorphantMapper]
    protected internal partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int Id, string Name)>()
                .Members(source => new()
                {
                    Id = source.Id,
                    Name = source.Name
                });
    }
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerErrors, Is.Empty);
            Assert.That(
                result.GeneratedSources.Any(generated =>
                    generated.HintName.Contains(
                        ".TypeMapper.",
                        StringComparison.Ordinal)),
                Is.True);
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
file partial class TestMapper : TypeMapper<TestMapper>
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
    public partial class MapperOne : TypeMapper<MapperOne>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class MapperTwo : TypeMapper<MapperTwo>
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
                Does.Contain("TestCase.Outer"));
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
    public class TestMapper : TypeMapper<TestMapper>
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
