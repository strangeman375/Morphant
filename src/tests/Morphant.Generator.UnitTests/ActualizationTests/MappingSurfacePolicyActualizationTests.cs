using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorActualizationTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class MappingSurfacePolicyActualizationTests
{
    [Test]
    public void Shares_reference_surface_across_nullable_presentations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public string Value { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string Value { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class NullableMapper : TypeMapper<NullableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source?, Destination>()
                .Members(source => new()
                {
                    Value = source?.Value ?? string.Empty
                });
    }

    [MorphantMapper]
    public partial class NonNullableMapper : TypeMapper<NonNullableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    Value = source.Value
                });
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "ReferenceNullablePresentations",
            source,
            LanguageVersion.CSharp9);

        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public void Shares_SystemTuple_surface_across_nullable_presentations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public string? Value { get; init; }
    }

    [MorphantMapper]
    public partial class NullableMapper : TypeMapper<NullableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, System.Tuple<string?>>()
                .Members(source => new()
                {
                    Item1 = source.Value
                });
    }

    [MorphantMapper]
    public partial class NonNullableMapper : TypeMapper<NonNullableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, System.Tuple<string>>()
                .Members(source => new()
                {
                    Item1 = source.Value ?? string.Empty
                });
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "SystemTupleNullablePresentations",
            source,
            LanguageVersion.CSharp9);

        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public async Task Generates_independent_concrete_ValueTuple_scopes()
    {
        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            ConcreteScopesSource,
            (FirstScopedHintName, FirstScopedExtension),
            (SecondScopedHintName, SecondScopedExtension));
    }

    [Test]
    public void Actualizes_stable_shared_and_family_scoped_tuple_rules()
    {
        RunAndAssert(
            LanguageVersion.CSharp9,
            new TestConstructionSurfaceGenerator(),
            Step(
                "closed System.Tuple pair is shared",
                SharedSource,
                (SharedHintName, SharedExtension)),
            Step(
                "nested ValueTuple pair is mapper-family-scoped",
                ScopedSource,
                (ScopedHintName, ScopedExtension)),
            Step(
                "overlapping base and leaf keep the family surface",
                OverlappingSource,
                (ScopedHintName, ScopedExtension)),
            Step(
                "closed System.Tuple pair is shared again",
                SharedSource,
                (SharedHintName, SharedExtension)));
    }

    private const string SharedHintName =
        "Morphant.Generated.MappingExtension." +
        "System_Tuple_System_Int32__System_Int32___System_Int32.g.cs";

    private const string ScopedHintName =
        "Morphant.Generated.MappingExtension." +
        "System_Tuple_System_ValueTuple_System_Int32__System_Int32____" +
        "System_Int32__TestCase_CommonMapper_TMapper_.g.cs";

    private const string FirstScopedHintName =
        "Morphant.Generated.MappingExtension." +
        "System_ValueTuple_System_Int32__System_Int32___System_Int32__" +
        "TestCase_FirstMapper.g.cs";

    private const string SecondScopedHintName =
        "Morphant.Generated.MappingExtension." +
        "System_ValueTuple_System_Int32__System_Int32___System_Int32__" +
        "TestCase_SecondMapper.g.cs";

    // lang=c#
    private const string SharedSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : CommonMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<System.Tuple<int, int>, int>();
    }

    [MorphantMapper]
    public partial class LeafMapper : CommonMapper<LeafMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }
}
""";

    // lang=c#
    private const string ScopedSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : CommonMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<System.Tuple<(int X, int Y)>, int>();
    }

    [MorphantMapper]
    public partial class LeafMapper : CommonMapper<LeafMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }
}
""";

    // lang=c#
    private const string OverlappingSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : CommonMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<System.Tuple<(int X, int Y)>, int>()
                .Convert(_ => 1);
    }

    [MorphantMapper]
    public partial class LeafMapper : CommonMapper<LeafMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<System.Tuple<(int X, int Y)>, int>()
                .Convert(_ => 2);
        }
    }
}
""";

    // lang=c#
    private const string ConcreteScopesSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class FirstMapper : TypeMapper<FirstMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int X, int Y), int>().Convert(_ => 1);
    }

    [MorphantMapper]
    public partial class SecondMapper : TypeMapper<SecondMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int A, int B), int>().Convert(_ => 2);
    }
}
""";

    // lang=c#
    private const string SharedExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        /// <summary>
        /// Uses a callback to construct a destination when none exists.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">The construction callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> ConstructUsing<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> builder,
            global::Morphant.Delegates.ConstructUsing<global::System.Tuple<int, int>, int> construct)
            where TMapper : global::Morphant.TypeMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with context to construct a destination.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">The construction callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> ConstructUsing<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> builder,
            global::Morphant.Delegates.ConstructUsing<global::System.Tuple<int, int>, global::Morphant.Context.MappingContext, int> construct)
            where TMapper : global::Morphant.TypeMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback to select or construct the destination.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">The result callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> ResolveUsing<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> builder,
            global::Morphant.Delegates.ResolveUsing<global::System.Tuple<int, int>, int, int> resolve)
            where TMapper : global::Morphant.TypeMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with context to select or construct the destination.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">The result callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> ResolveUsing<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> builder,
            global::Morphant.Delegates.ResolveUsing<global::System.Tuple<int, int>, int, global::Morphant.Context.MappingContext, int> resolve)
            where TMapper : global::Morphant.TypeMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback as the complete mapping algorithm.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> Convert<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<int, int>?, int> mapping)
            where TMapper : global::Morphant.TypeMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with the optional existing destination as the complete mapping algorithm.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> Convert<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<int, int>?, int, int> mapping)
            where TMapper : global::Morphant.TypeMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with the optional existing destination and context as the complete mapping algorithm.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> Convert<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<int, int>, int> builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<int, int>?, int, global::Morphant.Context.MappingContext, int> mapping)
            where TMapper : global::Morphant.TypeMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

    // lang=c#
    private const string ScopedExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        /// <summary>
        /// Uses a callback to construct a destination when none exists.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">The construction callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> ConstructUsing<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> builder,
            global::Morphant.Delegates.ConstructUsing<global::System.Tuple<(int X, int Y)>, int> construct)
            where TMapper : global::TestCase.CommonMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with context to construct a destination.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">The construction callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> ConstructUsing<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> builder,
            global::Morphant.Delegates.ConstructUsing<global::System.Tuple<(int X, int Y)>, global::Morphant.Context.MappingContext, int> construct)
            where TMapper : global::TestCase.CommonMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback to select or construct the destination.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">The result callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> ResolveUsing<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> builder,
            global::Morphant.Delegates.ResolveUsing<global::System.Tuple<(int X, int Y)>, int, int> resolve)
            where TMapper : global::TestCase.CommonMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with context to select or construct the destination.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">The result callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> ResolveUsing<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> builder,
            global::Morphant.Delegates.ResolveUsing<global::System.Tuple<(int X, int Y)>, int, global::Morphant.Context.MappingContext, int> resolve)
            where TMapper : global::TestCase.CommonMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback as the complete mapping algorithm.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> Convert<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<(int X, int Y)>?, int> mapping)
            where TMapper : global::TestCase.CommonMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with the optional existing destination as the complete mapping algorithm.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> Convert<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<(int X, int Y)>?, int, int> mapping)
            where TMapper : global::TestCase.CommonMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with the optional existing destination and context as the complete mapping algorithm.
        /// </summary>
        /// <typeparam name="TMapper">A type parameter from the mapping declaration.</typeparam>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> Convert<TMapper>(
            this global::Morphant.MappingBuilder<TMapper, global::System.Tuple<(int X, int Y)>, int> builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<(int X, int Y)>?, int, global::Morphant.Context.MappingContext, int> mapping)
            where TMapper : global::TestCase.CommonMapper<TMapper>
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

    // lang=c#
    private const string FirstScopedExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        /// <summary>
        /// Uses a callback to construct a destination when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">The construction callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> builder,
            global::Morphant.Delegates.ConstructUsing<(int X, int Y), int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with context to construct a destination.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">The construction callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> builder,
            global::Morphant.Delegates.ConstructUsing<(int X, int Y), global::Morphant.Context.MappingContext, int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback to select or construct the destination.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">The result callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> builder,
            global::Morphant.Delegates.ResolveUsing<(int X, int Y), int, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with context to select or construct the destination.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">The result callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> builder,
            global::Morphant.Delegates.ResolveUsing<(int X, int Y), int, global::Morphant.Context.MappingContext, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback as the complete mapping algorithm.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> Convert(
            this global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> builder,
            global::Morphant.Delegates.Convert<(int X, int Y), int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with the optional existing destination as the complete mapping algorithm.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> Convert(
            this global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> builder,
            global::Morphant.Delegates.Convert<(int X, int Y), int, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with the optional existing destination and context as the complete mapping algorithm.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> Convert(
            this global::Morphant.MappingBuilder<global::TestCase.FirstMapper, (int X, int Y), int> builder,
            global::Morphant.Delegates.Convert<(int X, int Y), int, global::Morphant.Context.MappingContext, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

    // lang=c#
    private const string SecondScopedExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        /// <summary>
        /// Uses a callback to construct a destination when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">The construction callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> builder,
            global::Morphant.Delegates.ConstructUsing<(int A, int B), int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with context to construct a destination.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">The construction callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> builder,
            global::Morphant.Delegates.ConstructUsing<(int A, int B), global::Morphant.Context.MappingContext, int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback to select or construct the destination.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">The result callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> builder,
            global::Morphant.Delegates.ResolveUsing<(int A, int B), int, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with context to select or construct the destination.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">The result callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> builder,
            global::Morphant.Delegates.ResolveUsing<(int A, int B), int, global::Morphant.Context.MappingContext, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback as the complete mapping algorithm.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> Convert(
            this global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> builder,
            global::Morphant.Delegates.Convert<(int A, int B), int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with the optional existing destination as the complete mapping algorithm.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> Convert(
            this global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> builder,
            global::Morphant.Delegates.Convert<(int A, int B), int, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Uses a callback with the optional existing destination and context as the complete mapping algorithm.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">The mapping callback.</param>
        /// <returns>The same mapping builder.</returns>
        public static global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> Convert(
            this global::Morphant.MappingBuilder<global::TestCase.SecondMapper, (int A, int B), int> builder,
            global::Morphant.Delegates.Convert<(int A, int B), int, global::Morphant.Context.MappingContext, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";
}
