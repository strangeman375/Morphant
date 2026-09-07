using Morphant;

namespace Stage05Audit.Cases
{
    public sealed class Source { }
    public sealed class ConstructTag { }
    public sealed class ResolveTag { }
    public sealed class ConstructUsingTag { }
    public sealed class ResolveUsingTag { }
    public sealed class ConvertTag { }
    public sealed class StaticTag { }
    public sealed class BaseTag { }
    public sealed class VirtualTag { }
    public sealed class Destination<T>
    {
        public Destination(string text) { Text = text; }
        public string Text { get; }
    }
    public sealed class MemberDestination { public string Text { get; set; } = ""; }

    public abstract class RootMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : RootMapper<TMapper>
    {
        protected virtual string ReadVirtual() => "root";
        protected override void Configure(MapperBuilder builder) { }
    }

    public abstract class BaseMapper<TMapper> : RootMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        protected string Read() => "base";
        protected static string ReadStatic() => "base-static";
        protected override string ReadVirtual() => "base-virtual";

        public string OriginalRead() => Read();
        public string OriginalStatic() => ReadStatic();
        public string OriginalBase() => base.ReadVirtual();
        public string OriginalVirtual() => ReadVirtual();

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<ConstructTag>>().Construct(_ => new(Read()));
            builder.Map<Source, Destination<ResolveTag>>().Resolve((_, previous) => new(Read()));
            builder.Map<Source, MemberDestination>().Members(_ => new() { Text = Read() });
            builder.Map<Source, Destination<ConstructUsingTag>>().ConstructUsing(_ => new(Read()));
            builder.Map<Source, Destination<ResolveUsingTag>>().ResolveUsing((_, previous) => new(Read()));
            builder.Map<Source, Destination<ConvertTag>>().Convert(_ => new(Read()));
            builder.Map<Source, Destination<StaticTag>>().Convert(_ => new(ReadStatic()));
#if BASE_ACCESS
            builder.Map<Source, Destination<BaseTag>>().Convert(_ => new(base.ReadVirtual()));
#endif
            builder.Map<Source, Destination<VirtualTag>>().Convert(_ => new(ReadVirtual()));
        }
    }

    [MorphantMapper]
    public partial class Mapper : BaseMapper<Mapper>
    {
        protected new string Read() => "derived";
        protected new static string ReadStatic() => "derived-static";
        protected override string ReadVirtual() => "derived-virtual";

        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination<ConstructTag>>().IncludeBase<Source, Destination<ConstructTag>>();
            builder.Map<Source, Destination<ResolveTag>>().IncludeBase<Source, Destination<ResolveTag>>();
            builder.Map<Source, MemberDestination>().IncludeBase<Source, MemberDestination>();
            builder.Map<Source, Destination<ConstructUsingTag>>().IncludeBase<Source, Destination<ConstructUsingTag>>();
            builder.Map<Source, Destination<ResolveUsingTag>>().IncludeBase<Source, Destination<ResolveUsingTag>>();
            builder.Map<Source, Destination<ConvertTag>>().IncludeBase<Source, Destination<ConvertTag>>();
            builder.Map<Source, Destination<StaticTag>>().IncludeBase<Source, Destination<StaticTag>>();
#if BASE_ACCESS
            builder.Map<Source, Destination<BaseTag>>().IncludeBase<Source, Destination<BaseTag>>();
#endif
            builder.Map<Source, Destination<VirtualTag>>().IncludeBase<Source, Destination<VirtualTag>>();
        }
    }

    public static class Scenario
    {
        public static void Run()
        {
            var mapper = new Mapper();
            var source = new Source();
            Check.Equal("original nonvirtual binding", "base", mapper.OriginalRead());
            Check.Equal("original static binding", "base-static", mapper.OriginalStatic());
            Check.Equal("original base binding", "root", mapper.OriginalBase());
            Check.Equal("original virtual dispatch", "derived-virtual", mapper.OriginalVirtual());
            Check.Equal("inherited Construct binding", "base",
                ((ITypeMapper<Source, Destination<ConstructTag>>)mapper).Create(source).Text);
            Check.Equal("inherited Resolve binding", "base",
                ((ITypeMapper<Source, Destination<ResolveTag>>)mapper).Create(source).Text);
            Check.Equal("inherited Members binding", "base",
                ((ITypeMapper<Source, MemberDestination>)mapper).Create(source).Text);
            Check.Equal("inherited ConstructUsing binding", "base",
                ((ITypeMapper<Source, Destination<ConstructUsingTag>>)mapper).Create(source).Text);
            Check.Equal("inherited ResolveUsing binding", "base",
                ((ITypeMapper<Source, Destination<ResolveUsingTag>>)mapper).Create(source).Text);
            Check.Equal("inherited Convert binding", "base",
                ((ITypeMapper<Source, Destination<ConvertTag>>)mapper).Create(source).Text);
            Check.Equal("inherited static binding", "base-static",
                ((ITypeMapper<Source, Destination<StaticTag>>)mapper).Create(source).Text);
#if BASE_ACCESS
            Check.Equal("inherited base binding", "root",
                ((ITypeMapper<Source, Destination<BaseTag>>)mapper).Create(source).Text);
#endif
            Check.Equal("inherited virtual dispatch", "derived-virtual",
                ((ITypeMapper<Source, Destination<VirtualTag>>)mapper).Create(source).Text);
        }
    }
}
