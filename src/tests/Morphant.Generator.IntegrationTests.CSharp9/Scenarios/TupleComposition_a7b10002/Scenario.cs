// Compiled integration scenario: tuple construction and member composition
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;
using FusionTupleMembers =
    Morphant.Generated.Tuples.V2_a51caaf0c27a1203d7dd02a67a0a5455.TupleMembers;
using MixedTupleMembers =
    Morphant.Generated.Tuples.S2_07c16aa828a1cc0400a34298febbe3a6.TupleMembers;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleComposition_a7b10002
{
    public sealed record FusionSource(int Id, string Name);

    public sealed record ResultValueSource(int Id, string Text);

    public sealed record SystemSource(int Id, string Text);

    public sealed record ResolveSource(bool Reuse, int Id, string Text);

    public sealed record NestedSource(int Value);

    public sealed record MixedSource(NestedSource Child, string Text);

    public sealed class Payload
    {
        public Payload(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class MutablePayload
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        private static readonly List<string> Events = new List<string>();

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<FusionSource, (int Id, string Name)>()
                .Construct(source => new(
                    InitialId(source),
                    InitialName(source)))
                .Members(source => new FusionTupleMembers
                {
                    Id = FinalId(source)
                })
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<ResultValueSource, (int Id, string Text)>()
                .Construct(source => new(
                    InitialResultId(source),
                    InitialResultText(source)))
                .Members((source, _, result) => new()
                {
                    Text = FinalResultText(source, result.Text)
                })
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<SystemSource, Tuple<Payload, string>>()
                .Construct(source => new(
                    CreatePayload(source),
                    InitialSystemText(source)))
                .Members((source, _, result) => new()
                {
                    Item2 = FinalSystemText(source, result.Item1)
                })
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<ResolveSource, Tuple<int, string>>()
                .Resolve((source, previous) =>
                {
                    if (source.Reuse && previous.HasValue)
                    {
                        return previous;
                    }

                    return new(
                        source.Id,
                        InitialResolveText(source));
                })
                .Members(source => new()
                {
                    Item2 = FinalResolveText(source)
                })
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<NestedSource, MutablePayload>();

            builder.Map<MixedSource, Tuple<MutablePayload, string>>()
                .Construct(source => new(
                    new MutablePayload(),
                    source.Text + ":initial"))
                .Members((source, _, result) =>
                {
                    var members =
                        new MixedTupleMembers
                        {
                            Item2 = source.Text + ":" + result.Item1.Value
                        };

                    Update<MutablePayload>(source.Child, members.Item1);
                    return members;
                })
                .MemberSelection(MemberSelection.Explicit);
        }

        public static void ClearEvents() => Events.Clear();

        public static string EventLog() => string.Join(",", Events);

        private static int InitialId(FusionSource source)
        {
            Events.Add("discarded-id");
            return source.Id + 1000;
        }

        private static string InitialName(FusionSource source)
        {
            Events.Add("initial-name");
            return source.Name + ":initial";
        }

        private static int FinalId(FusionSource source)
        {
            Events.Add("final-id");
            return source.Id;
        }

        private static int InitialResultId(ResultValueSource source)
        {
            Events.Add("result-id");
            return source.Id;
        }

        private static string InitialResultText(ResultValueSource source)
        {
            Events.Add("result-initial");
            return source.Text + ":initial";
        }

        private static string FinalResultText(
            ResultValueSource source,
            string current)
        {
            Events.Add("result-final");
            return current + ":" + source.Id;
        }

        private static Payload CreatePayload(SystemSource source)
        {
            Events.Add("system-payload");
            return new Payload(source.Id);
        }

        private static string InitialSystemText(SystemSource source)
        {
            Events.Add("system-initial");
            return source.Text + ":initial";
        }

        private static string FinalSystemText(
            SystemSource source,
            Payload payload)
        {
            Events.Add("system-final");
            return source.Text + ":" + payload.Value;
        }

        private static string InitialResolveText(ResolveSource source)
        {
            Events.Add("resolve-discarded");
            return source.Text + ":initial";
        }

        private static string FinalResolveText(ResolveSource source)
        {
            Events.Add("resolve-final");
            return source.Text + ":final";
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            VerifyFusedCreateAndUpdate(mapper);
            VerifyResultDependentValueTuple(mapper);
            VerifyResultDependentSystemTuple(mapper);
            VerifyDeclarativeResolveBranches(mapper);
            VerifyReconstructionWithNestedUpdate(mapper);
        }

        private static void VerifyFusedCreateAndUpdate(TestMapper mapper)
        {
            var contract =
                (ITypeMapper<FusionSource, (int Id, string Name)>)mapper;

            TestMapper.ClearEvents();
            var created = contract.Create(
                new FusionSource(7, "created"),
                default(MappingContext));

            if (created != (7, "created:initial") ||
                TestMapper.EventLog() != "initial-name,final-id")
            {
                throw new InvalidOperationException(
                    "Tuple fusion evaluated an overridden rule or reordered " +
                    "surviving rules.");
            }

            TestMapper.ClearEvents();
            var updated = contract.Update(
                new FusionSource(11, "unused"),
                (Id: -1, Name: "preserved"),
                default(MappingContext));

            if (updated != (11, "preserved") ||
                TestMapper.EventLog() != "final-id")
            {
                throw new InvalidOperationException(
                    "ValueTuple Update did not mutate the by-value result.");
            }
        }

        private static void VerifyResultDependentValueTuple(TestMapper mapper)
        {
            var contract =
                (ITypeMapper<ResultValueSource, (int Id, string Text)>)mapper;

            TestMapper.ClearEvents();
            var created = contract.Create(
                new ResultValueSource(13, "value"),
                default(MappingContext));

            if (created != (13, "value:initial:13") ||
                TestMapper.EventLog() !=
                    "result-id,result-initial,result-final")
            {
                throw new InvalidOperationException(
                    "A result-dependent ValueTuple rule lost its initial " +
                    "materialization.");
            }

            TestMapper.ClearEvents();
            var updated = contract.Update(
                new ResultValueSource(17, "ignored"),
                (Id: 19, Text: "existing"),
                default(MappingContext));

            if (updated != (19, "existing:17") ||
                TestMapper.EventLog() != "result-final")
            {
                throw new InvalidOperationException(
                    "A result-dependent ValueTuple Update used construction.");
            }
        }

        private static void VerifyResultDependentSystemTuple(TestMapper mapper)
        {
            var contract =
                (ITypeMapper<SystemSource, Tuple<Payload, string>>)mapper;

            TestMapper.ClearEvents();
            var created = contract.Create(
                new SystemSource(23, "legacy"),
                default(MappingContext));

            if (created.Item1.Value != 23 ||
                created.Item2 != "legacy:23" ||
                TestMapper.EventLog() !=
                    "system-payload,system-initial,system-final")
            {
                throw new InvalidOperationException(
                    "A result-dependent System.Tuple plan was not " +
                    "materialized and reconstructed exactly once.");
            }

            TestMapper.ClearEvents();
            var existing = new Tuple<Payload, string>(
                new Payload(29),
                "preserved");
            var updated = contract.Update(
                new SystemSource(31, "unused"),
                existing,
                default(MappingContext));

            if (!ReferenceEquals(existing, updated) ||
                TestMapper.EventLog() != string.Empty)
            {
                throw new InvalidOperationException(
                    "System.Tuple scalar rules reconstructed Update.");
            }
        }

        private static void VerifyDeclarativeResolveBranches(TestMapper mapper)
        {
            var contract =
                (ITypeMapper<ResolveSource, Tuple<int, string>>)mapper;

            TestMapper.ClearEvents();
            var created = contract.Create(
                new ResolveSource(false, 37, "create"),
                default(MappingContext));

            if (created.Item1 != 37 ||
                created.Item2 != "create:final" ||
                TestMapper.EventLog() != "resolve-final")
            {
                throw new InvalidOperationException(
                    "Resolve construction did not fuse its final element plan.");
            }

            var existing = new Tuple<int, string>(41, "existing");
            TestMapper.ClearEvents();
            var reused = contract.Update(
                new ResolveSource(true, 43, "reuse"),
                existing,
                default(MappingContext));

            if (!ReferenceEquals(existing, reused) ||
                TestMapper.EventLog() != string.Empty)
            {
                throw new InvalidOperationException(
                    "Resolve reuse evaluated creation-only tuple rules.");
            }

            TestMapper.ClearEvents();
            var replaced = contract.Update(
                new ResolveSource(false, 47, "replace"),
                existing,
                default(MappingContext));

            if (ReferenceEquals(existing, replaced) ||
                replaced.Item1 != 47 ||
                replaced.Item2 != "replace:final" ||
                TestMapper.EventLog() != "resolve-final")
            {
                throw new InvalidOperationException(
                    "Resolve replacement did not receive the final tuple plan.");
            }
        }

        private static void VerifyReconstructionWithNestedUpdate(
            TestMapper mapper)
        {
            var contract =
                (ITypeMapper<MixedSource, Tuple<MutablePayload, string>>)mapper;
            var created = contract.Create(
                new MixedSource(new NestedSource(53), "mixed"));

            if (created.Item1.Value != 53 ||
                created.Item2 != "mixed:53")
            {
                throw new InvalidOperationException(
                    "System.Tuple reconstruction lost a nested statement " +
                    "Update.");
            }

            var payload = new MutablePayload { Value = 59 };
            var existing = new Tuple<MutablePayload, string>(
                payload,
                "preserved");
            var updated = contract.Update(
                new MixedSource(new NestedSource(61), "ignored"),
                existing);

            if (!ReferenceEquals(existing, updated) ||
                !ReferenceEquals(payload, updated.Item1) ||
                updated.Item1.Value != 61 ||
                updated.Item2 != "preserved")
            {
                throw new InvalidOperationException(
                    "System.Tuple Update did not preserve the outer " +
                    "instance while applying a nested statement Update.");
            }
        }
    }
}
