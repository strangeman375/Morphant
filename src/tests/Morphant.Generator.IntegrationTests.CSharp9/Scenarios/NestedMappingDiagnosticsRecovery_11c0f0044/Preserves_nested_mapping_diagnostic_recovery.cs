// Compiled integration scenario: NestedMappingDiagnosticsTests::Preserves_suppressed_nested_mapping_diagnostic_recovery
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0044
#pragma warning disable MORPH0045
#pragma warning disable MORPH0046

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;
using global::Morphant.Generated.Types.N_Morphant.N_Generator.N_IntegrationTests.N_CSharp9.N_Scenarios.N_NestedMappingDiagnosticsRecovery__11c0f0044.Plans;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NestedMappingDiagnosticsRecovery_11c0f0044
{
    public sealed class ChildSource
    {
        public ChildSource(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class ChildDestination
    {
        public ChildDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class Source
    {
        public ChildSource Child { get; set; } = new ChildSource(1);

        public int Value { get; set; }
    }

    public sealed class UnknownDestination
    {
        public ChildDestination? Child { get; set; }

        public int Value { get; set; }
    }

    public sealed class IncompatibleResultDestination
    {
        public string Text { get; set; } = string.Empty;

        public int Value { get; set; }
    }

    public sealed class ExplicitUpdateDestination
    {
        public ChildDestination Child { get; set; } =
            new ChildDestination(-1);

        public int Value { get; set; }
    }

    public sealed class AdaptiveDestination
    {
        public AdaptiveDestination(ChildDestination value) => Stored = value;

        public ChildDestination Stored { get; }
    }

    public sealed class RuntimeSlot
    {
        public RuntimeSlot(ChildDestination value) => Value = value;

        public ChildDestination Value { get; }

        public static implicit operator RuntimeSlot(
            ChildDestination value) => new RuntimeSlot(value);
    }

    public sealed class RuntimeAdaptiveDestination
    {
        public RuntimeSlot Child { get; set; } =
            new RuntimeSlot(new ChildDestination(-1));
    }

    public sealed class WideDestination
    {
        public object? Child { get; set; }
    }

    public sealed class AmbiguousDestination
    {
        public ChildDestination First { get; set; } =
            new ChildDestination(-1);

        public ChildDestination Second { get; set; } =
            new ChildDestination(-1);
    }

    public sealed class WrongProxyDestination
    {
        public ChildDestination Child { get; } =
            new ChildDestination(-1);

        public int Value { get; set; }
    }

    public sealed class SpoofedMembers
    {
        public global::Morphant.Members.Member<ChildDestination> Child =>
            null!;
    }

    public sealed class ReadOnlyDestination
    {
        public ReadOnlyDestination(ChildDestination? child) => Child = child;

        public ChildDestination? Child { get; }
    }

    public sealed class IndependentDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int ChildArgumentReads { get; private set; }

        public static int ValueReads { get; private set; }

        public static void Reset()
        {
            ChildArgumentReads = 0;
            ValueReads = 0;
        }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, UnknownDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => source.Value >= 0
                    ? new()
                    {
                        Value = ReadValue(source.Value),
                        Child = Map(null)
                    }
                    : new()
                    {
                        Value = ReadValue(source.Value),
                        Child = Ignore()
                    });

            builder.Map<Source, IncompatibleResultDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    Value = ReadValue(source.Value),
                    Text = Map<int>(ReadValue(source.Value))
                });

            builder.Map<Source, ExplicitUpdateDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    Value = ReadValue(source.Value),
                    Child = Update<ChildDestination>(
                        ReadChild(source.Child),
                        ReadObject())
                });

            builder.Map<Source, AdaptiveDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                    new(Map<ChildDestination>(ReadChild(source.Child))));

            builder.Map<Source, RuntimeAdaptiveDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .ResolveUsing((source, previous) => previous.HasValue
                    ? previous.Value
                    : new RuntimeAdaptiveDestination())
                .Members(source => new()
                {
                    Child = Map<ChildDestination>(ReadChild(source.Child))
                });

            builder.Map<Source, WideDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    Child = Map<ChildDestination>(ReadChild(source.Child))
                });

            builder.Map<Source, AmbiguousDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source =>
                {
                    var child = Map(ReadChild(source.Child));
                    return new()
                    {
                        First = child,
                        Second = child
                    };
                });

            builder.Map<Source, WrongProxyDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, previous) =>
                {
                    var members = new SpoofedMembers();
                    Update(ReadChild(source.Child), members.Child);
                    return new()
                    {
                        Value = ReadValue(source.Value)
                    };
                });

            builder.Map<Source, ReadOnlyDestination>(MappingMode.Update)
                .ResolveUsing((source, previous) =>
                    previous.HasValue
                        ? previous.Value
                        : new ReadOnlyDestination(null))
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, previous) =>
                {
                    var members = new ReadOnlyDestinationMembers();
                    Update(ReadChild(source.Child), members.Child);
                    return members;
                });

            builder.Map<Source, IndependentDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    Value = ReadValue(source.Value)
                });
        }

        private static ChildSource ReadChild(ChildSource source)
        {
            ChildArgumentReads++;
            return source;
        }

        private static int ReadValue(int value)
        {
            ValueReads++;
            return value;
        }

        private static object ReadObject()
        {
            ValueReads++;
            return new object();
        }
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public List<ChildCall> Calls { get; } = new List<ChildCall>();

        public ChildDestination Create(
            ChildSource? source,
            MappingContext context)
        {
            Calls.Add(new ChildCall(context.Operation, null));
            return new ChildDestination(source!.Value * 10);
        }

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            Calls.Add(new ChildCall(context.Operation, destination?.Value));
            return new ChildDestination(
                source!.Value * 10 + (destination?.Value ?? 1000));
        }
    }

    public sealed class ChildCall
    {
        public ChildCall(MappingOperation operation, int? destination)
        {
            Operation = operation;
            Destination = destination;
        }

        public MappingOperation Operation { get; }

        public int? Destination { get; }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new TestMapper();
            var child = new ChildMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<Source, AdaptiveDestination>>(outer)
                .AddSingleton<ITypeMapper<
                    Source,
                    RuntimeAdaptiveDestination>>(outer)
                .AddSingleton<ITypeMapper<Source, WideDestination>>(outer)
                .AddSingleton<ITypeMapper<Source, AmbiguousDestination>>(outer)
                .AddSingleton<ITypeMapper<Source, ReadOnlyDestination>>(outer)
                .AddSingleton<ITypeMapper<ChildSource, ChildDestination>>(child)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
            var source = new Source
            {
                Child = new ChildSource(3),
                Value = 17
            };

            VerifyUnknownPair(outer, source);
            VerifyIncompatibleResult(outer, source);
            VerifyExplicitUpdate(outer, source);
            VerifyAdaptivePath(mapper, child, source);
            VerifyRuntimeAdaptivePath(mapper, child, source);
            VerifyWideCurrentSlot(mapper, child, source);
            VerifyAmbiguousLocal(mapper, child, source);
            VerifyWrongProxy(outer, source);
            VerifyReadOnlyProxy(mapper, child, source);
            VerifyIndependentPair(outer, source);
        }

        private static void VerifyUnknownPair(TestMapper mapper, Source source)
        {
            TestMapper.Reset();
            ExpectConfiguration(() =>
                ((ITypeMapper<Source, UnknownDestination>)mapper).Create(
                    source,
                    default(MappingContext)));

            if (TestMapper.ValueReads != 0)
            {
                throw new InvalidOperationException(
                    "MORPH0044 recovery evaluated a sibling member value.");
            }

            TestMapper.Reset();
            var valid =
                ((ITypeMapper<Source, UnknownDestination>)mapper).Create(
                    new Source
                    {
                        Child = source.Child,
                        Value = -7
                    },
                    default(MappingContext));

            if (valid.Value != -7 ||
                valid.Child is not null ||
                TestMapper.ValueReads != 1)
            {
                throw new InvalidOperationException(
                    "MORPH0044 recovery changed an independent branch.");
            }
        }

        private static void VerifyIncompatibleResult(
            TestMapper mapper,
            Source source)
        {
            TestMapper.Reset();
            ExpectConfiguration(() =>
                ((ITypeMapper<Source, IncompatibleResultDestination>)mapper)
                .Create(source, default(MappingContext)));

            if (TestMapper.ValueReads != 0)
            {
                throw new InvalidOperationException(
                    "MORPH0045 recovery evaluated nested or sibling values.");
            }
        }

        private static void VerifyExplicitUpdate(
            TestMapper mapper,
            Source source)
        {
            TestMapper.Reset();
            var previous = new ExplicitUpdateDestination
            {
                Child = new ChildDestination(19),
                Value = 23
            };
            ExpectConfiguration(() =>
                ((ITypeMapper<Source, ExplicitUpdateDestination>)mapper)
                .Update(source, previous, default(MappingContext)));

            if (previous.Child.Value != 19 ||
                previous.Value != 23 ||
                TestMapper.ChildArgumentReads != 0 ||
                TestMapper.ValueReads != 0)
            {
                throw new InvalidOperationException(
                    "MORPH0046 explicit recovery evaluated or mutated its leaf.");
            }
        }

        private static void VerifyAdaptivePath(
            IMapper mapper,
            ChildMapper child,
            Source source)
        {
            TestMapper.Reset();
            child.Calls.Clear();
            var created = mapper.Map<Source, AdaptiveDestination>(source);

            if (created.Stored.Value != 30 ||
                TestMapper.ChildArgumentReads != 1 ||
                child.Calls.Count != 1 ||
                child.Calls[0].Operation != MappingOperation.Create)
            {
                throw new InvalidOperationException(
                    "Adaptive Create was not preserved by MORPH0046 recovery.");
            }

            TestMapper.Reset();
            child.Calls.Clear();
            ExpectConfiguration(() => mapper.Map(
                source,
                new AdaptiveDestination(new ChildDestination(7))));

            if (TestMapper.ChildArgumentReads != 0 || child.Calls.Count != 0)
            {
                throw new InvalidOperationException(
                    "Unavailable adaptive Update evaluated its source.");
            }
        }

        private static void VerifyWideCurrentSlot(
            IMapper mapper,
            ChildMapper child,
            Source source)
        {
            TestMapper.Reset();
            child.Calls.Clear();
            var compatible = new WideDestination
            {
                Child = new ChildDestination(7)
            };
            var updated = mapper.Map(source, compatible);

            if (!ReferenceEquals(updated, compatible) ||
                ((ChildDestination)updated.Child!).Value != 37 ||
                TestMapper.ChildArgumentReads != 1 ||
                child.Calls.Count != 1 ||
                child.Calls[0].Operation != MappingOperation.Update ||
                child.Calls[0].Destination != 7)
            {
                throw new InvalidOperationException(
                    "A runtime-compatible wide current slot did not update.");
            }

            TestMapper.Reset();
            child.Calls.Clear();
            var incompatible = new WideDestination { Child = "wrong" };
            ExpectMismatch(() => mapper.Map(source, incompatible));

            if (!Equals(incompatible.Child, "wrong") ||
                child.Calls.Count != 0)
            {
                throw new InvalidOperationException(
                    "A wide-slot mismatch dispatched or mutated the nested leaf.");
            }

            TestMapper.Reset();
            child.Calls.Clear();
            var empty = new WideDestination { Child = null };
            mapper.Map(source, empty);

            if (((ChildDestination)empty.Child!).Value != 1030 ||
                TestMapper.ChildArgumentReads != 1 ||
                child.Calls.Count != 1 ||
                child.Calls[0].Destination is not null)
            {
                throw new InvalidOperationException(
                    "A null wide current slot did not reach nested Update.");
            }
        }

        private static void VerifyRuntimeAdaptivePath(
            IMapper mapper,
            ChildMapper child,
            Source source)
        {
            TestMapper.Reset();
            child.Calls.Clear();
            var created = mapper.Map<Source, RuntimeAdaptiveDestination>(
                source);

            if (created.Child.Value.Value != 30 ||
                TestMapper.ChildArgumentReads != 1 ||
                child.Calls.Count != 1 ||
                child.Calls[0].Operation != MappingOperation.Create)
            {
                throw new InvalidOperationException(
                    "Runtime result recovery changed valid adaptive Create.");
            }

            TestMapper.Reset();
            child.Calls.Clear();
            var previous = new RuntimeAdaptiveDestination();
            ExpectConfiguration(() => mapper.Map(source, previous));

            if (previous.Child.Value.Value != -1 ||
                TestMapper.ChildArgumentReads != 0 ||
                child.Calls.Count != 0)
            {
                throw new InvalidOperationException(
                    "Runtime result recovery executed invalid adaptive Update.");
            }
        }

        private static void VerifyAmbiguousLocal(
            IMapper mapper,
            ChildMapper child,
            Source source)
        {
            TestMapper.Reset();
            child.Calls.Clear();
            var created = mapper.Map<Source, AmbiguousDestination>(source);

            if (created.First.Value != 30 ||
                created.Second.Value != 30 ||
                TestMapper.ChildArgumentReads != 1 ||
                child.Calls.Count != 1 ||
                child.Calls[0].Operation != MappingOperation.Create)
            {
                throw new InvalidOperationException(
                    "Ambiguous adaptive-local recovery changed valid Create.");
            }

            TestMapper.Reset();
            child.Calls.Clear();
            var previous = new AmbiguousDestination
            {
                First = new ChildDestination(5),
                Second = new ChildDestination(7)
            };
            ExpectConfiguration(() => mapper.Map(source, previous));

            if (previous.First.Value != 5 ||
                previous.Second.Value != 7 ||
                TestMapper.ChildArgumentReads != 0 ||
                child.Calls.Count != 0)
            {
                throw new InvalidOperationException(
                    "Ambiguous adaptive Update partially executed its leaf.");
            }
        }

        private static void VerifyWrongProxy(TestMapper mapper, Source source)
        {
            TestMapper.Reset();
            ExpectConfiguration(() =>
                ((ITypeMapper<Source, WrongProxyDestination>)mapper).Create(
                    source,
                    default(MappingContext)));

            if (TestMapper.ChildArgumentReads != 0 ||
                TestMapper.ValueReads != 0)
            {
                throw new InvalidOperationException(
                    "Wrong-proxy recovery evaluated its member leaf.");
            }
        }

        private static void VerifyReadOnlyProxy(
            IMapper mapper,
            ChildMapper child,
            Source source)
        {
            TestMapper.Reset();
            child.Calls.Clear();
            var current = new ChildDestination(11);
            var destination = new ReadOnlyDestination(current);
            var updated = mapper.Map(source, destination);

            if (!ReferenceEquals(updated, destination) ||
                !ReferenceEquals(updated.Child, current) ||
                TestMapper.ChildArgumentReads != 1 ||
                child.Calls.Count != 1 ||
                child.Calls[0].Operation != MappingOperation.Update ||
                child.Calls[0].Destination != 11)
            {
                throw new InvalidOperationException(
                    "Eligible get-only proxy did not discard its replacement.");
            }

            TestMapper.Reset();
            child.Calls.Clear();
            var empty = new ReadOnlyDestination(null);
            mapper.Map(source, empty);

            if (TestMapper.ChildArgumentReads != 0 || child.Calls.Count != 0)
            {
                throw new InvalidOperationException(
                    "A null get-only proxy did not skip its nested source.");
            }
        }

        private static void VerifyIndependentPair(
            TestMapper mapper,
            Source source)
        {
            TestMapper.Reset();
            var result =
                ((ITypeMapper<Source, IndependentDestination>)mapper).Create(
                    source,
                    default(MappingContext));

            if (result.Value != 17 || TestMapper.ValueReads != 1)
            {
                throw new InvalidOperationException(
                    "An independent pair was changed by nested recovery.");
            }
        }

        private static void ExpectConfiguration(Action action)
        {
            try
            {
                action();
                throw new InvalidOperationException(
                    "An invalid nested mapping unexpectedly executed.");
            }
            catch (MappingConfigurationException)
            {
            }
        }

        private static void ExpectMismatch(Action action)
        {
            try
            {
                action();
                throw new InvalidOperationException(
                    "An incompatible wide destination was accepted.");
            }
            catch (NestedDestinationTypeMismatchException)
            {
            }
        }
    }
}
