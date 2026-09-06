using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class GeneratedPlanNamingUsageTests
{
    [TestCase("Order.Maps", "Morphant.Generated.N_6b1e06427f58b75757a77839ab73a30c")]
    [TestCase("Order-Maps", "Morphant.Generated.N_0e2b5996e8c3d1a575ca79d385ddbe8c")]
    [TestCase("Order_002EMaps", "Morphant.Generated.N_5defa84e6d5bcb86ef4b7c64229a310e")]
    [TestCase("Order..Maps", "Morphant.Generated.N_7bdfc104f1a3d4d88eff66ca84bcf7cc")]
    [TestCase("Order_Maps", "Morphant.Generated.N_167a643e6a0e355e2ea9b3c0888f01e8")]
    [TestCase("Заказ🚀", "Morphant.Generated.N_8a7d3fd8c104938406aa751182e26d4b")]
    [TestCase("SignedMaps", "Morphant.Generated.N_b6a42b3741f3f607aa36d038a32f00b1", true)]
    [TestCase("SignedMaps_Kba27fb6be8f80649", "Morphant.Generated.N_11f339498537bcae609b877f424416af")]
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
using __SCOPE__;

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
using Morphant.Generated.N_58367882961a46be32ebecd9df026a51;

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

    [Test]
    public void Explicit_names_distinguish_global_user_type_from_special_type()
    {
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

public sealed class Int32 { }

namespace TestCase
{
    public sealed class UserTypeSource { }
    public sealed class SpecialTypeSource { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<
                UserTypeSource,
                (global::Int32 Value, int Count)>(MappingMode.Update)
                .Members(s => new global::Morphant.Generated.N_27fdc9c2e07bbe577efd674deb933baf.TupleMembers
                { Value = Ignore(), Count = Ignore() });
            builder.Map<
                SpecialTypeSource,
                (int Value, int Count)>(MappingMode.Update)
                .Members(s => new global::Morphant.Generated.N_2cb8d588fcb16098ee7da0dfe1024bc3.TupleMembers
                { Value = Ignore(), Count = Ignore() });
        }
    }
}
""";
        AssertClean(GeneratorTestDriver.Run(
            "TupleGlobalTypeNameConsumer", source, LanguageVersion.CSharp9));
    }

    [Test]
    public void Dependency_version_changes_preserve_all_generated_sources()
    {
        const string dependency =
"""
#nullable enable
[assembly: System.Reflection.AssemblyVersion("__VERSION__")]
namespace Models
{
    public sealed class Destination { public int Id { get; set; } }
}
""";
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Source
{
    public int Id { get; set; }
    public int Count { get; set; }
    public Models.Destination Value { get; set; } = new();
}
[MorphantMapper]
public partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Models.Destination>();
        builder.Map<Source, (Models.Destination Value, int Count)>();
    }
}
""";
        var initial = GeneratorTestDriver.Run(
            "StableDependencyMaps", source, LanguageVersion.CSharp9,
            additionalReferences: [GeneratorTestDriver.CompileReference(
                "Models", dependency.Replace("__VERSION__", "1.0.0.0"))]);
        var updated = GeneratorTestDriver.Run(
            "StableDependencyMaps", source, LanguageVersion.CSharp9,
            driver: initial.Driver,
            additionalReferences: [GeneratorTestDriver.CompileReference(
                "Models", dependency.Replace("__VERSION__", "2.0.0.0"))]);
        AssertClean(initial);
        AssertClean(updated);
        Assert.That(
            updated.GeneratedSources.Select(s => (s.HintName, s.SourceText.ToString())),
            Is.EquivalentTo(initial.GeneratedSources.Select(s =>
                (s.HintName, s.SourceText.ToString()))));
    }

    [Test]
    public void Registration_order_preserves_explicit_destination_type_names()
    {
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Source { public int Id { get; set; } public int Count { get; set; } }
public sealed class Destination { public int Id { get; set; } }
[MorphantMapper]
public partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        __REGISTRATIONS__
    }
}
""";
        const string ordinary = "builder.Map<Source, Destination>()" +
            ".Construct(s => new global::Morphant.Generated.N_66ad384dd37ee7b081e6efa67ae4cc5a.DestinationConstruction());";
        const string tuple = "builder.Map<Source, (int Id, int Count)>()" +
            ".Construct(s => new global::Morphant.Generated.N_13d09e5594ff7d40f54250ee5de5a870.TupleConstruction(s.Id, s.Count));";
        var initial = GeneratorTestDriver.Run(
            "StableOrderMaps",
            source.Replace("__REGISTRATIONS__", ordinary + tuple),
            LanguageVersion.CSharp9);
        var updated = GeneratorTestDriver.Run(
            "StableOrderMaps",
            source.Replace("__REGISTRATIONS__", tuple + ordinary),
            LanguageVersion.CSharp9, driver: initial.Driver);
        AssertClean(initial);
        AssertClean(updated);
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
