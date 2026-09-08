#nullable enable
#pragma warning disable MORPH0061
using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

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
            // Suppression preserves a typed failure for the unsupported generic configuration.
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
            try
            {
                _ = application
                    ? update ? facade.Map<ISource, Result<object>>(source, previous) : facade.Map<ISource, Result<object>>(source)
                    : update ? direct.Update(source, previous) : direct.Create(source);
                throw new InvalidOperationException("The unsupported base mapping must retain a typed exception stub.");
            }
            catch (MappingConfigurationException exception)
                when (exception.Operation == (update ? MappingOperation.Update : MappingOperation.Create) &&
                      exception.SourceType == typeof(ISource) && exception.DestinationType == typeof(Result<object>) &&
                      exception.Reason == "The mapping configuration is invalid: a ForDerived link is invalid.") { }

            var branch = (ITypeMapper<IProducer<string>, TextResult<object>>)generated;
            var result = application
                ? facade.Map<IProducer<string>, TextResult<object>>(source)
                : branch.Create(source);
            if (result.Operation != MappingOperation.Create)
                throw new InvalidOperationException("An independent valid derived pair must remain executable.");
        }
    }
}
