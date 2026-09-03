// Compiled integration scenario: shared tuple presentation across mappers
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleSharedPresentation_a7b10005
{
    [MorphantMapper]
    public partial class FirstMapper : TypeMapper<FirstMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(long X, decimal Y), (decimal Left, long Top)>()
                .Members(source => new()
                {
                    Left = source.Y + 1m,
                    Top = source.X + 2
                });
    }

    [MorphantMapper]
    public partial class SecondMapper : TypeMapper<SecondMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(long X, decimal Y), (decimal Left, long Top)>()
                .Members(source => new()
                {
                    Left = source.Y + 10m,
                    Top = source.X + 20
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var firstMapper = new FirstMapper();
            var secondMapper = new SecondMapper();
            var first =
                (ITypeMapper<
                    (long X, decimal Y),
                    (decimal Left, long Top)>)firstMapper;
            var second =
                (ITypeMapper<
                    (long X, decimal Y),
                    (decimal Left, long Top)>)secondMapper;

            var firstCreated = first.Create(
                (X: 2L, Y: 3m),
                default(MappingContext));
            var firstUpdated = first.Update(
                (X: 5L, Y: 7m),
                (Left: -1m, Top: -2L),
                default(MappingContext));
            var secondCreated = second.Create(
                (X: 2L, Y: 3m),
                default(MappingContext));
            var secondUpdated = second.Update(
                (X: 5L, Y: 7m),
                (Left: -1m, Top: -2L),
                default(MappingContext));

            if (firstCreated != (Left: 4m, Top: 4L) ||
                firstUpdated != (Left: 8m, Top: 7L) ||
                secondCreated != (Left: 13m, Top: 22L) ||
                secondUpdated != (Left: 17m, Top: 25L))
            {
                throw new InvalidOperationException(
                    "Mappers sharing a tuple presentation affected each " +
                    "other's mapping behavior.");
            }
        }
    }
}
