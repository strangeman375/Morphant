using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class GeneratedPlanNamingUsageTests
{
    [TestCase("Order.Maps", "A_Order_002EMaps")]
    [TestCase("Order-Maps", "A_Order_002DMaps")]
    [TestCase("Order_002EMaps", "A_Order__002EMaps")]
    [TestCase("Order..Maps", "A_Order_002E_002EMaps")]
    [TestCase("Order_Maps", "A_Order__Maps")]
    [TestCase("Заказ🚀", "A__0417_0430_043A_0430_0437_D83D_DE80")]
    [TestCase("SignedMaps", "A_SignedMaps_Kba27fb6be8f80649", true)]
    [TestCase("SignedMaps_Kba27fb6be8f80649", "A_SignedMaps__Kba27fb6be8f80649")]
    public void Explicit_names_preserve_assembly_identity_and_readable_leaf_names(
        string assemblyName,
        string scope,
        bool signed = false)
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using Morphant.Generated.Types.__SCOPE__.Plans;

public sealed class Source { public int Id { get; set; } }
public sealed class Destination
{
    public Destination(int id) => Id = id;
    public int Id { get; set; }
}

[MorphantMapper]
public partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>()
            .Construct(s => new DestinationConstruction(s.Id))
            .Members(s => new DestinationMembers { Id = s.Id });
}
""";
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable);

        if (signed)
        {
            options = options
                .WithCryptoPublicKey(typeof(TypeMapper<>).Assembly
                    .GetName().GetPublicKey()!.ToImmutableArray())
                .WithPublicSign(true);
        }

        var result = GeneratorTestDriver.Run(
            assemblyName,
            source.Replace("__SCOPE__", scope),
            LanguageVersion.CSharp9,
            compilationOptions: options);

        AssertClean(result);
    }

    [TestCase("source")]
    [TestCase("dll")]
    [TestCase("ref")]
    public void Friend_assemblies_can_generate_plans_for_the_same_destinations(
        string referenceKind)
    {
        // lang=c#
        const string producerSource =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PlanConsumer")]
namespace Shared
{
    public sealed class Source { public int Id { get; set; } public int Count { get; set; } }
    public sealed class Destination
    {
        public Destination(int id) => Id = id;
        public int Id { get; set; }
    }
}
[MorphantMapper]
public partial class ProducerMapper : TypeMapper<ProducerMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Shared.Source, Shared.Destination>();
        builder.Map<Shared.Source, (int Id, int Count)>();
    }
}
""";
        // lang=c#
        const string consumerSource =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using Shared;
using Morphant.Generated.Types.A_PlanConsumer.N_Shared.Plans;

[MorphantMapper]
public partial class ConsumerMapper : TypeMapper<ConsumerMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>()
            .Construct(s => new DestinationConstruction(s.Id))
            .Members(s => new DestinationMembers { Id = s.Id });
        builder.Map<Source, (int Id, int Count)>();
    }
}
""";
        var producer = GeneratorTestDriver.Run(
            "PlanProducer", producerSource, LanguageVersion.CSharp9);
        AssertClean(producer);

        MetadataReference reference;

        if (referenceKind == "source")
        {
            reference = producer.OutputCompilation.ToMetadataReference();
        }
        else
        {
            using var stream = new MemoryStream();
            var emit = producer.OutputCompilation.Emit(
                stream,
                options: new EmitOptions(
                    metadataOnly: referenceKind == "ref",
                    includePrivateMembers: referenceKind != "ref"));
            Assert.That(emit.Diagnostics, Is.Empty);
            reference = MetadataReference.CreateFromImage(stream.ToArray());
        }

        var consumer = GeneratorTestDriver.Run(
            "PlanConsumer", consumerSource, LanguageVersion.CSharp9,
            additionalReferences: [reference]);
        AssertClean(consumer);
    }

    [Test]
    public void Assembly_version_changes_preserve_all_generated_sources()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
[assembly: System.Reflection.AssemblyVersion("__VERSION__")]
public sealed class Source { public int Id { get; set; } public int Count { get; set; } }
public sealed class Destination { public int Id { get; set; } }
[MorphantMapper]
public partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>();
        builder.Map<Source, (int Id, int Count)>();
    }
}
""";
        var initial = GeneratorTestDriver.Run(
            "VersionedMaps", source.Replace("__VERSION__", "1.0.0.0"),
            LanguageVersion.CSharp9);
        var updated = GeneratorTestDriver.Run(
            "VersionedMaps", source.Replace("__VERSION__", "2.0.0.0"),
            LanguageVersion.CSharp9, driver: initial.Driver);
        AssertClean(initial);
        AssertClean(updated);
        Assert.That(
            updated.GeneratedSources.Select(s => (s.HintName, s.SourceText.ToString())),
            Is.EquivalentTo(initial.GeneratedSources.Select(s =>
                (s.HintName, s.SourceText.ToString()))));
    }

    private static void AssertClean(GeneratorTestDriverResult result)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
