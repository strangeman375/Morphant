using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class SharedExtensionTests
{
    [Test]
    public void Renaming_generic_parameters_preserves_extension_file_names()
    {
        const string source = """
#nullable enable
using Morphant;
internal sealed class Box<A,B> { }
internal sealed class Destination<T> { }
[MorphantMapper]
internal partial class Mapper<TFirst, TSecond> : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Box<TSecond, TFirst>, Destination<TSecond>>();
}
""";
        var original = MappingRegistrationGeneratorTest.Run(source);
        var renamed = MappingRegistrationGeneratorTest.Run(
            source.Replace("TFirst", "X").Replace("TSecond", "Y"), driver: original.Driver);

        Assert.That(original.Diagnostics, Is.Empty);
        Assert.That(renamed.Diagnostics, Is.Empty);
        Assert.That(original.CompilerWarningsAndErrors, Is.Empty);
        Assert.That(renamed.CompilerWarningsAndErrors, Is.Empty);
        Assert.That(renamed.GeneratedSources.Select(file => file.HintName),
            Is.EquivalentTo(original.GeneratedSources.Select(file => file.HintName)));
    }

    [TestCase("Box<T, int>", "Box<string, U>", true)]
    [TestCase("Box<T, int>", "Box<U, long>", false)]
    [TestCase("Box<T, int>", "Box<U, int>", false)]
    [TestCase("Box<T, int>", "Box<Box<U, int>, int>", false)]
    [TestCase("Box<T, T>", "Box<U, TUnused>", true)]
    [TestCase("Box<T, T[]>", "Box<U, TUnused[]>", true)]
    [TestCase("Box<T, T>", "Box<Box<U, int>, TUnused>", false)]
    public void Open_pairs_only_conflict_at_an_ambiguous_intersection(
        string first, string second, bool ambiguous)
    {
        var source = $$"""
#nullable enable
using Morphant;
internal sealed class Box<A,B> { }
internal sealed class Destination { }
internal partial class Container
{
    [MorphantMapper]
    private partial class First<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<{{first}}, Destination>();
    }

    [MorphantMapper]
    private partial class Second<U, TUnused> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<{{second}}, Destination>();
    }
}
""";
        var result = MappingRegistrationGeneratorTest.Run(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(ambiguous ? new[] { "MORPH0064" } : Array.Empty<string>()));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Removing_an_intersection_repairs_all_mappers_sharing_the_open_pair()
    {
        const string source = """
#nullable enable
#pragma warning disable MORPH0064
using Morphant;
internal sealed class Box<X, Y> { }
internal sealed class Destination { }
[MorphantMapper]
internal partial class First<T> : TypeMapper
{
    protected override void Configure(MapperBuilder builder) => builder.Map<Box<T, int>, Destination>();
}
[MorphantMapper]
internal partial class Shared<T> : TypeMapper
{
    protected override void Configure(MapperBuilder builder) => builder.Map<Box<T, int>, Destination>();
}
[MorphantMapper]
internal partial class Third<U> : TypeMapper
{
    protected override void Configure(MapperBuilder builder) => builder.Map<Box<string, U>, Destination>();
}
internal static class Scenario
{
    public static void Verify()
    {
        Check(new First<string>());
        Check(new Shared<string>());
    }

    private static bool Repaired => false;

    private static void Check(ITypeMapper<Box<string, int>, Destination> mapper)
    {
        try
        {
            if (mapper.Create(new Box<string, int>()) is Destination && Repaired) return;
        }
        catch (Morphant.Exceptions.MappingConfigurationException) when (!Repaired) { return; }
        throw new System.Exception("Unexpected mapper behavior.");
    }
}
""";
        var initial = GeneratorTestDriver.Run("SharedOpenRepair", source, LanguageVersion.CSharp9);
        var repaired = source.Replace("Box<string, U>", "Box<U, long>").Replace("Repaired => false", "Repaired => true");
        var updated = GeneratorTestDriver.Run("SharedOpenRepair", repaired, LanguageVersion.CSharp9, driver: initial.Driver);
        var clean = GeneratorTestDriver.Run("SharedOpenRepair", repaired, LanguageVersion.CSharp9);

        Assert.That(initial.Diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0064" }));
        Assert.That(initial.CompilerWarningsAndErrors, Is.Empty);
        Assert.That(updated.Diagnostics, Is.Empty);
        Assert.That(updated.CompilerWarningsAndErrors, Is.Empty);
        Assert.That(updated.GeneratedSources.Select(file => (file.HintName, file.SourceText.ToString())),
            Is.EquivalentTo(clean.GeneratedSources.Select(file => (file.HintName, file.SourceText.ToString()))));
        GeneratedCodeExecution.AssertScenario("intersection", initial.OutputCompilation, "Scenario");
        GeneratedCodeExecution.AssertScenario("repair", updated.OutputCompilation, "Scenario");
    }

    [TestCase("Box<string>", "Box<string?>", true)]
    [TestCase("Box<object>", "Box<dynamic>", true)]
    [TestCase("Box<string>?", "Box<string>", false)]
    public void Shared_pairs_compare_nested_presentations(string first, string second, bool conflict)
    {
        var source = $$"""
#nullable enable
using Morphant;
internal sealed class Box<T> { }
internal sealed class Destination { }

[MorphantMapper]
internal partial class First : TypeMapper
{
    protected override void Configure(MapperBuilder builder) => builder.Map<{{first}}, Destination?>();
}

[MorphantMapper]
internal partial class Second : TypeMapper
{
    protected override void Configure(MapperBuilder builder) => builder.Map<{{second}}, Destination>();
}
""";
        var result = MappingRegistrationGeneratorTest.Run(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(conflict ? new[] { "MORPH0056" } : Array.Empty<string>()));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Matching_the_first_tuple_presentation_repairs_only_the_conflicting_mapper()
    {
        const string source = """
#nullable enable
using Morphant;
#pragma warning disable MORPH0056
[MorphantMapper]
internal partial class First : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int Value, int Count), int>().Convert(s => s.Value);
}
[MorphantMapper]
internal partial class Second : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int Alias, int Count), int>().Convert(s => s.Count);
}
internal static class Scenario
{
    public static void Verify()
    {
        ITypeMapper<(int, int), int> first = new First();
        ITypeMapper<(int, int), int> second = new Second();
        if (first.Create((1, 2)) != 1) throw new System.Exception("First registration changed.");
        try
        {
            int value = second.Create((1, 2));
            if (value == 2 && Repaired) return;
        }
        catch (Morphant.Exceptions.MappingConfigurationException) when (!Repaired) { return; }
        throw new System.Exception("Unexpected second mapper behavior.");
    }

    private static bool Repaired => false;
}
""";
        var initial = GeneratorTestDriver.Run("SharedTupleRepair", source, LanguageVersion.CSharp9);
        var updated = GeneratorTestDriver.Run("SharedTupleRepair",
            source.Replace("int Alias", "int Value").Replace("Repaired => false", "Repaired => true"),
            LanguageVersion.CSharp9, driver: initial.Driver);

        Assert.That(initial.CompilerWarningsAndErrors, Is.Empty);
        Assert.That(updated.Diagnostics, Is.Empty);
        Assert.That(updated.CompilerWarningsAndErrors, Is.Empty);
        GeneratedCodeExecution.AssertScenario("conflict", initial.OutputCompilation, "Scenario");
        GeneratedCodeExecution.AssertScenario("repair", updated.OutputCompilation, "Scenario");
    }

    [Test]
    public void Adding_and_removing_friend_access_replaces_the_local_extension_and_diagnostic()
    {
        const string librarySource = """
#nullable enable
using Morphant;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FriendConsumer")]
[MorphantMapper]
internal partial class LibraryMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) => builder.Map<string, int>().Convert(s => s?.Length ?? 0);
}
""";
        const string consumer = """
#nullable enable
using Morphant;
[MorphantMapper]
internal partial class ConsumerMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) => builder.Map<string, int>().Convert(s => s?.Length ?? 0);
}
""";
        static PortableExecutableReference Library(string name)
        {
            var library = GeneratorTestDriver.Run(name, librarySource, LanguageVersion.CSharp9);
            using var dll = new MemoryStream();
            var emit = library.OutputCompilation.Emit(dll);
            Assert.That(emit.Success, Is.True, string.Join(Environment.NewLine, emit.Diagnostics));
            return MetadataReference.CreateFromImage(dll.ToArray());
        }

        var reference = Library("FirstLibrary");

        var clean = GeneratorTestDriver.Run("FriendConsumer", consumer, LanguageVersion.CSharp9);
        var conflict = GeneratorTestDriver.Run("FriendConsumer", consumer, LanguageVersion.CSharp9,
            driver: clean.Driver, additionalReferences: [reference]);
        var both = GeneratorTestDriver.Run("FriendConsumer", consumer, LanguageVersion.CSharp9,
            driver: conflict.Driver, additionalReferences: [reference, Library("SecondLibrary")]);
        var hidden = GeneratorTestDriver.Run("FriendConsumer", consumer, LanguageVersion.CSharp9,
            driver: both.Driver, additionalReferences: [reference.WithAliases(["LibraryOnly"])]);
        var aliased = reference.WithAliases(["LibraryOnly"]);
        var imported = GeneratorTestDriver.Run("FriendConsumer",
            consumer.Replace("using Morphant;", "extern alias LibraryOnly;\nusing Morphant;\nusing LibraryOnly::Morphant;"),
            LanguageVersion.CSharp9, driver: hidden.Driver, additionalReferences: [aliased]);
        var importedStatic = GeneratorTestDriver.Run("FriendConsumer",
            consumer.Replace("using Morphant;", "extern alias LibraryOnly;\nusing Morphant;\nusing static LibraryOnly::Morphant.MorphantGeneratedMappingExtensions;"),
            LanguageVersion.CSharp9, driver: imported.Driver, additionalReferences: [aliased]);
        var localFile = new GeneratorTestSourceFile("B_Local.cs", consumer);
        var importedFile = new GeneratorTestSourceFile("A_Imported.cs", consumer.Replace("ConsumerMapper", "ImportedMapper")
            .Replace("using Morphant;", "extern alias LibraryOnly;\nusing Morphant;\nusing LibraryOnly::Morphant;"));
        var mixed = GeneratorTestDriver.Run("FriendConsumer", [localFile, importedFile], LanguageVersion.CSharp9,
            driver: imported.Driver, additionalReferences: [aliased]);
        var mixedReordered = GeneratorTestDriver.Run("FriendConsumer", [importedFile with { Name = "Z_Imported.cs" }, localFile],
            LanguageVersion.CSharp9, driver: mixed.Driver, additionalReferences: [aliased]);
        var recovered = GeneratorTestDriver.Run("FriendConsumer", consumer, LanguageVersion.CSharp9,
            driver: importedStatic.Driver);
        var other = GeneratorTestDriver.Run("OtherConsumer", consumer, LanguageVersion.CSharp9,
            additionalReferences: [reference]);

        Assert.Multiple(() =>
        {
            Assert.That(clean.Diagnostics, Is.Empty);
            Assert.That(conflict.Diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0065" }));
            Assert.That(conflict.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(both.Diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0065" }));
            Assert.That(both.CompilerWarningsAndErrors.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CS0121" }));
            Assert.That(hidden.Diagnostics, Is.Empty);
            Assert.That(hidden.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(imported.Diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0065" }));
            Assert.That(imported.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(importedStatic.Diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0065" }));
            Assert.That(importedStatic.CompilerWarningsAndErrors, Is.Empty);
            foreach (var (state, path) in new[] { (mixed, "A_Imported.cs"), (mixedReordered, "Z_Imported.cs") })
            {
                Assert.That(state.Diagnostics.Select(d => (d.Id, d.Location.SourceTree!.FilePath)),
                    Is.EqualTo(new[] { ("MORPH0065", path) }));
                Assert.That(state.CompilerWarningsAndErrors.Select(d => (d.Id, d.Location.SourceTree!.FilePath)),
                    Is.EqualTo(new[] { ("CS0121", path) }));
            }
            Assert.That(recovered.Diagnostics, Is.Empty);
            Assert.That(recovered.GeneratedSources.Select(file => (file.HintName, file.SourceText.ToString())),
                Is.EquivalentTo(clean.GeneratedSources.Select(file => (file.HintName, file.SourceText.ToString()))));
            Assert.That(recovered.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(other.Diagnostics, Is.Empty);
            Assert.That(other.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
