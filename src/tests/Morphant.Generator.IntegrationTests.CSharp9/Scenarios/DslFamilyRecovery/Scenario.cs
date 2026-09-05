#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0018

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DslFamilyRecovery
{
    public sealed class Destination
    {
        public Destination(int id) => Id = id;
        public int Id { get; }
    }

    public abstract class Root<TMapper> : TypeMapper<TMapper>
        where TMapper : Root<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int Id, int Other), Destination>()
                .Convert(s => new Destination(s.Id));
    }

    public abstract class Derived<TMapper> : Root<TMapper>
        where TMapper : Derived<TMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<(int Code, int Other), Destination>()
                .Convert(s => new Destination(s.Id));
            builder.Map<int, int>().Convert(s => s + 1);
        }
    }

    [MorphantMapper]
    public partial class RootMapper : Root<RootMapper>
    {
        protected override void Configure(MapperBuilder builder) => base.Configure(builder);
    }

    [MorphantMapper]
    public partial class DerivedMapper : Derived<DerivedMapper>
    {
        protected override void Configure(MapperBuilder builder) => base.Configure(builder);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var root = (ITypeMapper<(int Id, int Other), Destination>)new RootMapper();
            var derived = new DerivedMapper();
            var invalid = (ITypeMapper<(int Code, int Other), Destination>)derived;
            var valid = (ITypeMapper<int, int>)derived;

            ExpectFailure(() => invalid.Create((2, 3), default), MappingOperation.Create);
            ExpectFailure(() => invalid.Update((2, 3), new Destination(7), default), MappingOperation.Update);

            if (root.Create((2, 3), default).Id != 2 ||
                root.Update((4, 5), new Destination(7), default).Id != 4 ||
                valid.Create(10, default) != 11 || valid.Update(20, 7, default) != 21)
                throw new InvalidOperationException("Invalid callback binding affected an independent mapping.");
        }

        private static void ExpectFailure(Action map, MappingOperation operation)
        {
            try
            {
                map();
            }
            catch (MappingConfigurationException exception)
            {
                if (exception.Operation == operation)
                    return;
                throw;
            }

            throw new InvalidOperationException("An invalid derived callback executed through its base family.");
        }
    }
}
