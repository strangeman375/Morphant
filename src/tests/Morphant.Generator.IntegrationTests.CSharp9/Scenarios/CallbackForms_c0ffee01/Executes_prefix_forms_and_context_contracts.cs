// Compiled integration scenario: TypeMapperCallbackTests/CallbackFormsTests::Executes_prefix_forms_and_context_contracts
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackForms_c0ffee01
{
    public sealed class Source
    {
        public int Value { get; init; }

        public bool Reuse { get; init; }

        public bool ReturnNull { get; init; }
    }

    public sealed class ConstructDestination
    {
        public ConstructDestination(int seed) => Seed = seed;

        public int Seed { get; }

        public int Stamp { get; set; }
    }

    public sealed class ResolveDestination
    {
        public ResolveDestination(int seed) => Seed = seed;

        public int Seed { get; }

        public int Stamp { get; set; }
    }

    public sealed class RuntimeConstructDestination
    {
        public RuntimeConstructDestination(int seed) => Seed = seed;

        public int Seed { get; }

        public int Stamp { get; set; }
    }

    public sealed class RuntimeResolveDestination
    {
        public RuntimeResolveDestination(int seed) => Seed = seed;

        public int Seed { get; }

        public int Stamp { get; set; }
    }

    public sealed class SourceConvertDestination
    {
        public SourceConvertDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class PreviousConvertDestination
    {
        public PreviousConvertDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class ContextConvertDestination
    {
        public ContextConvertDestination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int RuntimeConstructCalls { get; private set; }

        public static int RuntimeResolveCalls { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct((source, context) => new(
                    seed: context.Operation == MappingOperation.Create
                        ? source.Value
                        : source.Value + 100))
                .Members((source, previous, result, context) => new()
                {
                    Stamp =
                        (context.Operation == MappingOperation.Create
                            ? 1000
                            : 2000) +
                        (previous.HasValue ? 100 : 0) +
                        result.Seed +
                        source.Value
                });

            builder.Map<Source, ResolveDestination>()
                .Resolve((source, previous, context) =>
                {
                    if (context.Operation == MappingOperation.Create)
                    {
                        return new(seed: source.Value);
                    }

                    if (previous.HasValue)
                    {
                        return previous;
                    }

                    return new(seed: source.Value + 100);
                })
                .Members((source, previous) => new()
                {
                    Stamp = source.Value +
                        (previous.HasValue ? 100 : 0)
                });

            builder.Map<Source, RuntimeConstructDestination>()
                .ConstructUsing((source, context) =>
                {
                    if (context.Mapper is null)
                    {
                        throw new InvalidOperationException(
                            "ConstructUsing received an invalid context.");
                    }

                    RuntimeConstructCalls++;
                    var nested = context.Mapper.Map<
                        Source,
                        SourceConvertDestination>(source);
                    return new RuntimeConstructDestination(
                        nested.Value +
                        (context.Operation == MappingOperation.Create
                            ? 0
                            : 100));
                })
                .Members(source => new()
                {
                    Stamp = source.Value
                });

            builder.Map<Source, RuntimeResolveDestination>()
                .ResolveUsing((source, previous, context) =>
                {
                    if (context.Mapper is null)
                    {
                        throw new InvalidOperationException(
                            "ResolveUsing received an invalid context.");
                    }

                    RuntimeResolveCalls++;

                    if (source.ReturnNull)
                    {
                        return null!;
                    }

                    if (previous.HasValue && source.Reuse)
                    {
                        return previous.Value;
                    }

                    return new RuntimeResolveDestination(
                        source.Value +
                        (context.Operation == MappingOperation.Create
                            ? 0
                            : 100));
                })
                .Members((source, previous, result) => new()
                {
                    Stamp = result.Seed + source.Value +
                        (previous.HasValue ? 1000 : 0)
                });

            builder.Map<Source, SourceConvertDestination>()
                .Convert(source => new SourceConvertDestination(
                    source?.Value ?? -1));

            builder.Map<Source, PreviousConvertDestination>()
                .Convert((source, previous) =>
                    new PreviousConvertDestination(
                        (source?.Value ?? 0) +
                        (previous.HasValue
                            ? previous.Value.Value
                            : 100)));

            builder.Map<Source, ContextConvertDestination>()
                .Convert((source, previous, context) =>
                {
                    if (context.Mapper is null)
                    {
                        throw new InvalidOperationException(
                            "Convert received an invalid context.");
                    }

                    return new ContextConvertDestination(
                        (source?.Value ?? 0) +
                        (previous.HasValue ? previous.Value.Value : 0) +
                        (context.Operation == MappingOperation.Create
                            ? 1000
                            : 2000));
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var implementation = new TestMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<Source, ConstructDestination>>(
                    implementation)
                .AddSingleton<ITypeMapper<Source, ResolveDestination>>(
                    implementation)
                .AddSingleton<ITypeMapper<
                    Source,
                    RuntimeConstructDestination>>(implementation)
                .AddSingleton<ITypeMapper<
                    Source,
                    RuntimeResolveDestination>>(implementation)
                .AddSingleton<ITypeMapper<
                    Source,
                    SourceConvertDestination>>(implementation)
                .AddSingleton<ITypeMapper<
                    Source,
                    PreviousConvertDestination>>(implementation)
                .AddSingleton<ITypeMapper<
                    Source,
                    ContextConvertDestination>>(implementation)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            VerifyStructuredConstruct(mapper);
            VerifyStructuredResolve(mapper);
            VerifyRuntimeConstruct(mapper);
            VerifyRuntimeResolve(mapper);
            VerifyConvertForms(mapper);
        }

        private static void VerifyStructuredConstruct(IMapper mapper)
        {
            var created = mapper.Map<Source, ConstructDestination>(
                new Source { Value = 1 });
            var createdByUpdate = mapper.Map<
                Source,
                ConstructDestination>(
                new Source { Value = 2 },
                null);
            var previous = new ConstructDestination(7);
            var updated = mapper.Map(
                new Source { Value = 3 },
                previous);

            if (created.Seed != 1 || created.Stamp != 1002 ||
                createdByUpdate.Seed != 102 ||
                createdByUpdate.Stamp != 2104 ||
                !ReferenceEquals(previous, updated) ||
                updated.Seed != 7 || updated.Stamp != 2110)
            {
                throw new InvalidOperationException(
                    "Structured Construct or Members context changed.");
            }
        }

        private static void VerifyStructuredResolve(IMapper mapper)
        {
            var created = mapper.Map<Source, ResolveDestination>(
                new Source { Value = 4 });
            var createdByUpdate = mapper.Map<Source, ResolveDestination>(
                new Source { Value = 5 },
                null);
            var previous = new ResolveDestination(8);
            var updated = mapper.Map(
                new Source { Value = 6 },
                previous);

            if (created.Seed != 4 || created.Stamp != 4 ||
                createdByUpdate.Seed != 105 ||
                createdByUpdate.Stamp != 5 ||
                !ReferenceEquals(previous, updated) ||
                updated.Stamp != 106)
            {
                throw new InvalidOperationException(
                    "Structured Resolve context changed.");
            }
        }

        private static void VerifyRuntimeConstruct(IMapper mapper)
        {
            var created = mapper.Map<Source, RuntimeConstructDestination>(
                new Source { Value = 7 });
            var createdByUpdate = mapper.Map<
                Source,
                RuntimeConstructDestination>(
                new Source { Value = 8 },
                null);
            var previous = new RuntimeConstructDestination(9);
            var updated = mapper.Map(
                new Source { Value = 10 },
                previous);

            if (created.Seed != 7 || created.Stamp != 7 ||
                createdByUpdate.Seed != 108 ||
                createdByUpdate.Stamp != 8 ||
                !ReferenceEquals(previous, updated) ||
                updated.Stamp != 10 ||
                TestMapper.RuntimeConstructCalls != 2)
            {
                throw new InvalidOperationException(
                    "ConstructUsing context or lifecycle changed.");
            }
        }

        private static void VerifyRuntimeResolve(IMapper mapper)
        {
            var created = mapper.Map<Source, RuntimeResolveDestination>(
                new Source { Value = 11 });
            var createdByUpdate = mapper.Map<
                Source,
                RuntimeResolveDestination>(
                new Source { Value = 12 },
                null);
            var previous = new RuntimeResolveDestination(13);
            var reused = mapper.Map(
                new Source { Value = 14, Reuse = true },
                previous);
            var nullResult = mapper.Map<
                Source,
                RuntimeResolveDestination>(
                new Source { ReturnNull = true });

            if (created.Seed != 11 || created.Stamp != 22 ||
                createdByUpdate.Seed != 112 ||
                createdByUpdate.Stamp != 124 ||
                !ReferenceEquals(previous, reused) ||
                reused.Stamp != 1027 ||
                nullResult is not null ||
                TestMapper.RuntimeResolveCalls != 4)
            {
                throw new InvalidOperationException(
                    "ResolveUsing context, lifecycle, or null result changed.");
            }
        }

        private static void VerifyConvertForms(IMapper mapper)
        {
            var sourceOnly = mapper.Map<Source, SourceConvertDestination>(
                null);
            var previousAware = mapper.Map(
                new Source { Value = 15 },
                new PreviousConvertDestination(16));
            var contextCreate = mapper.Map<
                Source,
                ContextConvertDestination>(new Source { Value = 17 });
            var contextUpdate = mapper.Map(
                new Source { Value = 18 },
                new ContextConvertDestination(19));

            if (sourceOnly.Value != -1 ||
                previousAware.Value != 31 ||
                contextCreate.Value != 1017 ||
                contextUpdate.Value != 2037)
            {
                throw new InvalidOperationException(
                    "A Convert prefix form changed its input contract.");
            }
        }
    }
}
