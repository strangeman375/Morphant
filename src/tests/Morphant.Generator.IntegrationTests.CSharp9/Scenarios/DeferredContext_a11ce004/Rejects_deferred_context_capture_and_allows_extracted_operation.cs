// Compiled integration scenario: TypeMapperCallbackTests/ContextMarkerTests::Rejects_deferred_context_capture_and_allows_extracted_operation
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DeferredContext_a11ce004
{
    public sealed class Source
    {
    }

    public sealed class InvalidConstructDestination
    {
        public InvalidConstructDestination(
            Func<MappingOperation> callback) =>
            Callback = callback;

        public Func<MappingOperation> Callback { get; }
    }

    public sealed class InvalidResolveDestination
    {
        public InvalidResolveDestination(
            Func<MappingOperation> callback) =>
            Callback = callback;

        public Func<MappingOperation> Callback { get; }
    }

    public sealed class InvalidMembersDestination
    {
        public Func<MappingOperation> Callback { get; set; } =
            () => MappingOperation.Create;
    }

    public sealed class ValidConstructDestination
    {
        public ValidConstructDestination(
            Func<MappingOperation> callback) =>
            Callback = callback;

        public Func<MappingOperation> Callback { get; }
    }

    public sealed class ValidResolveDestination
    {
        public ValidResolveDestination(
            Func<MappingOperation> callback) =>
            Callback = callback;

        public Func<MappingOperation> Callback { get; }
    }

    public sealed class ValidMembersDestination
    {
        public Func<MappingOperation> Callback { get; set; } =
            () => MappingOperation.Create;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, InvalidConstructDestination>()
                .Construct((_, context) => new(
                    Value<Func<MappingOperation>>(
                        () => context.Operation)));

            builder.Map<Source, InvalidResolveDestination>()
                .Resolve((_, _, context) => new(
                    Value<Func<MappingOperation>>(
                        () => context.Operation)));

            builder.Map<Source, InvalidMembersDestination>()
                .Members((_, _, _, context) => new()
                {
                    Callback = Value<Func<MappingOperation>>(
                        () => context.Operation)
                });

            builder.Map<Source, ValidConstructDestination>()
                .Construct((_, context) =>
                {
                    var operation = context.Operation;
                    return new(Value<Func<MappingOperation>>(
                        () => operation));
                });

            builder.Map<Source, ValidResolveDestination>()
                .Resolve((_, _, context) =>
                {
                    var operation = context.Operation;
                    return new(Value<Func<MappingOperation>>(
                        () => operation));
                });

            builder.Map<Source, ValidMembersDestination>()
                .Members((_, _, _, context) =>
                {
                    var operation = context.Operation;
                    return new()
                    {
                        Callback = Value<Func<MappingOperation>>(
                            () => operation)
                    };
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source();

            ExpectUnsupported<InvalidConstructDestination>(mapper, source);
            ExpectUnsupported<InvalidResolveDestination>(mapper, source);
            ExpectUnsupported<InvalidMembersDestination>(mapper, source);

            AssertCreateOperation<ValidConstructDestination>(
                mapper,
                source,
                destination => destination.Callback());
            AssertCreateOperation<ValidResolveDestination>(
                mapper,
                source,
                destination => destination.Callback());
            AssertCreateOperation<ValidMembersDestination>(
                mapper,
                source,
                destination => destination.Callback());
        }

        private static void ExpectUnsupported<TDestination>(
            TestMapper mapper,
            Source source)
        {
            try
            {
                ((ITypeMapper<Source, TDestination>)mapper).Create(
                    source,
                    default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "MappingContextMarker was captured by a runtime callback.");
        }

        private static void AssertCreateOperation<TDestination>(
            TestMapper mapper,
            Source source,
            Func<TDestination, MappingOperation> read)
        {
            var destination =
                ((ITypeMapper<Source, TDestination>)mapper).Create(source);

            if (read(destination) != MappingOperation.Create)
            {
                throw new InvalidOperationException(
                    "An extracted context operation was not transferred.");
            }
        }
    }
}
