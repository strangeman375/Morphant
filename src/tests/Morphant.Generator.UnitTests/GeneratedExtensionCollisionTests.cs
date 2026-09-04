using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class GeneratedExtensionCollisionTests
{
    [Test]
    public void Unrelated_CRTP_families_use_distinct_extension_containers()
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
        public Destination(T value) => Value = value;

        public T Value { get; set; }
    }

    public abstract class FirstFamily<TMapper, T> : TypeMapper<TMapper>
        where TMapper : FirstFamily<TMapper, T>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Convert(_ => new Destination<T>(default!));
    }

    public abstract class SecondFamily<TMapper, T> : TypeMapper<TMapper>
        where TMapper : SecondFamily<TMapper, T>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Members(source => new()
                {
                    Value = source.Value
                });
    }

    [MorphantMapper]
    public partial class FirstMapper :
        FirstFamily<FirstMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<string>, Destination<string>>()
                .IncludeBase<Source<string>, Destination<string>>();
        }
    }

    [MorphantMapper]
    public partial class SecondMapper :
        SecondFamily<SecondMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<string>, Destination<string>>()
                .IncludeBase<Source<string>, Destination<string>>();
        }
    }

}
""";

        var result = GeneratorTestDriver.Run(
            "UnrelatedCrtpFamilies",
            source,
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Related_CRTP_families_do_not_create_competing_extensions()
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
        public T Value { get; set; } = default!;
    }

    public abstract class RootFamily<TMapper, T> : TypeMapper<TMapper>
        where TMapper : RootFamily<TMapper, T>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Convert(_ => new Destination<T>());
    }

    public abstract class DerivedFamily<TMapper, T> :
        RootFamily<TMapper, T>
        where TMapper : DerivedFamily<TMapper, T>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<T>, Destination<T>>()
                .Members(_ => new() { Value = default! });
        }
    }

    [MorphantMapper]
    public partial class RootMapper : RootFamily<RootMapper, string>
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }

    [MorphantMapper]
    public partial class DerivedMapper :
        DerivedFamily<DerivedMapper, string>
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "RelatedCrtpFamilies",
            source,
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Shared_and_erased_scoped_surfaces_select_exact_receivers()
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
        public Destination(T value) => Value = value;

        public T Value { get; set; }
    }

    [MorphantMapper]
    public partial class ObjectMapper : TypeMapper<ObjectMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<object>, Destination<object>>();
    }

    [MorphantMapper]
    public partial class DynamicMapper : TypeMapper<DynamicMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var mapping =
                builder.Map<Source<dynamic>, Destination<dynamic>>();

            mapping.Construct(_ => new(ByConvention()));
            mapping.Construct((_, _) => new(ByConvention()));
            mapping.Resolve((_, _) => new(ByConvention()));
            mapping.Resolve((_, _, _) => new(ByConvention()));
            mapping.ConstructUsing(_ =>
                new Destination<dynamic>(default!));
            mapping.ConstructUsing((_, _) =>
                new Destination<dynamic>(default!));
            mapping.ResolveUsing((_, _) =>
                new Destination<dynamic>(default!));
            mapping.ResolveUsing((_, _, _) =>
                new Destination<dynamic>(default!));
            mapping.Convert(_ =>
                new Destination<dynamic>(default!));
            mapping.Convert((_, _) =>
                new Destination<dynamic>(default!));
            mapping.Convert((_, _, _) =>
                new Destination<dynamic>(default!));
            mapping.Members(_ => new() { Value = default! });
            mapping.Members((_, _) => new() { Value = default! });
            mapping.Members((_, _, _) =>
                new() { Value = default! });
            mapping.Members((_, _, _, _) =>
                new() { Value = default! });
        }
    }

    [MorphantMapper]
    public partial class StringMapper : TypeMapper<StringMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<string>, Destination<string>>();
    }

    [MorphantMapper]
    public partial class NullableMapper : TypeMapper<NullableMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var mapping =
                builder.Map<Source<string?>, Destination<string?>>();

            mapping.Construct(_ => new(ByConvention()));
            mapping.Resolve((_, _) => new(ByConvention()));
            mapping.ConstructUsing(_ =>
                new Destination<string?>(default!));
            mapping.ResolveUsing((_, _) =>
                new Destination<string?>(default!));
            mapping.Convert(_ =>
                new Destination<string?>(default!));
            mapping.Members(_ => new() { Value = default! });
        }
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "SharedAndScopedExtensionSelection",
            source,
            LanguageVersion.CSharp9);

        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }
}
