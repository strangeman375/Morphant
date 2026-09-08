#nullable enable
using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PolymorphismGenericVariance
{
    public interface ISource { }
    public interface IProducer<out T> : ISource { }
    public sealed class TextSource : IProducer<string> { }
    public class Result<T> { public MappingOperation Operation { get; set; } }
    public sealed class GenericResult<T> : Result<T> { }
    public sealed class TextResult<T> : Result<T> { }

    [MorphantMapper]
    public partial class BroadFirstMapper<T> : TypeMapper<BroadFirstMapper<T>> where T : class
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ISource, Result<T>>()
                .ForDerived<IProducer<T>, GenericResult<T>>()
                .ForDerived<IProducer<string>, TextResult<T>>()
                .Convert(_ => new Result<T>());
            builder.Map<IProducer<T>, GenericResult<T>>()
                .Convert((_, _, context) => new GenericResult<T> { Operation = context.Operation });
            builder.Map<IProducer<string>, TextResult<T>>()
                .Convert((_, _, context) => new TextResult<T> { Operation = context.Operation });
        }
    }

    [MorphantMapper]
    public partial class SpecificFirstMapper<T> : TypeMapper<SpecificFirstMapper<T>> where T : class
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ISource, Result<T>>()
                .ForDerived<IProducer<string>, TextResult<T>>()
                .ForDerived<IProducer<T>, GenericResult<T>>()
                .Convert(_ => new Result<T>());
            builder.Map<IProducer<T>, GenericResult<T>>()
                .Convert((_, _, context) => new GenericResult<T> { Operation = context.Operation });
            builder.Map<IProducer<string>, TextResult<T>>()
                .Convert((_, _, context) => new TextResult<T> { Operation = context.Operation });
        }
    }

    public static class Scenario
    {
        public static void Verify(bool specificFirst, bool application, bool update)
        {
            // For T = object, IProducer<string> is the strictly more specific branch.
            object generated = specificFirst ? new SpecificFirstMapper<object>() : new BroadFirstMapper<object>();
            using var provider = new ServiceCollection()
                .AddSingleton((ITypeMapper<ISource, Result<object>>)generated)
                .AddSingleton((ITypeMapper<IProducer<object>, GenericResult<object>>)generated)
                .AddSingleton((ITypeMapper<IProducer<string>, TextResult<object>>)generated)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var direct = (ITypeMapper<ISource, Result<object>>)generated;
            var facade = provider.GetRequiredService<IMapper>();
            var source = new TextSource();
            var previous = new TextResult<object>();
            var result = application
                ? update ? facade.Map<ISource, Result<object>>(source, previous) : facade.Map<ISource, Result<object>>(source)
                : update ? direct.Update(source, previous) : direct.Create(source);

            if (result is not TextResult<object> ||
                result.Operation != (update ? MappingOperation.Update : MappingOperation.Create))
                throw new InvalidOperationException("Closed generic covariance must select the text branch and preserve the operation.");
        }
    }
}
