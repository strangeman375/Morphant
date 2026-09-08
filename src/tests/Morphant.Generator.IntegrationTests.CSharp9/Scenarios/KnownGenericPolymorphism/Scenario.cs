#nullable enable
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.KnownGenericPolymorphism
{
    public interface ISource<T> { }
    public interface IBroad<T> : ISource<T> { T Value { get; } }
    public interface ISpecific<T> : IBroad<T> { }
    public interface IOther<T> : ISource<T> { }
    public sealed record Source<T>(T Value) : ISpecific<T>;
    public sealed record AmbiguousSource<T>(T Value) : ISpecific<T>, IOther<T>;
    public class Result<T> { public T Value { get; set; } = default!; public MappingOperation Operation { get; set; } }
    public sealed class BroadResult<T> : Result<T> { }
    public sealed class SpecificResult<T> : Result<T> { }
    public sealed class OtherResult<T> : Result<T> { }

    public interface IProducer<out T> { }
    public sealed class TextProducer : IProducer<string> { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ISource<T>, Result<T>>()
                .ForDerived<IBroad<T>, BroadResult<T>>()
                .ForDerived<ISpecific<T>, SpecificResult<T>>()
                .ForDerived<IOther<T>, OtherResult<T>>()
                .Convert(_ => new Result<T>());
            builder.Map<IBroad<T>, BroadResult<T>>()
                .Members((source, _, _, context) => new() { Value = source.Value, Operation = context.Operation });
            builder.Map<ISpecific<T>, SpecificResult<T>>()
                .Members((source, _, _, context) => new() { Value = source.Value, Operation = context.Operation });
            builder.Map<IOther<T>, OtherResult<T>>()
                .Convert(_ => new OtherResult<T>());

            builder.Map<object, Result<T>>()
                .ForDerived<IProducer<object>, BroadResult<T>>()
                .ForDerived<IProducer<string>, SpecificResult<T>>()
                .Convert(_ => new Result<T>());
            builder.Map<IProducer<object>, BroadResult<T>>()
                .Members((_, _, _, context) => new() { Operation = context.Operation });
            builder.Map<IProducer<string>, SpecificResult<T>>()
                .Members((_, _, _, context) => new() { Operation = context.Operation });
        }
    }

    public static class Scenario
    {
        public static void Verify(bool application, bool update)
        {
            var generated = new TestMapper<string>();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<ISource<string>, Result<string>>>(generated)
                .AddSingleton<ITypeMapper<IBroad<string>, BroadResult<string>>>(generated)
                .AddSingleton<ITypeMapper<ISpecific<string>, SpecificResult<string>>>(generated)
                .AddSingleton<ITypeMapper<IOther<string>, OtherResult<string>>>(generated)
                .AddSingleton<ITypeMapper<object, Result<string>>>(generated)
                .AddSingleton<ITypeMapper<IProducer<object>, BroadResult<string>>>(generated)
                .AddSingleton<ITypeMapper<IProducer<string>, SpecificResult<string>>>(generated)
                .AddSingleton<IMapper, Mapper>().BuildServiceProvider();
            var facade = provider.GetRequiredService<IMapper>();
            var direct = (ITypeMapper<ISource<string>, Result<string>>)generated;
            var source = new Source<string>("mapped");
            var previous = new SpecificResult<string> { Value = "previous" };
            var result = application
                ? update ? facade.Map<ISource<string>, Result<string>>(source, previous) : facade.Map<ISource<string>, Result<string>>(source)
                : update ? direct.Update(source, previous) : direct.Create(source);
            var operation = update ? MappingOperation.Update : MappingOperation.Create;
            if (result is not SpecificResult<string> || result.Value != "mapped" || result.Operation != operation ||
                (update && !ReferenceEquals(result, previous)))
                throw new InvalidOperationException("Known generic inheritance must select the specific branch and retain its Update destination.");

            var closedDirect = (ITypeMapper<object, Result<string>>)generated;
            var text = new TextProducer();
            var closed = application
                ? update ? facade.Map<object, Result<string>>(text, previous) : facade.Map<object, Result<string>>(text)
                : update ? closedDirect.Update(text, previous) : closedDirect.Create(text);
            if (closed is not SpecificResult<string> || closed.Operation != operation ||
                (update && !ReferenceEquals(closed, previous)))
                throw new InvalidOperationException("Closed covariant source branches must remain supported in a generic mapper.");

            try
            {
                var ambiguous = new AmbiguousSource<string>("ambiguous");
                _ = application
                    ? update ? facade.Map<ISource<string>, Result<string>>(ambiguous, previous) : facade.Map<ISource<string>, Result<string>>(ambiguous)
                    : update ? direct.Update(ambiguous, previous) : direct.Create(ambiguous);
                throw new InvalidOperationException("Incomparable generic interfaces must report runtime ambiguity.");
            }
            catch (AmbiguousPolymorphicMappingException exception)
                when (exception.Operation == operation && exception.SourceType == typeof(ISource<string>) &&
                      exception.DestinationType == typeof(Result<string>) &&
                      exception.ActualSourceType == typeof(AmbiguousSource<string>) &&
                      exception.MatchingSourceTypes.SequenceEqual(new[] { typeof(ISpecific<string>), typeof(IOther<string>) }) &&
                      exception.MatchingDestinationTypes.SequenceEqual(new[] { typeof(SpecificResult<string>), typeof(OtherResult<string>) })) { }
        }
    }
}
