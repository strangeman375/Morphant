// Compiled integration scenario: authoritative tuple factory results
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;
using OuterTupleMembers =
    Morphant.Generated.Tuples.SystemTuple1_Type_System_Collections_Generic_List1_Int32.TupleMembers;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleFactories_a7b10003
{
    public sealed record ValueSource(int Id, string Name);

    public sealed class SystemSource
    {
        public int Mode { get; init; }

        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public Tuple<int, string>? Replacement { get; init; }
    }

    public sealed record NullSource(int Value);

    public sealed record ListSource(int Value);

    public sealed record OuterSource(ListSource Child);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ValueFactoryCalls { get; private set; }

        public static int ValueMemberCalls { get; private set; }

        public static int SystemFactoryCalls { get; private set; }

        public static int NullFactoryCalls { get; private set; }

        public static int OuterFactoryCalls { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ValueSource, (int Id, string Name)>()
                .ConstructUsing(CreateValueTuple)
                .Members(source => new()
                {
                    Name = MapValueName(source)
                })
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<SystemSource, Tuple<int, string>>()
                .ResolveUsing(ResolveSystemTuple)
                .Members(source => new()
                {
                    Item2 = Ignore()
                })
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<NullSource, Tuple<int>>()
                .ConstructUsing(CreateNullTuple)
                .Members(source => new()
                {
                    Item1 = Ignore()
                })
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<ListSource, List<int>>()
                .Convert((source, previous) =>
                {
                    var result = previous.HasValue
                        ? previous.Value
                        : new List<int>();

                    result.Clear();
                    result.Add(source!.Value);
                    return result;
                });

            builder.Map<OuterSource, Tuple<List<int>>>()
                .ConstructUsing(CreateOuterTuple)
                .Members((source, _) =>
                {
                    var members = new OuterTupleMembers();

                    Update<List<int>>(source.Child, members.Item1);
                    return members;
                });
        }

        public static void ResetCounters()
        {
            ValueFactoryCalls = 0;
            ValueMemberCalls = 0;
            SystemFactoryCalls = 0;
            NullFactoryCalls = 0;
            OuterFactoryCalls = 0;
        }

        private static (int Id, string Name) CreateValueTuple(
            ValueSource source)
        {
            ValueFactoryCalls++;
            return (source.Id + 100, source.Name + ":factory");
        }

        private static string MapValueName(ValueSource source)
        {
            ValueMemberCalls++;
            return source.Name + ":member";
        }

        private static Tuple<int, string> ResolveSystemTuple(
            SystemSource source,
            Option<Tuple<int, string>> previous)
        {
            SystemFactoryCalls++;

            if (source.Mode == 1 && previous.HasValue)
            {
                return previous.Value;
            }

            if (source.Mode == 2 && source.Replacement is not null)
            {
                return source.Replacement;
            }

            return new Tuple<int, string>(source.Id, source.Name);
        }

        private static Tuple<int> CreateNullTuple(NullSource source)
        {
            NullFactoryCalls++;
            _ = source.Value;
            return null!;
        }

        private static Tuple<List<int>> CreateOuterTuple(OuterSource source)
        {
            OuterFactoryCalls++;
            _ = source.Child;
            return new Tuple<List<int>>(new List<int> { -1 });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            VerifyValueTupleFactory(mapper);
            VerifySystemTupleFactory(mapper);
            VerifyNullFactoryResult(mapper);
            VerifyNestedUpdateOnAuthoritativeResult(mapper);
        }

        private static void VerifyValueTupleFactory(TestMapper mapper)
        {
            var contract =
                (ITypeMapper<ValueSource, (int Id, string Name)>)mapper;

            TestMapper.ResetCounters();
            var created = contract.Create(
                new ValueSource(3, "created"),
                default(MappingContext));

            if (created != (103, "created:member") ||
                TestMapper.ValueFactoryCalls != 1 ||
                TestMapper.ValueMemberCalls != 1)
            {
                throw new InvalidOperationException(
                    "ConstructUsing did not remain observable exactly once.");
            }

            TestMapper.ResetCounters();
            var updated = contract.Update(
                new ValueSource(5, "updated"),
                (Id: 107, Name: "existing"),
                default(MappingContext));

            if (updated != (107, "updated:member") ||
                TestMapper.ValueFactoryCalls != 0 ||
                TestMapper.ValueMemberCalls != 1)
            {
                throw new InvalidOperationException(
                    "ValueTuple Update unexpectedly invoked ConstructUsing.");
            }
        }

        private static void VerifySystemTupleFactory(TestMapper mapper)
        {
            var contract =
                (ITypeMapper<SystemSource, Tuple<int, string>>)mapper;

            TestMapper.ResetCounters();
            var created = contract.Create(
                new SystemSource
                {
                    Id = 11,
                    Name = "created"
                },
                default(MappingContext));

            if (created.Item1 != 11 ||
                created.Item2 != "created" ||
                TestMapper.SystemFactoryCalls != 1)
            {
                throw new InvalidOperationException(
                    "ResolveUsing Create changed its authoritative result.");
            }

            var existing = new Tuple<int, string>(13, "existing");
            TestMapper.ResetCounters();
            var reused = contract.Update(
                new SystemSource { Mode = 1 },
                existing,
                default(MappingContext));

            if (!ReferenceEquals(existing, reused) ||
                TestMapper.SystemFactoryCalls != 1)
            {
                throw new InvalidOperationException(
                    "ResolveUsing did not return the exact previous instance.");
            }

            var replacement = new Tuple<int, string>(17, "replacement");
            TestMapper.ResetCounters();
            var replaced = contract.Update(
                new SystemSource
                {
                    Mode = 2,
                    Replacement = replacement
                },
                existing,
                default(MappingContext));

            if (!ReferenceEquals(replacement, replaced) ||
                ReferenceEquals(existing, replaced) ||
                TestMapper.SystemFactoryCalls != 1)
            {
                throw new InvalidOperationException(
                    "ResolveUsing reconstructed a replacement instance.");
            }
        }

        private static void VerifyNullFactoryResult(TestMapper mapper)
        {
            var contract = (ITypeMapper<NullSource, Tuple<int>>)mapper;

            TestMapper.ResetCounters();
            var result = contract.Create(
                new NullSource(19),
                default(MappingContext));

            if (result is not null || TestMapper.NullFactoryCalls != 1)
            {
                throw new InvalidOperationException(
                    "A null factory result was not terminal.");
            }
        }

        private static void VerifyNestedUpdateOnAuthoritativeResult(
            TestMapper mapper)
        {
            var contract =
                (ITypeMapper<OuterSource, Tuple<List<int>>>)mapper;

            TestMapper.ResetCounters();
            var created = contract.Create(
                new OuterSource(new ListSource(23)));
            var createdList = created.Item1;

            if (createdList.Count != 1 ||
                createdList[0] != 23 ||
                TestMapper.OuterFactoryCalls != 1)
            {
                throw new InvalidOperationException(
                    "Nested Update did not apply to a factory result.");
            }

            var existingList = new List<int> { -1 };
            var existing = new Tuple<List<int>>(existingList);
            TestMapper.ResetCounters();
            var updated = contract.Update(
                new OuterSource(new ListSource(29)),
                existing);

            if (!ReferenceEquals(existing, updated) ||
                !ReferenceEquals(existingList, updated.Item1) ||
                updated.Item1.Count != 1 ||
                updated.Item1[0] != 29 ||
                TestMapper.OuterFactoryCalls != 0)
            {
                throw new InvalidOperationException(
                    "Nested Update replaced an authoritative System.Tuple.");
            }
        }
    }
}
