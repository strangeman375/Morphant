// Compiled integration scenario: TypeMapperInheritanceTests/GenericAndAccessibilityTests::Provides_the_open_configuration_surface_for_a_closed_base
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_f2272e41
{
    public class Source<T>
    {
        public T Value { get; init; } = default!;
    }

    public class Destination<T>
    {
        public T Value { get; set; } = default!;
    }

    public abstract class GenericBaseMapper<T> : TypeMapper
    {
        protected static TValue Identity<TValue>(TValue value) => value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Members((source, _) => new()
                {
                    Value = Identity<T>((T)source.Value)
                });
    }

    public sealed class ClosedSource : Source<int>
    {
    }

    public sealed class ClosedDestination : Destination<int>
    {
    }

    [MorphantMapper]
    public partial class ClosedMapper : GenericBaseMapper<int>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<ClosedSource, ClosedDestination>()
                .IncludeBase<Source<int>, Destination<int>>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            if (typeof(ITypeMapper<Source<int>, Destination<int>>)
                .IsAssignableFrom(typeof(ClosedMapper)))
            {
                throw new InvalidOperationException(
                    "The open base surface became a derived registration.");
            }

            var mapper = (ITypeMapper<ClosedSource, ClosedDestination>)
                new ClosedMapper();
            var result = mapper.Create(
                new ClosedSource { Value = 17 },
                default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "The closed base configuration was not specialized.");
            }
        }
    }
}
