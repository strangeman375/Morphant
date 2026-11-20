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
        var library = GeneratorTestDriver.Run("Library", librarySource, LanguageVersion.CSharp9);
        using var dll = new MemoryStream();
        var emit = library.OutputCompilation.Emit(dll);
        Assert.That(emit.Success, Is.True, string.Join(Environment.NewLine, emit.Diagnostics));
        var reference = MetadataReference.CreateFromImage(dll.ToArray());

        var clean = GeneratorTestDriver.Run("FriendConsumer", consumer, LanguageVersion.CSharp9);
        var conflict = GeneratorTestDriver.Run("FriendConsumer", consumer, LanguageVersion.CSharp9,
            driver: clean.Driver, additionalReferences: [reference]);
        var recovered = GeneratorTestDriver.Run("FriendConsumer", consumer, LanguageVersion.CSharp9,
            driver: conflict.Driver);
        var other = GeneratorTestDriver.Run("OtherConsumer", consumer, LanguageVersion.CSharp9,
            additionalReferences: [reference]);

        Assert.Multiple(() =>
        {
            Assert.That(clean.Diagnostics, Is.Empty);
            Assert.That(conflict.Diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0065" }));
            Assert.That(conflict.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(recovered.Diagnostics, Is.Empty);
            Assert.That(recovered.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(other.Diagnostics, Is.Empty);
            Assert.That(other.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
