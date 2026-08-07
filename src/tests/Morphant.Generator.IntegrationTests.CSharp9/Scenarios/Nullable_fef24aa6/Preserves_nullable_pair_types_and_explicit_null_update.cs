// Compiled integration scenario: TypeMapperNestedMapTests/NullableTests::Preserves_nullable_pair_types_and_explicit_null_update
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Nullable_fef24aa6
{
    public sealed record OuterSource(string? Text, int? Number);

    public sealed class OuterDestination
    {
        public string? Text { get; set; }

        public int? CreatedNumber { get; set; }

        public int? UpdatedNumber { get; set; }
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Members((source, _) => new()
                {
                    Text = Create<string?>(source.Text),
                    CreatedNumber = Create(source.Number),
                    UpdatedNumber = Update<int?>(
                        source.Number,
                        (int?)null)
                });
    }

    public sealed class TextMapper : ITypeMapper<string?, string?>
    {
        public int Calls { get; private set; }

        public string? Create(
            string? source,
            MappingContext context)
        {
            if (context.Operation != MappingOperation.Create)
            {
                throw new InvalidOperationException(
                    "Nullable text used the wrong operation.");
            }

            Calls++;
            return source ?? "text-null";
        }

        public string? Update(
            string? source,
            string? destination,
            MappingContext context) =>
            throw new InvalidOperationException(
                "Nullable text unexpectedly used Update.");
    }

    public sealed class NumberMapper : ITypeMapper<int?, int?>
    {
        public List<MappingOperation> Operations { get; } = new();

        public int? Create(
            int? source,
            MappingContext context)
        {
            Operations.Add(context.Operation);
            return source ?? 7;
        }

        public int? Update(
            int? source,
            int? destination,
            MappingContext context)
        {
            if (destination.HasValue)
            {
                throw new InvalidOperationException(
                    "The explicit null destination was replaced.");
            }

            Operations.Add(context.Operation);
            return source ?? 9;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var text = new TextMapper();
            var number = new NumberMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(
                outer);
            provider.Add<ITypeMapper<string?, string?>>(text);
            provider.Add<ITypeMapper<int?, int?>>(number);
            var mapper = new Mapper(provider);

            var result = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(null, null));

            if (result.Text != "text-null" ||
                result.CreatedNumber != 7 ||
                result.UpdatedNumber != 9 ||
                text.Calls != 1 ||
                number.Operations.Count != 2 ||
                number.Operations[0] != MappingOperation.Create ||
                number.Operations[1] != MappingOperation.Update)
            {
                throw new InvalidOperationException(
                    "Nullable nested pair semantics are incorrect.");
            }
        }
    }
}
