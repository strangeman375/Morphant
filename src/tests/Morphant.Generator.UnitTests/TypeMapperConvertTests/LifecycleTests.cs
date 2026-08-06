using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperConvertTests;

[TestFixture]
internal sealed class LifecycleTests
{
    [Test]
    public void Preserves_original_call_state_and_authoritative_results()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace TestCase
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

    public sealed class ManualServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service)
                ? service
                : null;

        public void Add<TService>(TService service)
            where TService : class =>
            _services[typeof(IEnumerable<TService>)] =
                new TService[] { service };
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<Source, Destination?>>(generated);
            var mapper = new Mapper(provider);

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
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Applies_only_MappingMode_as_the_manual_operation_gate()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;

namespace TestCase
{
    public sealed record Source(int Value);

    public sealed record CreateDestination(int Value);

    public sealed record UpdateDestination(int Value);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int CreateCalls { get; private set; }

        public static int UpdateCalls { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, CreateDestination>(MappingMode.Create)
                .Convert((source, _, _) =>
                {
                    CreateCalls++;
                    return new(source?.Value ?? -1);
                });

            builder.Map<Source, UpdateDestination>(MappingMode.Update)
                .Convert((source, previous, _) =>
                {
                    UpdateCalls++;
                    return previous.HasValue
                        ? previous.Value
                        : new UpdateDestination(source?.Value ?? -1);
                });
        }
    }

    public sealed class ManualServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service)
                ? service
                : null;

        public void Add<TService>(TService service)
            where TService : class =>
            _services[typeof(IEnumerable<TService>)] =
                new TService[] { service };
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<Source, CreateDestination>>(generated);
            provider.Add<ITypeMapper<Source, UpdateDestination>>(generated);
            var mapper = new Mapper(provider);
            var source = new Source(7);

            var created = mapper.Map<Source, CreateDestination>(source);
            ExpectNotSupported(() =>
                mapper.Map(source, new CreateDestination(1)));
            ExpectNotSupported(() =>
                mapper.Map<Source, UpdateDestination>(source));
            var createdByUpdate = mapper.Map<Source, UpdateDestination>(
                source,
                null);
            var previous = new UpdateDestination(9);
            var reused = mapper.Map(source, previous);

            if (created.Value != 7 ||
                createdByUpdate.Value != 7 ||
                !ReferenceEquals(previous, reused) ||
                TestMapper.CreateCalls != 1 ||
                TestMapper.UpdateCalls != 2)
            {
                throw new InvalidOperationException(
                    "MappingMode did not exclusively gate Convert.");
            }
        }

        private static void ExpectNotSupported(Action action)
        {
            try
            {
                action();
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A disabled manual operation was executed.");
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
