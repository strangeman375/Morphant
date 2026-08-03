using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceGenericTests
{
    [Test]
    public void Reuses_one_constrained_generic_plan_for_closed_destinations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Factory { public Factory() { } }

    public sealed class Outer<TOuter>
        where TOuter : class
    {
        public sealed class Destination<TValue, TFactory>
            where TValue : unmanaged
            where TFactory : class?, new()
        {
            public Destination(
                TOuter outer,
                TValue value,
                TFactory factory) { }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Outer<string>.Destination<int, Factory>>();
            builder.Map<Source, Outer<object>.Destination<long, Factory>>();
        }
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var plans = generated
            .Where(static pair =>
                pair.Key.Contains(".Construction.", StringComparison.Ordinal))
            .ToArray();

        Assert.That(plans, Has.Length.EqualTo(1));
        Assert.That(
            plans[0].Value,
            Does.Contain(
                "internal sealed class DestinationConstruction<TOuter, TValue, TFactory>"));
        Assert.That(
            plans[0].Value,
            Does.Contain("where TOuter : class"));
        Assert.That(
            plans[0].Value,
            Does.Contain("where TValue : unmanaged"));
        Assert.That(
            plans[0].Value,
            Does.Contain("where TFactory : class?, new()"));
        Assert.That(
            generated.Keys.Count(static name =>
                name.Contains(".MappingExtension.", StringComparison.Ordinal)),
            Is.EqualTo(2));
        Assert.That(
            generated.Values,
            Has.Some.Contains(
                "DestinationConstruction<string, int, global::TestCase.Factory>"));
        Assert.That(
            generated.Values,
            Has.Some.Contains(
                "DestinationConstruction<object, long, global::TestCase.Factory>"));
    }

    [Test]
    public void Uses_only_definition_constraints_for_alpha_equivalent_pairs()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public interface IFirst { }
    public interface ISecond { }

    public sealed class Source<T>
        where T : class
    {
        public T Value { get; init; } = null!;
    }

    public sealed class Destination<T>
        where T : class
    {
        public Destination(T value) { }
    }

    [MorphantMapper]
    public partial class FirstMapper<T> : TypeMapper
        where T : class, IFirst
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Construct(source => new(source.Value));
    }

    [MorphantMapper]
    public partial class SecondMapper<U> : TypeMapper
        where U : class, ISecond
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<U>, Destination<U>>()
                .Construct(source => new(source.Value));
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var extensions = generated
            .Where(static pair =>
                pair.Key.Contains(
                    ".MappingExtension.",
                    StringComparison.Ordinal))
            .ToArray();

        Assert.That(extensions, Has.Length.EqualTo(1));
        Assert.That(
            extensions[0].Value,
            Does.Contain("Construct<T>("));
        Assert.That(
            extensions[0].Value,
            Does.Contain("where T : class"));
        Assert.That(
            extensions[0].Value,
            Does.Not.Contain("IFirst"));
        Assert.That(
            extensions[0].Value,
            Does.Not.Contain("ISecond"));
        Assert.That(
            extensions[0].Value,
            Does.Contain(
                "DestinationConstruction<T>"));
    }

    [Test]
    public void Omits_different_mapper_constraints_from_a_shared_extension()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class Destination<T>
    {
        public Destination(T value) { }
    }

    [MorphantMapper]
    public partial class ReferenceMapper<T> : TypeMapper
        where T : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Construct(source => new(source.Value));
    }

    [MorphantMapper]
    public partial class ValueMapper<T> : TypeMapper
        where T : struct
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Construct(source => new(source.Value));
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var extensions = generated
            .Where(static pair =>
                pair.Key.Contains(
                    ".MappingExtension.",
                    StringComparison.Ordinal))
            .ToArray();

        Assert.That(extensions, Has.Length.EqualTo(1));
        Assert.That(
            extensions[0].Value,
            Does.Contain("Construct<T>("));
        Assert.That(
            extensions[0].Value,
            Does.Not.Contain("where T"));
    }

    [Test]
    public void Merges_and_substitutes_constraints_from_pair_definitions()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public interface IMarker<T> { }

    public sealed class Source<TValue, TDependency>
        where TValue : class?, IMarker<TDependency>?
        where TDependency : class, new()
    {
        public TValue Value { get; init; } = default!;
    }

    public sealed class Destination<TValue, TDependency>
        where TValue : notnull, IMarker<TDependency>
        where TDependency : class?, new()
    {
        public Destination(TValue value, TDependency dependency) { }
    }

    [MorphantMapper]
    public partial class TestMapper<TValue, TDependency> : TypeMapper
        where TValue : class, IMarker<TDependency>
        where TDependency : class, new()
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                    Source<TValue, TDependency>,
                    Destination<TValue, TDependency>>()
                .Construct(source => new(
                    source.Value,
                    new TDependency()));
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

        Assert.That(
            extension,
            Does.Contain(
                "where TValue : class, global::TestCase.IMarker<TDependency>"));
        Assert.That(
            extension,
            Does.Contain("where TDependency : class, new()"));
        Assert.That(
            CountOccurrences(
                extension,
                "global::TestCase.IMarker<TDependency>"),
            Is.EqualTo(3));
    }

    [Test]
    public void Preserves_containing_definition_constraints_in_open_pairs()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Outer<TOuter>
        where TOuter : class
    {
        public sealed class Source<TValue>
            where TValue : TOuter
        {
            public TValue Value { get; init; } = default!;
        }

        public sealed class Destination<TValue>
            where TValue : TOuter
        {
            public Destination(TValue value) { }
        }
    }

    [MorphantMapper]
    public partial class TestMapper<TOuter, TValue> : TypeMapper
        where TOuter : class
        where TValue : TOuter
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                    Outer<TOuter>.Source<TValue>,
                    Outer<TOuter>.Destination<TValue>>()
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

        Assert.That(
            extension,
            Does.Contain("where TOuter : class"));
        Assert.That(
            extension,
            Does.Contain("where TValue : TOuter"));
        Assert.That(
            extension,
            Does.Contain(
                "DestinationConstruction<TOuter, TValue>"));
    }

    [Test]
    public void Substitutes_closed_types_in_definition_constraints()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public interface IMarker<T> { }

    public sealed class Source<TValue, TMarker>
        where TValue : IMarker<TMarker>
    {
        public TValue Value { get; init; } = default!;
    }

    public sealed class Destination<TValue, TMarker>
        where TValue : IMarker<TMarker>
    {
        public Destination(TValue value) { }
    }

    [MorphantMapper]
    public partial class TestMapper<TValue> : TypeMapper
        where TValue : IMarker<int>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                    Source<TValue, int>,
                    Destination<TValue, int>>()
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

        Assert.That(
            extension,
            Does.Contain(
                "where TValue : global::TestCase.IMarker<int>"));
        Assert.That(
            extension,
            Does.Contain(
                "DestinationConstruction<TValue, int>"));
        Assert.That(
            extension,
            Does.Not.Contain("@int"));
    }

    [Test]
    public void Renames_shadowed_containing_type_parameters_in_the_plan()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591, CS0693

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Outer<T>
    {
        public sealed class Destination<T>
        {
            public Destination(
                Outer<T> outer,
                T value) { }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Outer<string>.Destination<int>>();
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var plan = generated.Values.Single(static value =>
            value.Contains(
                "internal sealed class DestinationConstruction",
                StringComparison.Ordinal));

        Assert.That(
            plan,
            Does.Contain(
                "internal sealed class DestinationConstruction<T, T2>"));
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
