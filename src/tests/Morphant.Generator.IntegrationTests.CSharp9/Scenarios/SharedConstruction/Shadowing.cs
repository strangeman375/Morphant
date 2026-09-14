#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SharedConstruction
{
    public sealed class ShadowSource
    {
        public int Id { get; init; }
        public bool Reuse { get; init; }
        public int Read() => Id;
    }

    public sealed class ShadowDestination
    {
        public ShadowDestination(int id) => Id = id;
        public int Id { get; }
    }

    [MorphantMapper]
    public partial class ShadowedCreateMapper : TypeMapper<ShadowedCreateMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<ShadowSource, ShadowDestination>()
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue)
                    {
                        Func<ShadowSource, MappingContext, ShadowDestination> __Create =
                            (value, context) => new ShadowDestination(-1);
                        if (source.Reuse && __Create(source, default).Id == -1)
                            return previous.Value;
                        return new(source.Id);
                    }
                    return new(source.Id);
                });
    }

    [MorphantMapper]
    public partial class ShadowedConstructMapper : TypeMapper<ShadowedConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<ShadowSource, ShadowDestination>()
                .Resolve((source, previous) =>
                {
                    var id = source.Read();
                    if (previous.HasValue)
                    {
                        Func<ShadowSource, int, MappingContext, ShadowDestination> __Construct =
                            (value, number, context) => new ShadowDestination(-1);
                        if (source.Reuse && __Construct(source, id, default).Id == -1)
                            return previous.Value;
                        return new(id);
                    }
                    return new(id);
                });
    }

    public static partial class Scenario
    {
        public static void VerifyShadowing(bool construct, int operation, bool reuse)
        {
            var source = new ShadowSource { Id = 10, Reuse = reuse };
            var previous = new ShadowDestination(99);
            ITypeMapper<ShadowSource, ShadowDestination> mapper = construct
                ? (ITypeMapper<ShadowSource, ShadowDestination>)new ShadowedConstructMapper()
                : new ShadowedCreateMapper();
            var result = operation == 0 ? mapper.Create(source, default) :
                mapper.Update(source, operation == 1 ? null : previous, default);
            var expected = operation == 2 && reuse ? 99 : 10;
            if (result.Id != expected || ReferenceEquals(result, previous) != (operation == 2 && reuse))
                throw new InvalidOperationException("A user delegate intercepted the shared construction call.");
        }
    }
}
