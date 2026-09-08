#nullable enable
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericSourceMemberBinding
{
    public sealed class Profile { public int Value { get; set; } }
    public class BaseSource { public Profile Profile { get; } = new() { Value = 11 }; }
    public sealed class Source : BaseSource { public new Profile Profile { get; } = new() { Value = 99 }; }
    public sealed class Box<T> where T : BaseSource { public T Payload { get; set; } = default!; }
    public sealed class Destination { public int Value { get; set; } }

    public abstract class ExplicitFamily<TMapper, TSource> : TypeMapper<TMapper>
        where TMapper : ExplicitFamily<TMapper, TSource>
        where TSource : BaseSource
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Box<TSource>, Destination>()
            .Members(source => new() { Value = source.Payload.Profile.Value });
    }
    [MorphantMapper]
    public partial class ExplicitMapper : ExplicitFamily<ExplicitMapper, Source>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Box<Source>, Destination>().IncludeBase<Box<Source>, Destination>();
        }
    }
    public abstract class NestedFamily<TMapper, TSource> : TypeMapper<TMapper>
        where TMapper : NestedFamily<TMapper, TSource>
        where TSource : BaseSource
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Box<TSource>, Destination>()
            .IncludeMembers(source => source.Payload.Profile);
        public int Original(Box<TSource> source) => source.Payload.Profile.Value;
    }
    [MorphantMapper]
    public partial class NestedMapper : NestedFamily<NestedMapper, Source>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Box<Source>, Destination>().IncludeBase<Box<Source>, Destination>();
        }
    }
    [MorphantMapper]
    public partial class CrossPairMapper : TypeMapper<CrossPairMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>().IncludeBase<BaseSource, Destination>();
            builder.Map<BaseSource, Destination>().IncludeMembers(source => source.Profile);
        }
    }
    public enum Callback { Members, IncludeMembers }
    public enum Operation { Create, UpdateWithoutDestination, UpdateExisting }

    public static class Scenario
    {
        public static void Verify(Callback callback, Operation operation)
        {
            ITypeMapper<Box<Source>, Destination> mapper = callback == Callback.Members
                ? new ExplicitMapper() : new NestedMapper();
            var source = new Box<Source> { Payload = new Source() };
            var previous = operation == Operation.UpdateExisting ? new Destination { Value = 7 } : null;
            var result = operation == Operation.Create ? mapper.Create(source) : mapper.Update(source, previous);
            if (result.Value != 11)
                throw new InvalidOperationException($"{callback}/{operation}: expected the original property value 11, got {result.Value}.");
            if (operation == Operation.UpdateExisting && !ReferenceEquals(previous, result))
                throw new InvalidOperationException("Update replaced the destination.");
        }

        public static void VerifyOriginalBinding()
        {
            var source = new Source();
            if (new NestedMapper().Original(new Box<Source> { Payload = source }) != 11 ||
                ((ITypeMapper<Source, Destination>)new CrossPairMapper()).Create(source).Value != 11)
                throw new InvalidOperationException("The generic C# and cross-pair controls must read the base property.");
        }
    }
}
