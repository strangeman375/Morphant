using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceNamingTests
{
    [Test]
    public void Uses_destination_relative_namespaces_and_nested_type_scopes()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

public sealed class GlobalSource { }
public sealed class GlobalDestination { }

namespace First
{
    public sealed class Destination { }
}

namespace Second
{
    public sealed class Destination { }
}

namespace TestCase
{
    public sealed class Outer<T>
    {
        public sealed class Destination<U>
        {
            public Destination(T outer, U value) { }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<GlobalSource, GlobalDestination>();
            builder.Map<GlobalSource, First.Destination>();
            builder.Map<GlobalSource, Second.Destination>();
            builder.Map<GlobalSource, Outer<string>.Destination<int>>();
        }
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);

        Assert.That(
            generated[
                "Morphant.Generated.Construction.GlobalDestination.g.cs"],
            Does.Contain("namespace Morphant.Generated"));
        Assert.That(
            generated[
                "Morphant.Generated.Construction.First_Destination.g.cs"],
            Does.Contain("namespace First.Morphant.Generated"));
        Assert.That(
            generated[
                "Morphant.Generated.Construction.Second_Destination.g.cs"],
            Does.Contain("namespace Second.Morphant.Generated"));
        Assert.That(
            generated[
                "Morphant.Generated.Construction.TestCase_Outer_1_Destination_1.g.cs"],
            Does.Contain(
                "namespace TestCase.Morphant.Generated.Outer1Scope"));
    }

    [Test]
    public void Adds_a_hash_only_for_real_case_insensitive_hint_collisions()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class URL { }
    public sealed class Url { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, URL>();
            builder.Map<Source, Url>();
        }
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var constructionHints = generated.Keys
            .Where(static name =>
                name.Contains(".Construction.", StringComparison.Ordinal))
            .ToArray();
        var extensionHints = generated.Keys
            .Where(static name =>
                name.Contains(".MappingExtension.", StringComparison.Ordinal))
            .ToArray();

        Assert.That(constructionHints, Has.Length.EqualTo(2));
        Assert.That(extensionHints, Has.Length.EqualTo(2));
        Assert.That(
            constructionHints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Has.Length.EqualTo(2));
        Assert.That(
            extensionHints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Has.Length.EqualTo(2));
        Assert.That(
            constructionHints.Count(static name => name.Contains(
                "__",
                StringComparison.Ordinal)),
            Is.EqualTo(1));
        Assert.That(
            extensionHints.Count(static name =>
                CountOccurrences(name, "__") > 1),
            Is.EqualTo(1));
    }

    [Test]
    public void Escapes_keyword_type_parameters_in_every_generated_usage()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<@class>
        where @class : class
    {
        public @class Value { get; init; } = null!;
    }

    public sealed class Destination<@class>
        where @class : class
    {
        public Destination(@class value) { }
    }

    [MorphantMapper]
    public partial class TestMapper<@class> : TypeMapper
        where @class : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<@class>, Destination<@class>>()
                .Construct(source => new(source.Value));
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var extension = generated.Values.Single(static value =>
            value.Contains(
                "internal static partial class MorphantGeneratedMappingExtensions",
                StringComparison.Ordinal));
        var plan = generated.Values.Single(static value =>
            value.Contains(
                "internal sealed class DestinationConstruction",
                StringComparison.Ordinal));

        Assert.That(
            extension,
            Does.Contain("Construct<@class>("));
        Assert.That(
            extension,
            Does.Contain("global::TestCase.Source<@class>"));
        Assert.That(
            extension,
            Does.Contain("where @class : class"));
        Assert.That(
            plan,
            Does.Contain("DestinationConstruction<@class>"));
        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<@class>"));
    }

    [Test]
    public void Deduplicates_canonical_pair_representations_across_mappers()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<T> { }

    public sealed class Destination<T>
    {
        public Destination(T value) { }
    }

    [MorphantMapper]
    public partial class NullableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<string?>, Destination<string?>>();
    }

    [MorphantMapper]
    public partial class NonNullableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<string>, Destination<string>>();
    }

    [MorphantMapper]
    public partial class DynamicMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<dynamic>, Destination<dynamic>>();
    }

    [MorphantMapper]
    public partial class ObjectMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<object>, Destination<object>>();
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var extensions = generated.Keys.Where(static name =>
            name.Contains(
                ".MappingExtension.",
                StringComparison.Ordinal));

        Assert.That(extensions.Count(), Is.EqualTo(2));
        Assert.That(
            generated.Keys.Count(static name =>
                name.Contains(
                    ".Construction.",
                    StringComparison.Ordinal)),
            Is.EqualTo(1));
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(
                   pattern,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
