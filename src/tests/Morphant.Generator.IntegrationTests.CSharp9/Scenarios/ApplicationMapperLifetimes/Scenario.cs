#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationMapperLifetimes
{
    public sealed record Source(int Value);
    public sealed record ChildSource(int Value);
    public sealed record ChildDestination(int Value, ChildMapper Mapper, ScopeState State);
    public sealed record Destination(ChildDestination First, ChildDestination Second, ChildDestination Updated);

    public sealed class ScopeState
    {
        public List<ChildMapper> Instances { get; } = new();
        public List<IMapper> Contexts { get; } = new();
        public List<MappingOperation> Operations { get; } = new();
    }

    [MorphantMapper]
    public partial class ChildMapper : TypeMapper<ChildMapper>, IDisposable
    {
        private readonly ScopeState _state;
        public ChildMapper(ScopeState state)
        {
            _state = state;
            state.Instances.Add(this);
        }
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<ChildSource, ChildDestination>()
                .Convert((source, _, context) =>
                {
                    _state.Contexts.Add(context.Mapper);
                    _state.Operations.Add(context.Operation);
                    return new ChildDestination(source!.Value, this, _state);
                });
    }

    [MorphantMapper]
    public partial class RootMapper : TypeMapper<RootMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Convert((source, _, context) =>
                {
                    var child = new ChildSource(source!.Value);
                    var first = context.Mapper.Map<ChildSource, ChildDestination>(child);
                    var second = context.Mapper.Map<ChildSource, ChildDestination>(child);
                    var updated = context.Mapper.Map<ChildSource, ChildDestination>(child, first);
                    return new Destination(first, second, updated);
                });
    }

    public static class Scenario
    {
        public static void Verify(bool transient)
        {
            var services = new ServiceCollection()
                .AddScoped<ScopeState>()
                .AddScoped<ITypeMapper<Source, Destination>, RootMapper>()
                .AddScoped<IMapper, Mapper>();
            services.Add(new ServiceDescriptor(typeof(ITypeMapper<ChildSource, ChildDestination>),
                typeof(ChildMapper), transient ? ServiceLifetime.Transient : ServiceLifetime.Scoped));
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            var first = VerifyScope(provider, transient);
            var second = VerifyScope(provider, transient);
            if (ReferenceEquals(first, second) || first.Instances.Intersect(second.Instances).Any())
                throw new InvalidOperationException("Application scopes must not share scoped state or child mappers.");
        }

        private static ScopeState VerifyScope(ServiceProvider provider, bool transient)
        {
            ScopeState state;
            using (var scope = provider.CreateScope())
            {
                var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
                state = scope.ServiceProvider.GetRequiredService<ScopeState>();
                var created = mapper.Map<Source, Destination>(new Source(7));
                var updated = mapper.Map<Source, Destination>(new Source(11), created);
                var children = new[] { created.First, created.Second, created.Updated, updated.First, updated.Second, updated.Updated };
                if (!children.Select(child => child.Value).SequenceEqual(new[] { 7, 7, 7, 11, 11, 11 }) ||
                    children.Any(child => !ReferenceEquals(child.State, state)) ||
                    children.Select(child => child.Mapper).Distinct().Count() != (transient ? 6 : 1) ||
                    state.Instances.Count != (transient ? 6 : 1) ||
                    state.Instances.Any(child => child.DisposeCount != 0))
                    throw new InvalidOperationException("Nested lookup must respect DI lifetimes without caching or disposing child mappers.");

                if (!state.Operations.SequenceEqual(new[]
                    { MappingOperation.Create, MappingOperation.Create, MappingOperation.Update,
                      MappingOperation.Create, MappingOperation.Create, MappingOperation.Update }) ||
                    state.Contexts.Take(3).Distinct().Count() != 1 ||
                    state.Contexts.Skip(3).Distinct().Count() != 1 ||
                    ReferenceEquals(state.Contexts[0], state.Contexts[3]))
                    throw new InvalidOperationException("Nested operation frames must share one context mapper per root call.");

                foreach (var context in state.Contexts.Distinct())
                {
                    try
                    {
                        context.Map<ChildSource, ChildDestination>(new ChildSource(0));
                        throw new InvalidOperationException("A completed mapping context remained usable.");
                    }
                    catch (MappingScopeCompletedException exception)
                        when (exception.Operation == MappingOperation.Create &&
                              exception.SourceType == typeof(ChildSource) &&
                              exception.DestinationType == typeof(ChildDestination)) { }
                }
            }
            if (state.Instances.Any(child => child.DisposeCount != 1))
                throw new InvalidOperationException("The application DI scope must dispose each child mapper exactly once.");
            return state;
        }
    }
}
