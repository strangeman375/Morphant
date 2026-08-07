// Compiled integration scenario: TypeMapperInheritanceTests/GenericAndAccessibilityTests::Instantiates_generic_IncludeBase_types_for_a_nested_mapper
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_a15526de
{
    public class Source<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class DerivedSource<T> : Source<T>
    {
        public string Extra { get; init; } = string.Empty;
    }

    public class Destination<T>
    {
        public T Value { get; set; } = default!;

        public string Label { get; set; } = string.Empty;
    }

    public sealed class DerivedDestination<T> : Destination<T>
    {
        public string Extra { get; set; } = string.Empty;
    }

    public abstract class GenericBaseMapper<T> : TypeMapper
    {
        protected static string FormatValue(object? value) =>
            "base:" + value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Members((source, _) => new()
                {
                    Value = source.Value,
                    Label = FormatValue(source.Value)
                });
    }

    public partial class Container<T>
    {
        [MorphantMapper]
        public partial class Mapper : GenericBaseMapper<T>
        {
            protected override void Configure(MapperBuilder builder)
            {
                base.Configure(builder);
                builder.Map<DerivedSource<T>, DerivedDestination<T>>()
                    .IncludeBase<Source<T>, Destination<T>>()
                    .Members((source, _) => new()
                    {
                        Extra = source.Extra
                    });
            }
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<
                    DerivedSource<int>,
                    DerivedDestination<int>>)
                new Container<int>.Mapper();
            var result = mapper.Create(
                new DerivedSource<int>
                {
                    Value = 17,
                    Extra = "extra"
                },
                default);

            if (result.Value != 17 ||
                result.Label != "base:17" ||
                result.Extra != "extra")
            {
                throw new InvalidOperationException(
                    "The constructed generic IncludeBase pair was lost.");
            }
        }
    }
}
