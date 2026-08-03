using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingPairTests;

[TestFixture]
internal sealed class MappingPairCanonicalIdentityTests
{
    [Test]
    public async Task Removes_non_runtime_shape_from_canonical_pair_identity()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using AliasInt = System.Int32;
using Morphant;

namespace TestCase
{
    public sealed class Envelope<T> { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<dynamic, object?>();
            builder.Map<object?, dynamic>();

            builder.Map<AliasInt, Destination>();
            builder.Map<int, Destination>();

            builder.Map<Envelope<(int Id, string? Name)>, Destination>();
            builder.Map<Envelope<(int, string)>, Destination>();

            builder.Map<Envelope<string?>, Destination>();
            builder.Map<Envelope<string>, Destination>();

            builder.Map<nint, nuint>();
            builder.Map<IntPtr, UIntPtr>();
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            new MappingPairExpectation(
                "global::System.Object",
                "global::System.Object",
                Structured: false,
                Members: false),
            Pair("global::System.Int32"),
            Pair(
                "global::TestCase.Envelope<global::System.ValueTuple<global::System.Int32, global::System.String>>"),
            Pair(
                "global::TestCase.Envelope<global::System.String>"),
            new MappingPairExpectation(
                "global::System.IntPtr",
                "global::System.UIntPtr",
                Structured: false,
                Members: false));
    }

    [Test]
    public async Task Preserves_nullable_value_roots_as_distinct_pairs()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<int, Destination>();
            builder.Map<int?, Destination>();
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Pair("global::System.Int32"),
            Pair("global::System.Nullable<global::System.Int32>"));
    }

    [Test]
    public async Task Preserves_case_distinctions_for_future_hint_name_allocation()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase.Upper
{
    public sealed class Source { }
    public sealed class Destination { }
}

namespace TestCase.upper
{
    public sealed class Source { }
    public sealed class Destination { }
}

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Upper.Source, Upper.Destination>();
            builder.Map<upper.Source, upper.Destination>();
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            new MappingPairExpectation(
                "global::TestCase.Upper.Source",
                "global::TestCase.Upper.Destination",
                Structured: true,
                Members: false),
            new MappingPairExpectation(
                "global::TestCase.upper.Source",
                "global::TestCase.upper.Destination",
                Structured: true,
                Members: false));
    }

    [Test]
    public async Task Marks_distinct_open_pairs_that_can_unify()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Envelope<T> { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Envelope<T>, Destination>();
            builder.Map<Envelope<int>, Destination>();
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper`1",
            hasUnifiablePairs: true,
            Pair("global::TestCase.Envelope<T>"),
            Pair(
                "global::TestCase.Envelope<global::System.Int32>"));
    }

    private static MappingPairExpectation Pair(string source)
    {
        return new MappingPairExpectation(
            source,
            "global::TestCase.Destination",
            Structured: true,
            Members: false);
    }
}
