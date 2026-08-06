using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class CompatibilityTests
{
    [Test]
    public void Uses_warning_free_implicit_conversions_for_explicit_and_automatic_values()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.Diagnostics.CodeAnalysis;

namespace TestCase
{
    public interface IContract
    {
        int Value { get; }
    }

    public sealed class Contract : IContract
    {
        public Contract(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public readonly struct InputValue
    {
        public InputValue(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public readonly struct OutputValue
    {
        public OutputValue(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public static implicit operator OutputValue(InputValue value) =>
            new(value.Value);
    }

    public sealed class Source
    {
        public short Numeric { get; init; }

        public int? NullableNumber { get; init; }

        public Contract Reference { get; init; } = new(0);

        public InputValue Converted { get; init; }

        public int Boxed { get; init; }

        public string? NullableText { get; init; }

        public string? AllowNullText { get; init; }

        public object Incompatible { get; init; } = new();
    }

    public sealed class Destination
    {
        public int Numeric { get; set; }

        public int? NullableNumber { get; set; }

        public IContract? Reference { get; set; }

        public OutputValue Converted { get; set; }

        public object? Boxed { get; set; }

        public long ExplicitLong { get; set; }

        public string NullableText { get; set; } = "preserved-nullable";

        [AllowNull]
        public string AllowNullText { get; set; } = "initial";

        public string Incompatible { get; set; } = "preserved-incompatible";
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) => new()
                {
                    Numeric = Auto(),
                    NullableNumber = Auto(),
                    Reference = Auto(),
                    Converted = Auto(),
                    Boxed = Auto(),
                    AllowNullText = Auto(),
                    ExplicitLong = source.Numeric
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = new Source
            {
                Numeric = 7,
                NullableNumber = 8,
                Reference = new Contract(9),
                Converted = new InputValue(10),
                Boxed = 11,
                NullableText = null,
                AllowNullText = null,
                Incompatible = new object()
            };
            var result =
                ((ITypeMapper<Source, Destination>)new TestMapper())
                .Create(source, default(MappingContext));

            if (result.Numeric != 7 ||
                result.NullableNumber != 8 ||
                result.Reference?.Value != 9 ||
                result.Converted.Value != 10 ||
                result.Boxed is not 11 ||
                result.ExplicitLong != 7L ||
                result.NullableText != "preserved-nullable" ||
                result.AllowNullText is not null ||
                result.Incompatible != "preserved-incompatible")
            {
                throw new InvalidOperationException(
                    "Implicit conversion compatibility was not preserved.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
