using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class BaseConfigurationActualizationTests
{
    private const string DerivedMapper =
        "Morphant.Generated.TypeMapper.TestCase_DerivedMapper.g.cs";

    private const string StableMapper =
        "Morphant.Generated.TypeMapper.TestCase_StableMapper.g.cs";

    [Test]
    public void Actualizes_a_derived_mapper_when_its_base_callback_changes()
    {
        var models = SourceFile("Models.cs", ModelsSource);
        var derived = SourceFile("DerivedMapper.cs", DerivedMapperSource);
        var stable = SourceFile("StableMapper.cs", StableMapperSource);
        var generated = new[]
        {
            "Morphant.Generated.Construction.TestCase_Destination.g.cs",
            "Morphant.Generated.MappingExtension." +
            "TestCase_Source__TestCase_Destination.g.cs",
            "Morphant.Generated.Member.TestCase_Destination.g.cs",
            "Morphant.Generated.MemberExtension." +
            "TestCase_Source__TestCase_Destination.g.cs",
            "Morphant.Generated.Construction." +
            "TestCase_StableDestination.g.cs",
            "Morphant.Generated.MappingExtension." +
            "TestCase_StableSource__TestCase_StableDestination.g.cs",
            "Morphant.Generated.Member.TestCase_StableDestination.g.cs",
            "Morphant.Generated.MemberExtension." +
            "TestCase_StableSource__TestCase_StableDestination.g.cs",
            DerivedMapper,
            StableMapper
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "base callback version one",
                [models, derived, stable, BaseMapper(1)],
                generated),
            Step(
                "base callback version two",
                [models, derived, stable, BaseMapper(2)],
                generated,
                ChangedMapperStages()),
            Step(
                "base callback version one restored",
                [models, derived, stable, BaseMapper(1)],
                generated,
                ChangedMapperStages()));
    }

    private static GeneratorIncrementalitySourceFile BaseMapper(int delta)
    {
        return SourceFile(
            "BaseMapper.cs",
            BaseMapperSource.Replace("__DELTA__", delta.ToString()));
    }

    private static ExpectedIncrementalStage[] ChangedMapperStages()
    {
        return
        [
            .. EarlyPipeline(
                Reason(IncrementalStepRunReason.Modified, 1),
                Reason(IncrementalStepRunReason.Cached, 1)),
            Stage(
                "BuildTypeMapperModels",
                Expected(DerivedMapper, IncrementalStepRunReason.Modified),
                Expected(StableMapper, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperRequests",
                Expected(DerivedMapper, IncrementalStepRunReason.Modified),
                Expected(StableMapper, IncrementalStepRunReason.Cached))
        ];
    }

    // lang=c#
    private const string ModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    public sealed class StableSource
    {
        public int Value { get; init; }
    }

    public sealed class StableDestination
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string BaseMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public abstract class BaseMapper : TypeMapper<BaseMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) => new()
                {
                    Value = source.Value + __DELTA__
                });
    }
}
""";

    // lang=c#
    private const string DerivedMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase<Source, Destination>();
        }
    }
}
""";

    // lang=c#
    private const string StableMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class StableMapper : TypeMapper<StableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<StableSource, StableDestination>();
    }
}
""";
}
