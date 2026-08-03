using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceUsageTests
{
    [Test]
    public void Resolves_every_structured_construction_form_and_manual_mapping()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Members;

namespace TestCase
{
    public sealed class Source
    {
        public int Id { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id) { }

        public Destination(
            Guid id,
            bool enabled = true,
            params string[] tags) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source?, Destination?>()
                .Construct(source => new(source.Id))
                .Construct((source, previous) =>
                {
                    if (previous.HasValue)
                        return previous;

                    return new(
                            ByConvention(),
                            new()
                            {
                                idInt = source.Id,
                                enabled = Auto(),
                                tags = new[] { "generated" }
                            });
                })
                .Construct(source => new(
                    (ConstructorParameter<Guid>)Guid.NewGuid(),
                    tags: Array.Empty<string>()))
                .Construct(source => new(
                    ByFactory(() => new Destination(source.Id))))
                .Convert((source, previous, context) =>
                    previous.HasValue
                        ? previous.Value
                        : new Destination(source!.Id));
        }
    }
}
""";

        ConstructionSurfaceCompilationTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [Test]
    public void Resolves_direct_method_groups_and_previous_aware_replacement()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<string?, Guid>()
                .Construct(Guid.Parse)
                .Construct((source, previous) =>
                    previous.HasValue
                        ? previous.Value
                        : Guid.Parse(source))
                .Convert((source, previous, _) =>
                    source is null
                        ? previous.HasValue
                            ? previous.Value
                            : Guid.Empty
                        : Guid.Parse(source));
        }
    }
}
""";

        ConstructionSurfaceCompilationTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }
}
