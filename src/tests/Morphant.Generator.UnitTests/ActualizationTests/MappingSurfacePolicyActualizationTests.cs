using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorActualizationTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class MappingSurfacePolicyActualizationTests
{
    [Test]
    public void Shares_the_reference_surface_across_outer_nullable_annotations()
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
    public partial class NullableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source?, Destination>()
                .Members(source => new()
                {
                    Value = source?.Value ?? string.Empty
                });
    }

    [MorphantMapper]
    public partial class NonNullableMapper : TypeMapper
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

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public void Shares_a_SystemTuple_surface_with_nullable_elements_across_outer_annotations()
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
    public partial class NullableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, System.Tuple<string?>>()
                .Members(source => new()
                {
                    Item1 = source.Value
                });
    }

    [MorphantMapper]
    public partial class NonNullableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source?, System.Tuple<string?>?>()
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

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public async Task Generates_one_extension_for_a_shared_concrete_ValueTuple_pair()
    {
        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            SharedTupleSource,
            (TupleHintName, TupleExtension));
    }

    [Test]
    public void Actualizes_the_shared_receiver_across_inherited_tuple_edits()
    {
        RunAndAssert(
            LanguageVersion.CSharp9,
            new TestConstructionSurfaceGenerator(),
            Step(
                "System.Tuple pair in the base mapper",
                SystemTupleSource,
                (SystemTupleHintName, SystemTupleExtension)),
            Step(
                "nested ValueTuple pair in the base mapper",
                ScopedSource,
                (ScopedHintName, ScopedExtension)),
            Step(
                "base and leaf share the pair extension",
                OverlappingSource,
                (ScopedHintName, ScopedExtension)),
            Step(
                "System.Tuple pair restored in the same family",
                SystemTupleSource,
                (SystemTupleHintName, SystemTupleExtension)));
    }

    private const string SystemTupleHintName =
        "Morphant.Generated.MappingExtension.SystemTuple2ToInt32__66103d10a4b0b473cb67746889503d48.g.cs";

    private const string ScopedHintName =
        "Morphant.Generated.MappingExtension.SystemTuple1ToInt32__d0836a08294859deb6676a5a9992a81c.g.cs";

    private const string TupleHintName =
        "Morphant.Generated.MappingExtension.ValueTuple2ToInt32__93c9bbeb0e1819a269c99342ae7550b0.g.cs";


    // lang=c#
    private const string SystemTupleSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public abstract class CommonMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<System.Tuple<int, int>, int>();
    }

    [MorphantMapper]
    public partial class LeafMapper : CommonMapper
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
    public abstract class CommonMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<System.Tuple<(int X, int Y)>, int>();
    }

    [MorphantMapper]
    public partial class LeafMapper : CommonMapper
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
    public abstract class CommonMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<System.Tuple<(int X, int Y)>, int>();
    }

    [MorphantMapper]
    public partial class LeafMapper : CommonMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<System.Tuple<(int X, int Y)>, int>();
        }
    }
}
""";

    // lang=c#
    private const string SharedTupleSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class FirstMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int X, int Y), int>();
    }

    [MorphantMapper]
    public partial class SecondMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int X, int Y), int>();
    }
}
""";

    // lang=c#
    private const string SystemTupleExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<int, int>, int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::System.Tuple<int, int
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.ConstructUsing<global::System.Tuple<int, int>, int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<int, int>, int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::System.Tuple<int, int
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.ConstructUsing<global::System.Tuple<int, int>, global::Morphant.Context.MappingContext, int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<int, int>, int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::System.Tuple<int, int
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.ResolveUsing<global::System.Tuple<int, int>, int, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<int, int>, int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::System.Tuple<int, int
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.ResolveUsing<global::System.Tuple<int, int>, int, global::Morphant.Context.MappingContext, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<int, int>, int> Convert(
            this global::Morphant.MappingBuilder<global::System.Tuple<int, int
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<int, int>?, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<int, int>, int> Convert(
            this global::Morphant.MappingBuilder<global::System.Tuple<int, int
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<int, int>?, int, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<int, int>, int> Convert(
            this global::Morphant.MappingBuilder<global::System.Tuple<int, int
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<int, int>?, int, global::Morphant.Context.MappingContext, int> mapping)
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
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)>, int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.ConstructUsing<global::System.Tuple<(int X, int Y)>, int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)>, int> ConstructUsing(
            this global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.ConstructUsing<global::System.Tuple<(int X, int Y)>, global::Morphant.Context.MappingContext, int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)>, int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.ResolveUsing<global::System.Tuple<(int X, int Y)>, int, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)>, int> ResolveUsing(
            this global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.ResolveUsing<global::System.Tuple<(int X, int Y)>, int, global::Morphant.Context.MappingContext, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)>, int> Convert(
            this global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<(int X, int Y)>?, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)>, int> Convert(
            this global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<(int X, int Y)>?, int, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)>, int> Convert(
            this global::Morphant.MappingBuilder<global::System.Tuple<(int X, int Y)
        #nullable disable annotations
                >, int
        #nullable enable annotations
                > builder,
            global::Morphant.Delegates.Convert<global::System.Tuple<(int X, int Y)>?, int, global::Morphant.Context.MappingContext, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

    // lang=c#
    private const string TupleExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<(int X, int Y), int> ConstructUsing(
            this global::Morphant.MappingBuilder<(int X, int Y), int> builder,
            global::Morphant.Delegates.ConstructUsing<(int X, int Y), int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<(int X, int Y), int> ConstructUsing(
            this global::Morphant.MappingBuilder<(int X, int Y), int> builder,
            global::Morphant.Delegates.ConstructUsing<(int X, int Y), global::Morphant.Context.MappingContext, int> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<(int X, int Y), int> ResolveUsing(
            this global::Morphant.MappingBuilder<(int X, int Y), int> builder,
            global::Morphant.Delegates.ResolveUsing<(int X, int Y), int, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<(int X, int Y), int> ResolveUsing(
            this global::Morphant.MappingBuilder<(int X, int Y), int> builder,
            global::Morphant.Delegates.ResolveUsing<(int X, int Y), int, global::Morphant.Context.MappingContext, int> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<(int X, int Y), int> Convert(
            this global::Morphant.MappingBuilder<(int X, int Y), int> builder,
            global::Morphant.Delegates.Convert<(int X, int Y), int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<(int X, int Y), int> Convert(
            this global::Morphant.MappingBuilder<(int X, int Y), int> builder,
            global::Morphant.Delegates.Convert<(int X, int Y), int, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<(int X, int Y), int> Convert(
            this global::Morphant.MappingBuilder<(int X, int Y), int> builder,
            global::Morphant.Delegates.Convert<(int X, int Y), int, global::Morphant.Context.MappingContext, int> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

}
