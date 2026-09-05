#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DslFamilyIsolation
{
    public sealed class Source<T>
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public sealed class Destination<T>
    {
        public Destination(int id) => Id = id;
        public int Id { get; }
        public string Label { get; set; } = string.Empty;
    }

    public sealed class Payload { }

    [MorphantMapper]
    public partial class Root<TMapper, T> : TypeMapper<TMapper>
        where TMapper : Root<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MemberSelection(MemberSelection.Explicit);
            builder.Map<Source<T>, Destination<T>>()
                .Construct(s => new(s.Id + 10))
                .Members(s => new() { Label = "root:" + s.Label });
        }
    }

    [MorphantMapper]
    public partial class Independent<TMapper, T> : Root<TMapper, T>
        where TMapper : Independent<TMapper, T>
        where T : class, new()
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Construct(s => new(s.Id + 20));
    }

    [MorphantMapper]
    public partial class Connected<TMapper, T> : Root<TMapper, T>
        where TMapper : Connected<TMapper, T>
        where T : class, new()
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<T>, Destination<T>>()
                .Construct(s => new(s.Id + 30));
        }
    }

    public sealed class RootMapper : Root<RootMapper, Payload> { }
    public sealed class IndependentMapper : Independent<IndependentMapper, Payload> { }
    public sealed class ConnectedMapper : Connected<ConnectedMapper, Payload> { }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = new Source<Payload> { Id = 2, Label = "value" };
            var root = (ITypeMapper<Source<Payload>, Destination<Payload>>)new RootMapper();
            var independent = (ITypeMapper<Source<Payload>, Destination<Payload>>)new IndependentMapper();
            var connected = (ITypeMapper<Source<Payload>, Destination<Payload>>)new ConnectedMapper();

            Check(root.Create(source, default), 12, "root:value");
            Check(independent.Create(source, default), 22, "value");
            Check(connected.Create(source, default), 32, string.Empty);

            Check(root.Update(source, new Destination<Payload>(7) { Label = "old" }, default), 7, "root:value");
            Check(independent.Update(source, new Destination<Payload>(7) { Label = "old" }, default), 7, "value");
            Check(connected.Update(source, new Destination<Payload>(7) { Label = "old" }, default), 7, "old");
        }

        private static void Check(Destination<Payload> result, int id, string label)
        {
            if (result.Id != id || result.Label != label)
                throw new InvalidOperationException("Related mapper families mixed their callbacks or settings.");
        }
    }
}
