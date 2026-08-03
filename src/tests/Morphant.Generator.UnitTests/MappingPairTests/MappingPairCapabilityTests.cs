using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingPairTests;

[TestFixture]
internal sealed class MappingPairCapabilityTests
{
    [Test]
    public async Task Computes_all_construction_and_member_combinations_independently()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class StructuredOnly
    {
        public StructuredOnly(int value) { }
    }

    public sealed class StructuredWithMembers
    {
        public StructuredWithMembers(int value) { }
        public int Value { get; set; }
    }

    public abstract class DirectOnly { }

    public interface IDirectWithMembers
    {
        int Value { get; set; }
    }

    public sealed class FactoryOnly
    {
        private FactoryOnly() { }
    }

    public struct CustomStruct { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, StructuredOnly>();
            builder.Map<Source, StructuredWithMembers>();
            builder.Map<Source, DirectOnly>();
            builder.Map<Source, IDirectWithMembers>();
            builder.Map<Source, FactoryOnly>();
            builder.Map<Source, CustomStruct>();
            builder.Map<Source, CustomStruct?>();
        }
    }
}
""";

        const string sourceType = "global::TestCase.Source";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Pair(sourceType, "StructuredOnly", true, false),
            Pair(sourceType, "StructuredWithMembers", true, true),
            Pair(sourceType, "DirectOnly", false, false),
            Pair(sourceType, "IDirectWithMembers", false, true),
            Pair(sourceType, "FactoryOnly", false, false),
            Pair(sourceType, "CustomStruct", true, false),
            new MappingPairExpectation(
                sourceType,
                "global::System.Nullable<global::TestCase.CustomStruct>",
                Structured: true,
                Members: false));
    }

    [Test]
    public async Task Applies_the_exact_opaque_scalar_policy()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Numerics;
using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public enum Status { None }
    public struct CustomStruct { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, object>();
            builder.Map<Source, string>();
            builder.Map<Source, bool>();
            builder.Map<Source, int>();
            builder.Map<Source, decimal>();
            builder.Map<Source, nint>();
            builder.Map<Source, nuint>();
            builder.Map<Source, Status>();
            builder.Map<Source, Guid>();
            builder.Map<Source, Guid?>();
            builder.Map<Source, DateTime>();
            builder.Map<Source, DateTimeOffset>();
            builder.Map<Source, TimeSpan>();

            builder.Map<Source, DateOnly>();
            builder.Map<Source, TimeOnly>();
            builder.Map<Source, Uri>();
            builder.Map<Source, Version>();
            builder.Map<Source, BigInteger>();
            builder.Map<Source, Index>();
            builder.Map<Source, Range>();
            builder.Map<Source, CustomStruct>();
        }
    }
}
""";

        const string sourceType = "global::TestCase.Source";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Direct(sourceType, "global::System.Object"),
            Direct(sourceType, "global::System.String"),
            Direct(sourceType, "global::System.Boolean"),
            Direct(sourceType, "global::System.Int32"),
            Direct(sourceType, "global::System.Decimal"),
            Direct(sourceType, "global::System.IntPtr"),
            Direct(sourceType, "global::System.UIntPtr"),
            Direct(sourceType, "global::TestCase.Status"),
            Direct(sourceType, "global::System.Guid"),
            Direct(
                sourceType,
                "global::System.Nullable<global::System.Guid>"),
            Direct(sourceType, "global::System.DateTime"),
            Direct(sourceType, "global::System.DateTimeOffset"),
            Direct(sourceType, "global::System.TimeSpan"),
            Structured(sourceType, "global::System.DateOnly"),
            Structured(sourceType, "global::System.TimeOnly"),
            Structured(sourceType, "global::System.Uri"),
            Structured(sourceType, "global::System.Version"),
            Structured(sourceType, "global::System.Numerics.BigInteger"),
            Structured(sourceType, "global::System.Index"),
            Structured(sourceType, "global::System.Range"),
            Structured(sourceType, "global::TestCase.CustomStruct"));
    }

    [Test]
    public async Task Uses_only_supported_accessible_constructors()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class PublicConstructor
    {
        public PublicConstructor(int value) { }
    }

    public sealed class RefConstructor
    {
        public RefConstructor(ref int value) { }
    }

    public sealed class RefLikeConstructor
    {
        public RefLikeConstructor(Span<int> value) { }
    }

    public sealed class PrivateConstructor
    {
        private PrivateConstructor() { }
    }

    public abstract class AbstractConstructor
    {
        public AbstractConstructor() { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, PublicConstructor>();
            builder.Map<Source, RefConstructor>();
            builder.Map<Source, RefLikeConstructor>();
            builder.Map<Source, PrivateConstructor>();
            builder.Map<Source, AbstractConstructor>();
        }
    }
}
""";

        const string sourceType = "global::TestCase.Source";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Pair(sourceType, "PublicConstructor", true, false),
            Pair(sourceType, "RefConstructor", false, false),
            Pair(sourceType, "RefLikeConstructor", false, false),
            Pair(sourceType, "PrivateConstructor", false, false),
            Pair(sourceType, "AbstractConstructor", false, false));
    }

    [Test]
    public async Task Uses_only_supported_body_members_after_hiding()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Threading.Tasks;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class SetProperty
    {
        public int Value { get; set; }
    }

    public sealed class InitProperty
    {
        public int Value { get; init; }
    }

    public sealed class MutableField
    {
        public int Value;
    }

    public sealed class WholeDeferredValue
    {
        public Task<int>? Value { get; set; }
    }

    public sealed class UnsupportedMembers
    {
        public static int Static { get; set; }
        public int GetOnly => 0;
        public int PrivateSetter { get; private set; }
        public readonly int ReadOnly;
        public const int Constant = 0;
        public int this[int index] { get => 0; set { } }
    }

    public class WritableBase
    {
        public int Value { get; set; }
    }

    public sealed class HiddenByGetOnly : WritableBase
    {
        public new int Value => 0;
    }

    public sealed class RefLikeMember
    {
        public Span<int> Value { get => default; set { } }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, SetProperty>();
            builder.Map<Source, InitProperty>();
            builder.Map<Source, MutableField>();
            builder.Map<Source, WholeDeferredValue>();
            builder.Map<Source, UnsupportedMembers>();
            builder.Map<Source, HiddenByGetOnly>();
            builder.Map<Source, RefLikeMember>();
        }
    }
}
""";

        const string sourceType = "global::TestCase.Source";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Pair(sourceType, "SetProperty", true, true),
            Pair(sourceType, "InitProperty", true, true),
            Pair(sourceType, "MutableField", true, true),
            Pair(sourceType, "WholeDeferredValue", true, true),
            Pair(sourceType, "UnsupportedMembers", true, false),
            Pair(sourceType, "HiddenByGetOnly", true, false),
            Pair(sourceType, "RefLikeMember", true, false));
    }

    [Test]
    public async Task Evaluates_accessibility_from_the_generated_mapper_lexical_context()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        private Destination() { }
        private int Value { get; set; }

        [MorphantMapper]
        public partial class TestMapper : TypeMapper
        {
            protected override void Configure(MapperBuilder builder) =>
                builder.Map<Source, Destination>();
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Destination+TestMapper",
            hasUnifiablePairs: false,
            new MappingPairExpectation(
                "global::TestCase.Source",
                "global::TestCase.Destination",
                Structured: true,
                Members: true));
    }

    [Test]
    public async Task Excludes_private_destination_surface_outside_its_lexical_context()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        private Destination() { }
        public int Value { get; private set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            new MappingPairExpectation(
                "global::TestCase.Source",
                "global::TestCase.Destination",
                Structured: false,
                Members: false));
    }

    private static MappingPairExpectation Pair(
        string source,
        string destinationName,
        bool structured,
        bool members)
    {
        return new MappingPairExpectation(
            source,
            "global::TestCase." + destinationName,
            structured,
            members);
    }

    private static MappingPairExpectation Direct(
        string source,
        string destination)
    {
        return new MappingPairExpectation(
            source,
            destination,
            Structured: false,
            Members: false);
    }

    private static MappingPairExpectation Structured(
        string source,
        string destination)
    {
        return new MappingPairExpectation(
            source,
            destination,
            Structured: true,
            Members: false);
    }
}
