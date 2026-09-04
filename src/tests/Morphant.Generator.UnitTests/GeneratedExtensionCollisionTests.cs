using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class GeneratedExtensionCollisionTests
{
    [Test]
    public void Generated_plan_namespace_does_not_shadow_runtime_namespace()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    [Morphant.MorphantMapper]
    public partial class TestMapper : Morphant.TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "GeneratedNamespaceShadowing",
            source,
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void User_type_named_Morphant_does_not_collide_with_plan_namespace()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Morphant { }
    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "UserMorphantType",
            source,
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Plan_types_and_encoded_namespace_scopes_cannot_compete()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace A
{
    public sealed class Source { }
    public sealed class N_B { public int Value { get; set; } }
}

namespace A.BConstruction
{
    public sealed class Source { }
    public sealed class Destination { public int Value { get; set; } }
}

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<A.Source, A.N_B>();
            builder.Map<
                A.BConstruction.Source,
                A.BConstruction.Destination>();
        }
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "GeneratedPlanScopeCollision",
            source,
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void User_extension_with_a_generated_signature_is_reported_instead_of_lowered()
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
    public sealed class Destination { }

    internal static class UserExtensions
    {
        public static MappingBuilder<TMapper, TSource, TDestination> Convert<
            TMapper,
            TSource,
            TDestination>(
            this MappingBuilder<TMapper, TSource, TDestination> builder,
            global::Morphant.Delegates.Convert<TSource?, TDestination> mapping)
            where TMapper : TypeMapper<TMapper> => builder;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert(_ => new Destination());
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "UserExtensionCollision",
            source,
            LanguageVersion.CSharp9);
        var diagnostic = result.EffectiveDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0018"));
            Assert.That(
                GeneratorTestDriver.GetSourceText(diagnostic.Location),
                Is.EqualTo("Convert"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void User_method_in_reserved_partial_container_is_not_trusted_as_generated()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        public static MappingBuilder<TMapper, TSource, TDestination> Convert<
            TMapper,
            TSource,
            TDestination>(
            this MappingBuilder<TMapper, TSource, TDestination> builder,
            Action callback)
            where TMapper : TypeMapper<TMapper> => builder;
    }
}

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert(() => { });
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "UserReservedContainerMethod",
            [
                new GeneratorTestSourceFile(
                    "Morphant.Generated.MappingExtension.User.g.cs",
                    source)
            ],
            LanguageVersion.CSharp9);
        var diagnostic = result.EffectiveDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0018"));
            Assert.That(
                GeneratorTestDriver.GetSourceText(diagnostic.Location),
                Is.EqualTo("Convert"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Layered_competing_extensions_are_not_silently_dropped()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        public static MappingBuilder<
            TMapper,
            global::TestCase.Source,
            global::TestCase.Destination> Convert<TMapper>(
            this MapperBuilderBase<MappingBuilder<
                TMapper,
                global::TestCase.Source,
                global::TestCase.Destination>> builder,
            global::Morphant.Delegates.Convert<
                global::TestCase.Source?,
                global::TestCase.Destination> mapping,
            bool compilerFallback = false)
            where TMapper : TypeMapper<TMapper> =>
            throw new global::Morphant.Exceptions
                .RuntimeInvocationNotSupportedException();
    }
}

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    internal static class UserExtensions
    {
        public static MappingBuilder<TMapper, TSource, TDestination> Tap<
            TMapper,
            TSource,
            TDestination>(
            this MappingBuilder<TMapper, TSource, TDestination> builder)
            where TMapper : TypeMapper<TMapper> => builder;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert(_ => new Destination())
                .Tap();
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "LayeredUserExtensionCollision",
            source,
            LanguageVersion.CSharp9);
        var diagnostic = result.EffectiveDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0018"));
            Assert.That(
                GeneratorTestDriver.GetSourceText(diagnostic.Location),
                Is.EqualTo("Convert"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Competing_extension_diagnostic_actualizes_when_source_changes()
    {
        // lang=c#
        const string validSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert(_ => new Destination());
    }
}
""";
        // lang=c#
        const string conflictingSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    internal static class UserExtensions
    {
        public static MappingBuilder<TMapper, TSource, TDestination> Convert<
            TMapper,
            TSource,
            TDestination>(
            this MappingBuilder<TMapper, TSource, TDestination> builder,
            global::Morphant.Delegates.Convert<
                TSource?,
                TDestination> mapping)
            where TMapper : TypeMapper<TMapper> => builder;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert(_ => new Destination());
    }
}
""";

        var valid = GeneratorTestDriver.Run(
            "ExtensionCollisionActualization",
            validSource,
            LanguageVersion.CSharp9);
        var conflicting = GeneratorTestDriver.Run(
            "ExtensionCollisionActualization",
            conflictingSource,
            LanguageVersion.CSharp9,
            driver: valid.Driver);
        var restored = GeneratorTestDriver.Run(
            "ExtensionCollisionActualization",
            validSource,
            LanguageVersion.CSharp9,
            driver: conflicting.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(valid.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                conflicting.EffectiveDiagnostics.Select(
                    static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0018" }));
            Assert.That(restored.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                conflicting.TypeMapperSource,
                Does.Contain("Morphant cannot analyze this mapping " +
                    "configuration."));
            Assert.That(
                restored.TypeMapperSource,
                Is.EqualTo(valid.TypeMapperSource));
            Assert.That(valid.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(conflicting.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(restored.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Nested_destinations_with_ambiguous_readable_scopes_use_distinct_plan_names()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class FirstSource { }
    public sealed class SecondSource { }

    public sealed class Outer<T>
    {
        public sealed class Destination
        {
            public int Value { get; set; }
        }
    }

    public sealed class Outer1
    {
        public sealed class Destination<T>
        {
            public int Value { get; set; }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<FirstSource, Outer<int>.Destination>();
            builder.Map<SecondSource, Outer1.Destination<int>>();
        }
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "NestedPlanNameCollisionProbe",
            source,
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

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
