// Compiled integration scenario: TypeMapperCallbackTests/ContextMarkerTests::Rejects_runtime_use_of_declarative_context
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0033

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ContextMarker_deadbeef
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class ConstructDestination
    {
        public ConstructDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class MembersDestination
    {
        public int Value { get; set; }
    }

    public sealed class RuntimeConstructDestination
    {
    }

    public sealed class RuntimeResolveDestination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct((source, context) => new(
                    value: source.Value + context.GetHashCode()));

            builder.Map<Source, MembersDestination>()
                .Members((source, _, _, context) => new()
                {
                    Value = source.Value + context.GetHashCode()
                });

            builder.Map<Source, RuntimeConstructDestination>()
                .ConstructUsing(_ =>
                    Wrap<RuntimeConstructDestination>(Auto()));

            builder.Map<Source, RuntimeResolveDestination>()
                .ResolveUsing((source, _) =>
                    Wrap<RuntimeResolveDestination>(Map(source)));
        }

        private static TDestination Wrap<TDestination>(object marker)
            where TDestination : new() =>
            new();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 1 };

            ExpectUnsupported(() =>
                ((ITypeMapper<Source, ConstructDestination>)mapper)
                    .Create(source, default(MappingContext)));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, MembersDestination>)mapper)
                    .Create(source, default(MappingContext)));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, RuntimeConstructDestination>)mapper)
                    .Create(source, default(MappingContext)));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, RuntimeResolveDestination>)mapper)
                    .Create(source, default(MappingContext)));
        }

        private static void ExpectUnsupported(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "MappingContextMarker escaped into runtime code.");
        }
    }
}
