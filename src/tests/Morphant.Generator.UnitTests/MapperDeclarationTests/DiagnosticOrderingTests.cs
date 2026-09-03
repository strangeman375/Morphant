namespace Morphant.Generator.UnitTests.MapperDeclarationTests;

[TestFixture]
internal sealed class DiagnosticOrderingTests
{
    [Test]
    public void Category_diagnostics_are_published_in_ID_order()
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
public abstract class MissingBase : ITypeMapper<Source, Destination> { }

[MorphantMapper]
public class NonPartialMapper : TypeMapper<NonPartialMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}

public class NonPartialContainer
{
    [MorphantMapper]
    public partial class NestedMapper : TypeMapper<NestedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}

[MorphantMapper]
file partial class FileMapper : TypeMapper<FileMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}

[MorphantMapper]
public abstract partial class ExactMapper : TypeMapper<ExactMapper>,
    ITypeMapper<Source, Destination>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}

[MorphantMapper]
public abstract partial class UnifiableMapper<T> : TypeMapper<UnifiableMapper<T>>,
    ITypeMapper<T, Destination>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}

[MorphantMapper]
public partial class SupportsMapper : TypeMapper<SupportsMapper>
{
    private new bool Supports(Type sourceType, Type destinationType) => false;

    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
""";

        var result = MapperDeclarationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(diagnostic => diagnostic.Id),
            Is.EqualTo(new[]
            {
                "MORPH0005",
                "MORPH0006",
                "MORPH0007",
                "MORPH0008",
                "MORPH0009",
                "MORPH0010",
                "MORPH0034"
            }));
    }
}
