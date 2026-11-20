// Compiled integration scenario: TypeMapperConventionTests/TypeCompatibilityTests::Uses_only_warning_free_implicit_conversions
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TypeCompatibility_43514246
{
    public class BaseValue
    {
    }

    public sealed class DerivedValue : BaseValue
    {
    }

    public readonly struct SourceCode
    {
        public SourceCode(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public static implicit operator DestinationCode(SourceCode value) =>
            new(value.Value);
    }

    public readonly struct DestinationCode
    {
        public DestinationCode(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class Source
    {
        public int Numeric { get; init; }

        public DerivedValue Reference { get; init; } = new();

        public int? Lifted { get; init; }

        public SourceCode UserDefined { get; init; }

        public string? NullableRisk { get; init; }

        public int Narrowing { get; init; }

        public dynamic Runtime { get; init; } = 0;
    }

    public sealed class Destination
    {
        public long Numeric { get; set; }

        public BaseValue Reference { get; set; } = new();

        public long? Lifted { get; set; }

        public DestinationCode UserDefined { get; set; }

        public string NullableRisk { get; set; } = "preserved";

        public byte Narrowing { get; set; } = 17;

        public int Runtime { get; set; } = 19;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var reference = new DerivedValue();
            var source = new Source
            {
                Numeric = 31,
                Reference = reference,
                Lifted = 37,
                UserDefined = new SourceCode(41),
                NullableRisk = null,
                Narrowing = 257,
                Runtime = 43
            };
            var created = mapper.Create(source, default(MappingContext));
            var previous = new Destination();
            var updated = mapper.Update(
                source,
                previous,
                default(MappingContext));

            if (created.Numeric != 31L ||
                !ReferenceEquals(created.Reference, reference) ||
                created.Lifted != 37L ||
                created.UserDefined.Value != 41 ||
                created.NullableRisk != "preserved" ||
                created.Narrowing != 17 ||
                created.Runtime != 19 ||
                !ReferenceEquals(updated, previous) ||
                updated.Numeric != 31L ||
                !ReferenceEquals(updated.Reference, reference) ||
                updated.Lifted != 37L ||
                updated.UserDefined.Value != 41 ||
                updated.NullableRisk != "preserved" ||
                updated.Narrowing != 17 ||
                updated.Runtime != 19)
            {
                throw new InvalidOperationException(
                    "The implicit conversion boundary was not preserved.");
            }
        }
    }
}
