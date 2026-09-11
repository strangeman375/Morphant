#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructionAndMembersEvaluation
{
    public enum Route { Expressions, Local, Condition, Factory, Swap, Convention }

    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public int Reads { get; private set; }
        public int Left => -100;
        public Tuple<int, int>? InitialTuple { get; private set; }

        public int Next()
        {
            Events.Add("read:" + ++Reads);
            return Reads;
        }

        public int Observe(Tuple<int, int> tuple)
        {
            InitialTuple = tuple;
            Events.Add("observe");
            return tuple.Item2 + 10;
        }
    }

    public sealed class Destination
    {
        private readonly Source owner;
        private int left;
        private int right = 99;

        public Destination(Source owner, int left)
        {
            this.owner = owner;
            this.left = left;
            owner.Events.Add("construct:" + left);
        }

        public int Left
        {
            get => left;
            set { owner.Events.Add("left:" + value); left = value; }
        }

        public int Right
        {
            get => right;
            set { owner.Events.Add("right:" + value); right = value; }
        }
    }

    [MorphantMapper]
    public partial class ExpressionsMapper : TypeMapper<ExpressionsMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source, source.Next()))
                .Members(source => new() { Left = source.Next(), Right = source.Next() })
                .MemberSelection(MemberSelection.Explicit);
    }

    [MorphantMapper]
    public partial class LocalMapper : TypeMapper<LocalMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source, source.Next()))
                .Members(source =>
                {
                    var value = source.Next();
                    return new() { Left = value, Right = value };
                })
                .MemberSelection(MemberSelection.Explicit);
    }

    [MorphantMapper]
    public partial class ConditionMapper : TypeMapper<ConditionMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source, source.Next()))
                .Members(source =>
                {
                    if (source.Next() > 0)
                        return new() { Left = 10, Right = 20 };
                    return new() { Left = 30, Right = 40 };
                })
                .MemberSelection(MemberSelection.Explicit);
    }

    [MorphantMapper]
    public partial class FactoryMapper : TypeMapper<FactoryMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructUsing(source => new Destination(source, source.Next()))
                .Members(source => new() { Left = source.Next(), Right = source.Next() })
                .MemberSelection(MemberSelection.Explicit);
    }

    [MorphantMapper]
    public partial class SwapMapper : TypeMapper<SwapMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source, source.Next()))
                .Members((_, _, result) => new() { Left = result.Right, Right = result.Left })
                .MemberSelection(MemberSelection.Explicit);
    }

    [MorphantMapper]
    public partial class ConventionMapper : TypeMapper<ConventionMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source, source.Next()))
                .Members((_, _, result) =>
                {
                    if (result.Left > 0)
                        return new() { Right = result.Left };
                    return new() { Left = 10, Right = 20 };
                });
    }

    [MorphantMapper]
    public partial class TupleMapper : TypeMapper<TupleMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Tuple<int, int>>()
                .Construct(source => new(source.Next(), source.Next()))
                .Members(source => new() { Item1 = source.Next(), Item2 = source.Next() });
    }

    [MorphantMapper]
    public partial class TupleResultMapper : TypeMapper<TupleResultMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Tuple<int, int>>()
                .Construct(source => new(source.Next(), source.Next()))
                .Members((source, _, result) => new() { Item1 = source.Observe(result), Item2 = result.Item1 });
    }

    [MorphantMapper]
    public partial class TupleLocalMapper : TypeMapper<TupleLocalMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Tuple<int, int>>()
                .Construct(source => new(source.Next(), source.Next()))
                .Members(source =>
                {
                    var value = source.Next();
                    return new() { Item1 = value, Item2 = value };
                });
    }

    [MorphantMapper]
    public partial class TupleConditionMapper : TypeMapper<TupleConditionMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Tuple<int, int>>()
                .Construct(source => new(source.Next(), source.Next()))
                .Members(source =>
                {
                    if (source.Next() > 0)
                        return new() { Item1 = source.Next(), Item2 = source.Next() };
                    return new() { Item1 = -1, Item2 = -2 };
                });
    }

    public static class Scenario
    {
        public static void Verify(Route route)
        {
            ITypeMapper<Source, Destination> mapper = route switch
            {
                Route.Expressions => new ExpressionsMapper(),
                Route.Local => new LocalMapper(),
                Route.Condition => new ConditionMapper(),
                Route.Factory => new FactoryMapper(),
                Route.Swap => new SwapMapper(),
                Route.Convention => new ConventionMapper(),
                _ => throw new ArgumentOutOfRangeException(nameof(route))
            };
            var source = new Source();
            var created = mapper.Create(source);
            var expected = route switch
            {
                Route.Local => "read:1,construct:1,read:2,left:2,right:2",
                Route.Condition => "read:1,construct:1,read:2,left:10,right:20",
                Route.Swap => "read:1,construct:1,left:99,right:1",
                Route.Convention => "read:1,construct:1,right:1",
                _ => "read:1,construct:1,read:2,read:3,left:2,right:3"
            };
            Equal(expected, string.Join(",", source.Events));
            source.Events.Clear();
            var previousLeft = created.Left;
            var previousRight = created.Right;
            var reads = source.Reads;
            var updated = mapper.Update(source, created);
            if (!ReferenceEquals(created, updated)) throw new InvalidOperationException("Reuse changed identity.");
            expected = route switch
            {
                Route.Local => $"read:{reads + 1},left:{reads + 1},right:{reads + 1}",
                Route.Condition => $"read:{reads + 1},left:10,right:20",
                Route.Swap => $"left:{previousRight},right:{previousLeft}",
                Route.Convention => $"right:{previousLeft},left:-100",
                _ => $"read:{reads + 1},read:{reads + 2},left:{reads + 1},right:{reads + 2}"
            };
            Equal(expected, string.Join(",", source.Events));
        }

        public static void VerifyTuple(bool readsResult)
        {
            ITypeMapper<Source, Tuple<int, int>> mapper = readsResult ? new TupleResultMapper() : new TupleMapper();
            var source = new Source();
            var created = mapper.Create(source);
            if (readsResult)
            {
                Equal("read:1,read:2,observe", string.Join(",", source.Events));
                if (created.Item1 != 12 || created.Item2 != 1 || source.InitialTuple is not { Item1: 1, Item2: 2 } ||
                    ReferenceEquals(source.InitialTuple, created))
                    throw new InvalidOperationException("The user method must receive the stable initial tuple.");
            }
            else
            {
                Equal("read:1,read:2,read:3,read:4", string.Join(",", source.Events));
                if (created.Item1 != 3 || created.Item2 != 4)
                    throw new InvalidOperationException("Explicit tuple evaluations were lost.");
            }
            source.Events.Clear();
            if (!ReferenceEquals(created, mapper.Update(source, created)) || source.Events.Count != 0)
                throw new InvalidOperationException("Tuple reuse evaluated creation-only rules.");
        }

        public static void VerifyTupleBlock(bool condition)
        {
            ITypeMapper<Source, Tuple<int, int>> mapper = condition ? new TupleConditionMapper() : new TupleLocalMapper();
            var source = new Source();
            var created = mapper.Create(source);
            Equal(condition ? "read:1,read:2,read:3,read:4,read:5" : "read:1,read:2,read:3",
                string.Join(",", source.Events));
            if (created.Item1 != (condition ? 4 : 3) || created.Item2 != (condition ? 5 : 3))
                throw new InvalidOperationException("Tuple member blocks must run after initial element evaluations.");
        }

        private static void Equal(string expected, string actual)
        {
            if (expected != actual) throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }
}
