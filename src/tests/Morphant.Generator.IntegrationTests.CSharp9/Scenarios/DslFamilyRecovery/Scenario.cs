#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DslFamilyRecovery
{
    public sealed class Destination
    {
        public Destination(int id) => Id = id;
        public int Id { get; }
    }

    public abstract class Root : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int Id, int Other), Destination>()
                .Convert(s => new Destination(s.Id));
    }

    public abstract class Derived : Root
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<(int Id, int Other), Destination>()
                .Convert(s => new Destination(s.Id + 10));
            builder.Map<int, int>().Convert(s => s + 1);
        }
    }

    [MorphantMapper]
    public partial class RootMapper : Root
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<(int Id, int Other), Destination>()
                .IncludeBase<(int Id, int Other), Destination>();
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : Derived
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<(int Id, int Other), Destination>()
                .IncludeBase<(int Id, int Other), Destination>();
            builder.Map<int, int>().IncludeBase<int, int>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var root = (ITypeMapper<(int Id, int Other), Destination>)new RootMapper();
            var derived = new DerivedMapper();
            var second = (ITypeMapper<(int Id, int Other), Destination>)derived;
            var valid = (ITypeMapper<int, int>)derived;

            if (root.Create((2, 3), default).Id != 2 ||
                root.Update((4, 5), new Destination(7), default).Id != 4 ||
                second.Create((2, 3), default).Id != 12 ||
                second.Update((2, 3), new Destination(7), default).Id != 12 ||
                valid.Create(10, default) != 11 || valid.Update(20, 7, default) != 21)
                throw new InvalidOperationException("Mappers sharing a tuple pair lost their own rules.");
        }
    }
}
