#nullable enable
using System;
using Morphant;

namespace Stage06
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
    public static class Program
    {
        public static int Main()
        {
            var source = new Source();
            int failures = 0;
            void Check(string name, int expected, int actual)
            {
                Console.WriteLine($"{name}: expected={expected}, actual={actual}");
                if (expected != actual) failures++;
            }
            Check("inherited generic Members", 11, ((ITypeMapper<Box<Source>, Destination>)new ExplicitMapper()).Create(new Box<Source> { Payload = source }).Value);
            Check("nested ordinary generic C#", 11, new NestedMapper().Original(new Box<Source> { Payload = source }));
            Check("nested generic IncludeMembers", 11, ((ITypeMapper<Box<Source>, Destination>)new NestedMapper()).Create(new Box<Source> { Payload = source }).Value);
            Check("cross-pair IncludeMembers", 11, ((ITypeMapper<Source, Destination>)new CrossPairMapper()).Create(source).Value);
            return failures == 0 ? 0 : 1;
        }
    }
}
