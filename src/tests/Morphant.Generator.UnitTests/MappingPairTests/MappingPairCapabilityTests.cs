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

    public sealed class StructuredWithParameterless
    {
        public StructuredWithParameterless() { }
        public StructuredWithParameterless(int value) { }
    }

    public sealed class ParameterlessOnly { }

    public sealed class ParameterlessWithMembers
    {
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

    public struct CustomStructWithMembers
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, StructuredOnly>();
            builder.Map<Source, StructuredWithMembers>();
            builder.Map<Source, StructuredWithParameterless>();
            builder.Map<Source, ParameterlessOnly>();
            builder.Map<Source, ParameterlessWithMembers>();
            builder.Map<Source, DirectOnly>();
            builder.Map<Source, IDirectWithMembers>();
            builder.Map<Source, FactoryOnly>();
            builder.Map<Source, CustomStruct>();
            builder.Map<Source, CustomStruct?>();
            builder.Map<Source, CustomStructWithMembers>();
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
            Pair(
                sourceType,
                "StructuredWithParameterless",
                true,
                false),
            Pair(sourceType, "ParameterlessOnly", true, false),
            Pair(
                sourceType,
                "ParameterlessWithMembers",
                true,
                true),
            Pair(sourceType, "DirectOnly", false, false),
            Pair(sourceType, "IDirectWithMembers", false, true),
            Pair(sourceType, "FactoryOnly", false, false),
            Pair(sourceType, "CustomStruct", true, false),
            new MappingPairExpectation(
                sourceType,
                "global::System.Nullable<global::TestCase.CustomStruct>",
                Structured: true,
                Members: false),
            Pair(
                sourceType,
                "CustomStructWithMembers",
                true,
                true));
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
using System.Text;
using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public enum Status { None }
    public struct CustomStruct
    {
        public CustomStruct(int value) { Value = value; }
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, object>();
            builder.Map<Source, string>();
            builder.Map<Source, bool>();
            builder.Map<Source, bool?>();
            builder.Map<Source, char>();
            builder.Map<Source, char?>();
            builder.Map<Source, sbyte>();
            builder.Map<Source, sbyte?>();
            builder.Map<Source, byte>();
            builder.Map<Source, byte?>();
            builder.Map<Source, short>();
            builder.Map<Source, short?>();
            builder.Map<Source, ushort>();
            builder.Map<Source, ushort?>();
            builder.Map<Source, int>();
            builder.Map<Source, int?>();
            builder.Map<Source, uint>();
            builder.Map<Source, uint?>();
            builder.Map<Source, long>();
            builder.Map<Source, long?>();
            builder.Map<Source, ulong>();
            builder.Map<Source, ulong?>();
            builder.Map<Source, nint>();
            builder.Map<Source, nuint>();
            builder.Map<Source, nint?>();
            builder.Map<Source, nuint?>();
            builder.Map<Source, float>();
            builder.Map<Source, float?>();
            builder.Map<Source, double>();
            builder.Map<Source, double?>();
            builder.Map<Source, decimal>();
            builder.Map<Source, decimal?>();
            builder.Map<Source, Status>();
            builder.Map<Source, Status?>();
            builder.Map<Source, Guid>();
            builder.Map<Source, Guid?>();
            builder.Map<Source, DateTime>();
            builder.Map<Source, DateTime?>();
            builder.Map<Source, DateTimeOffset>();
            builder.Map<Source, DateTimeOffset?>();
            builder.Map<Source, DateOnly>();
            builder.Map<Source, DateOnly?>();
            builder.Map<Source, TimeOnly>();
            builder.Map<Source, TimeOnly?>();
            builder.Map<Source, TimeSpan>();
            builder.Map<Source, TimeSpan?>();
            builder.Map<Source, Half>();
            builder.Map<Source, Half?>();
            builder.Map<Source, Int128>();
            builder.Map<Source, Int128?>();
            builder.Map<Source, UInt128>();
            builder.Map<Source, UInt128?>();
            builder.Map<Source, Uri>();
            builder.Map<Source, Version>();
            builder.Map<Source, BigInteger>();
            builder.Map<Source, BigInteger?>();
            builder.Map<Source, Complex>();
            builder.Map<Source, Complex?>();
            builder.Map<Source, Rune>();
            builder.Map<Source, Rune?>();
            builder.Map<Source, Index>();
            builder.Map<Source, Index?>();
            builder.Map<Source, Range>();
            builder.Map<Source, Range?>();
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
            Opaque(sourceType, "global::System.Object"),
            Opaque(sourceType, "global::System.String"),
            Opaque(sourceType, "global::System.Boolean"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Boolean>"),
            Opaque(sourceType, "global::System.Char"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Char>"),
            Opaque(sourceType, "global::System.SByte"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.SByte>"),
            Opaque(sourceType, "global::System.Byte"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Byte>"),
            Opaque(sourceType, "global::System.Int16"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Int16>"),
            Opaque(sourceType, "global::System.UInt16"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.UInt16>"),
            Opaque(sourceType, "global::System.Int32"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Int32>"),
            Opaque(sourceType, "global::System.UInt32"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.UInt32>"),
            Opaque(sourceType, "global::System.Int64"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Int64>"),
            Opaque(sourceType, "global::System.UInt64"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.UInt64>"),
            Opaque(sourceType, "global::System.IntPtr"),
            Opaque(sourceType, "global::System.UIntPtr"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.IntPtr>"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.UIntPtr>"),
            Opaque(sourceType, "global::System.Single"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Single>"),
            Opaque(sourceType, "global::System.Double"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Double>"),
            Opaque(sourceType, "global::System.Decimal"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Decimal>"),
            Opaque(sourceType, "global::TestCase.Status"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::TestCase.Status>"),
            Opaque(sourceType, "global::System.Guid"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Guid>"),
            Opaque(sourceType, "global::System.DateTime"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.DateTime>"),
            Opaque(sourceType, "global::System.DateTimeOffset"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.DateTimeOffset>"),
            Opaque(sourceType, "global::System.DateOnly"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.DateOnly>"),
            Opaque(sourceType, "global::System.TimeOnly"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.TimeOnly>"),
            Opaque(sourceType, "global::System.TimeSpan"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.TimeSpan>"),
            Opaque(sourceType, "global::System.Half"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Half>"),
            Opaque(sourceType, "global::System.Int128"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Int128>"),
            Opaque(sourceType, "global::System.UInt128"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.UInt128>"),
            Opaque(sourceType, "global::System.Uri"),
            Opaque(sourceType, "global::System.Version"),
            Opaque(sourceType, "global::System.Numerics.BigInteger"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Numerics.BigInteger>"),
            Opaque(sourceType, "global::System.Numerics.Complex"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Numerics.Complex>"),
            Opaque(sourceType, "global::System.Text.Rune"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Text.Rune>"),
            Opaque(sourceType, "global::System.Index"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Index>"),
            Opaque(sourceType, "global::System.Range"),
            Opaque(
                sourceType,
                "global::System.Nullable<global::System.Range>"),
            new MappingPairExpectation(
                sourceType,
                "global::TestCase.CustomStruct",
                Structured: true,
                Members: true));
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
    public async Task Direct_destinations_expose_only_post_construction_members()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public interface ISetterProperty
    {
        int Value { get; set; }
    }

    public interface IInitOnlyProperty
    {
        int Value { get; init; }
    }

    public abstract class MutableField
    {
        public int Value;
    }

    public sealed class RequiredSetterProperty
    {
        private RequiredSetterProperty() { }
        public required int Value { get; set; }
    }

    public sealed class RequiredMutableField
    {
        private RequiredMutableField() { }
        public required int Value;
    }

    public sealed class RequiredInitOnlyProperty
    {
        private RequiredInitOnlyProperty() { }
        public required int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ISetterProperty>();
            builder.Map<Source, IInitOnlyProperty>();
            builder.Map<Source, MutableField>();
            builder.Map<Source, RequiredSetterProperty>();
            builder.Map<Source, RequiredMutableField>();
            builder.Map<Source, RequiredInitOnlyProperty>();
        }
    }
}
""";

        const string sourceType = "global::TestCase.Source";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp11,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Pair(sourceType, "ISetterProperty", false, true),
            Pair(sourceType, "IInitOnlyProperty", false, false),
            Pair(sourceType, "MutableField", false, true),
            Pair(sourceType, "RequiredSetterProperty", false, true),
            Pair(sourceType, "RequiredMutableField", false, true),
            Pair(sourceType, "RequiredInitOnlyProperty", false, false));
    }

    [Test]
    public async Task Uses_the_common_generated_assembly_context_for_accessibility()
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
                Structured: false,
                Members: false));
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

    private static MappingPairExpectation Opaque(
        string source,
        string destination)
    {
        return new MappingPairExpectation(
            source,
            destination,
            Structured: false,
            Members: false);
    }
}
