#nullable enable
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.IndirectPairConstraints
{
    internal struct Cell<T, TTag> { public T Value { get; set; } }

    internal sealed class Packet<TSequence, TCell> where TSequence : IEnumerable<object> where TCell : unmanaged
    {
        public int Code { get; set; }
    }

    [MorphantMapper]
    internal partial class First<T, TValue, TTag> : TypeMapper where T : Exception where TValue : unmanaged
    {
        protected override void Configure(MapperBuilder b) =>
            b.Map<Packet<List<T>, Cell<TValue, TTag>>, int>().Convert(s => (s?.Code ?? 0) + 1);
    }

    [MorphantMapper]
    internal partial class Second<T, TValue, TTag> : TypeMapper where T : class where TValue : unmanaged
    {
        protected override void Configure(MapperBuilder b) =>
            b.Map<Packet<List<T>, Cell<TValue, TTag>>, int>().Convert(s => (s?.Code ?? 0) + 2);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<Packet<List<Exception>, Cell<int, string>>, int> first = new First<Exception, int, string>();
            ITypeMapper<Packet<List<string>, Cell<long, object>>, int> second = new Second<string, long, object>();
            if (first.Create(new Packet<List<Exception>, Cell<int, string>> { Code = 10 }) != 11 ||
                second.Create(new Packet<List<string>, Cell<long, object>> { Code = 20 }) != 22)
                throw new InvalidOperationException("The shared extension changed a mapper's callback.");
        }
    }
}