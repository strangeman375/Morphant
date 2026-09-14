#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SharedConstruction
{
    public sealed class MergeSource
    {
        public List<string> Events { get; } = new List<string>();
        public int Id { get; init; } = 42;
        public int Mask { get; init; }
        public bool? Optional { get; init; }
        public bool Enabled { get; set; }
        public bool Choose { get; init; }
        public int Mode { get; init; }
        public string? Name { get; init; }
        public bool Check(int index)
        {
            Events.Add(index.ToString());
            var value = (Mask & (1 << index)) != 0;
            if (index == 3) Enabled = value;
            return value;
        }
        public bool TryRead([NotNullWhen(true)] out string? name)
        {
            Events.Add("read");
            name = Name;
            return name != null;
        }
        public Truth After() { Events.Add("after"); return new Truth(this); }
    }

    public sealed class Truth
    {
        private readonly MergeSource _source;
        public Truth(MergeSource source) => _source = source;
        public static bool operator true(Truth value)
        {
            value._source.Events.Add("true");
            return (value._source.Mask & 2) != 0;
        }
        public static bool operator false(Truth value)
        {
            value._source.Events.Add("false");
            return (value._source.Mask & 2) == 0;
        }
    }

    public sealed class MergeDestination
    {
        public MergeDestination(int id) => Id = id;
        public int Id { get; }
    }

    [MorphantMapper]
    public partial class CombinedGuardMapper : TypeMapper<CombinedGuardMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<MergeSource, MergeDestination>()
                .Resolve((source, previous) =>
                {
                    if ((source.Check(0) || source.Check(1)) && previous.HasValue &&
                        (source.Optional ?? source.Check(2)) &&
                        source.Check(3) &&
                        (source.Choose ? source.Check(4) : source.Check(5)) &&
                        (source.Mode switch
                        {
                            0 => source.Check(6),
                            _ => source.Check(7)
                        }) &&
                        source.Name != null && source.Name.Length > 0)
                        return previous.Value;
                    return new(source.Id);
                });
    }

    [MorphantMapper]
    public partial class InnerDeclarationMapper : TypeMapper<InnerDeclarationMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<MergeSource, MergeDestination>()
                .Resolve((source, previous) =>
                {
                    if (source.Check(0) && previous.HasValue)
                    {
                        if (source.TryRead(out var name) && name.Length > 0)
                            return previous.Value;
                    }
                    return new(source.Id);
                });
    }

    [MorphantMapper]
    public partial class TruthGuardMapper : TypeMapper<TruthGuardMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<MergeSource, MergeDestination>()
                .Resolve((source, previous) =>
                {
                    if (source.Check(0) && previous.HasValue)
                    {
                        if (source.After())
                            return previous.Value;
                    }
                    return new(source.Id);
                });
    }

    public static partial class Scenario
    {
        public static void VerifyCombinedGuard(int operation, int sample)
        {
            var cases = new (int Mask, bool? Optional, bool Choose, int Mode, string? Name, string Events, bool Reuse)[]
            {
                (255, null, true, 0, "mapped", "0,2,3,4,6", true),
                (0, null, true, 0, "mapped", "0,1", false),
                (1, null, true, 0, "mapped", "0,2", false),
                (247, true, true, 0, "mapped", "0,3", false),
                (239, true, true, 0, "mapped", "0,3,4", false),
                (223, true, false, 1, "mapped", "0,3,5", false),
                (191, true, true, 0, "mapped", "0,3,4,6", false),
                (127, true, false, 1, "mapped", "0,3,5,7", false),
                (255, false, true, 0, "mapped", "0", false),
                (254, null, false, 1, "mapped", "0,1,2,3,5,7", true),
                (255, true, true, 0, null, "0,3,4,6", false),
                (255, true, true, 0, "", "0,3,4,6", false)
            };
            var data = cases[sample];
            var source = new MergeSource
            {
                Mask = data.Mask, Optional = data.Optional, Choose = data.Choose, Mode = data.Mode, Name = data.Name
            };
            var previous = new MergeDestination(-1);
            var mapper = (ITypeMapper<MergeSource, MergeDestination>)new CombinedGuardMapper();
            var result = operation == 0 ? mapper.Create(source, default) :
                mapper.Update(source, operation == 1 ? null : previous, default);
            var reuse = operation == 2 && data.Reuse;
            var events = operation == 2 ? data.Events : (data.Mask & 1) != 0 ? "0" : "0,1";
            var enabled = operation == 2 && data.Events.Split(',').Contains("3") && (data.Mask & 8) != 0;
            if (string.Join(",", source.Events) != events || source.Enabled != enabled ||
                ReferenceEquals(result, previous) != reuse || result.Id != (reuse ? -1 : 42))
                throw new InvalidOperationException("Combining guards changed precedence, nullable flow or evaluation order.");
        }

        public static void VerifyInnerDeclaration(int operation, bool before, string? name)
        {
            var source = new MergeSource { Mask = before ? 1 : 0, Name = name };
            var previous = new MergeDestination(-1);
            var mapper = (ITypeMapper<MergeSource, MergeDestination>)new InnerDeclarationMapper();
            var result = operation == 0 ? mapper.Create(source, default) :
                mapper.Update(source, operation == 1 ? null : previous, default);
            var enters = operation == 2 && before;
            var reuse = enters && !string.IsNullOrEmpty(name);
            if (string.Join(",", source.Events) != (enters ? "0,read" : "0") ||
                ReferenceEquals(result, previous) != reuse || result.Id != (reuse ? -1 : 42))
                throw new InvalidOperationException("The inner out variable lost its scope or nullable flow.");
        }

        public static void VerifyTruthGuard(int operation, bool before, bool truth)
        {
            var source = new MergeSource { Mask = (before ? 1 : 0) | (truth ? 2 : 0) };
            var previous = new MergeDestination(-1);
            var mapper = (ITypeMapper<MergeSource, MergeDestination>)new TruthGuardMapper();
            var result = operation == 0 ? mapper.Create(source, default) :
                mapper.Update(source, operation == 1 ? null : previous, default);
            var enters = operation == 2 && before;
            var reuse = enters && truth;
            if (string.Join(",", source.Events) != (enters ? "0,after,true" : "0") ||
                ReferenceEquals(result, previous) != reuse || result.Id != (reuse ? -1 : 42))
                throw new InvalidOperationException("Combining guards changed custom truth-operator evaluation.");
        }
    }
}
