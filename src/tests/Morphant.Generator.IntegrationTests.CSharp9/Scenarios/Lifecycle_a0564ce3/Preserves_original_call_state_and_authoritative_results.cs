// Compiled integration scenario: TypeMapperConvertTests/LifecycleTests::Preserves_original_call_state_and_authoritative_results
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_a0564ce3
{
    public enum Command
    {
        Create,
        Reuse,
        Replace,
        ReturnNull
    }

    public sealed record Source(int Value, Command Command);

    public sealed class Destination
    {
        public Destination(int value) => Value = value;

        public int Value { get; set; }
    }

    public sealed record Call(
        MappingOperation Operation,
        bool SourceIsNull,
        bool PreviousHasValue);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static List<Call> Calls { get; } = new();

        protected override void Configure(MapperBuilder builder)
        {
            builder
                .NullSourceHandling(NullSourceHandling.Throw)
                .NullDestinationHandling(NullDestinationHandling.Throw)
                .ConstructorSelection(ConstructorSelection.Greediest)
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict);

            builder.Map<Source, Destination?>()
                .Convert((source, previous, context) =>
                {
                    Calls.Add(new(
                        context.Operation,
                        source is null,
                        previous.HasValue));

                    if (source is null ||
                        source.Command == Command.ReturnNull)
                    {
                        return null;
                    }

                    if (source.Command == Command.Reuse &&
                        previous.TryGetValue(out var destination))
                    {
                        destination.Value = source.Value;
                        return destination;
                    }

                    return new Destination(source.Value);
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<Source, Destination?>>(generated)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            var nullCreate = mapper.Map<Source, Destination?>(null);
            var nullUpdate = mapper.Map<Source, Destination?>(null, null);
            var explicitNull = mapper.Map<Source, Destination?>(
                new Source(1, Command.ReturnNull));
            var created = mapper.Map<Source, Destination?>(
                new Source(2, Command.Create));
            var previous = new Destination(3);
            var reused = mapper.Map<Source, Destination?>(
                new Source(4, Command.Reuse),
                previous);
            var replaced = mapper.Map<Source, Destination?>(
                new Source(5, Command.Replace),
                previous);

            if (nullCreate is not null ||
                nullUpdate is not null ||
                explicitNull is not null ||
                created?.Value != 2 ||
                !ReferenceEquals(previous, reused) ||
                reused.Value != 4 ||
                ReferenceEquals(previous, replaced) ||
                replaced?.Value != 5)
            {
                throw new InvalidOperationException(
                    "Convert did not return its authoritative result.");
            }

            var expected = new[]
            {
                new Call(MappingOperation.Create, true, false),
                new Call(MappingOperation.Update, true, false),
                new Call(MappingOperation.Create, false, false),
                new Call(MappingOperation.Create, false, false),
                new Call(MappingOperation.Update, false, true),
                new Call(MappingOperation.Update, false, true)
            };

            if (TestMapper.Calls.Count != expected.Length)
            {
                throw new InvalidOperationException(
                    "Convert observed the wrong number of calls.");
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (TestMapper.Calls[index] != expected[index])
                {
                    throw new InvalidOperationException(
                        "Convert observed the wrong call state.");
                }
            }
        }
    }
}
