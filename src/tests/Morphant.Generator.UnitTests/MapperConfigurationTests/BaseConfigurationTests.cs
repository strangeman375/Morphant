namespace Morphant.Generator.UnitTests.MapperConfigurationTests;

[TestFixture]
internal sealed class BaseConfigurationTests
{
    [Test]
    public void Connects_a_source_base_body_from_another_syntax_tree()
    {
        // lang=c#
        const string baseSource =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public abstract class BaseMapper<T> : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.NullSourceHandling(NullSourceHandling.Throw);
    }
}
""";

        // lang=c#
        const string derivedSource =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source
{
    public int Value { get; set; }
}

public sealed class Destination
{
    public int Value { get; set; }
}

[MorphantMapper]
public partial class DerivedMapper : BaseMapper<int>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure((builder!));
        builder.Map<Source, Destination>();
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(
        [
            new ConfigurationSourceFile("Base.cs", baseSource),
            new ConfigurationSourceFile("Derived.cs", derivedSource)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_a_metadata_only_generic_base_at_the_base_call()
    {
        var baseReference = BuildMetadataBase();

        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;
using Shared;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class DerivedMapper : ExternalBase<int>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Source, Destination>();
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(
            source,
            additionalReferences: [baseReference]);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0016"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Morphant cannot read Configure for base mapper " +
                    "'Shared.ExternalBase<int>' while analyzing mapper " +
                    "'TestCase.DerivedMapper'."));
            Assert.That(
                MapperConfigurationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Configure"));
            Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Deduplicates_repeated_unavailable_base_edges()
    {
        var baseReference = BuildMetadataBase();

        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;
using Shared;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class DerivedMapper : ExternalBase<int>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        base.Configure(builder);
        builder.Map<Source, Destination>();
    }
}
""";

        var result = MapperConfigurationGeneratorTest.Run(
            source,
            additionalReferences: [baseReference]);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0016", "MORPH0024" }));
    }

    private static Microsoft.CodeAnalysis.MetadataReference BuildMetadataBase()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

namespace Shared;

public abstract class ExternalBase<T> : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.MappingMode(MappingMode.Create);
    }
}
""";

        return MapperConfigurationGeneratorTest.CompileReference(
            "ExternalBaseAssembly",
            source);
    }
}
