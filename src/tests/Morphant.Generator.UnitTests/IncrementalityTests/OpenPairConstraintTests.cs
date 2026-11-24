using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class OpenPairConstraintTests
{
    [Test]
    public void A_nested_storage_edit_updates_the_unmanaged_constraint_and_recovers()
    {
        var map = SourceFile("Map.cs", """
#nullable enable
using Morphant;
[MorphantMapper] internal partial class Mapper<T> : TypeMapper where T : unmanaged
{
    protected override void Configure(MapperBuilder b) => b.Map<Packet<Cell<T>>,int>().Convert(_ => 1);
}
""");
        var packet = SourceFile("Packet.cs", "internal class Packet<U> where U : unmanaged { }");
        var cell = SourceFile("Cell.cs", "internal struct Cell<T> { public Hidden<T> Data { get; set; } }");
        string[] generated =
        [
            "Morphant.Generated.MappingExtension.PacketToInt32__304a6847d7cf4606609f291e2a995a61.g.cs",
            "Morphant.Generated.TypeMapper.Mapper__9edb5b92c15656d3b9d0ffe37d38c83e.g.cs"
        ];
        GeneratorIncrementalityStep Storage(string fieldType, params ExpectedIncrementalStage[] stages) =>
            Step(fieldType, [map, packet, cell, SourceFile("Hidden.cs",
                $"internal struct Hidden<T> {{ public {fieldType} Item {{ get; set; }} }}")], generated, stages);

        RunAndAssert(LanguageVersion.CSharp9, static () => new MorphantGenerator(),
            Storage("int"), Storage("long", EarlyPipeline(Reason(IncrementalStepRunReason.Cached, 1))),
            Storage("T"), Storage("int"));
    }
}
