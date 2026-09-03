// Compiled integration scenario: assembly, mapper, and pair setting precedence
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.UnknownDerivedTypeHandling
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public sealed class Unknown : IAnimal { }

    [MorphantMapper]
    public partial class AssemblyMapper : TypeMapper<AssemblyMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IAnimal, object>(global::Morphant.MappingMode.Create)
                .ForDerived<IDog, string>()
                .Convert(_ => "base");
    }

    [MorphantMapper]
    public partial class MapperOverride : TypeMapper<MapperOverride>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.UnknownDerivedTypeHandling(
                global::Morphant.UnknownDerivedTypeHandling.UseBaseMapping);
            builder.Map<IAnimal, object>(global::Morphant.MappingMode.Create)
                .ForDerived<IDog, string>()
                .Convert(_ => "base");
        }
    }

    [MorphantMapper]
    public partial class PairOverride : TypeMapper<PairOverride>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.UnknownDerivedTypeHandling(
                global::Morphant.UnknownDerivedTypeHandling.UseBaseMapping);
            builder.Map<IAnimal, object>(global::Morphant.MappingMode.Create)
                .ForDerived<IDog, string>()
                .UnknownDerivedTypeHandling(
                    global::Morphant.UnknownDerivedTypeHandling.Throw)
                .Convert(_ => "base");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            AssertThrows(new AssemblyMapper());
            AssertUsesBase(new MapperOverride());
            AssertThrows(new PairOverride());
        }

        private static void AssertThrows(TypeMapper mapper)
        {
            try
            {
                ((ITypeMapper<IAnimal, object>)mapper)
                    .Create(new Unknown());
                throw new InvalidOperationException(
                    "Unknown derived source was accepted.");
            }
            catch (UnmatchedPolymorphicMappingException)
            {
            }
        }

        private static void AssertUsesBase(TypeMapper mapper)
        {
            if (!Equals(
                    ((ITypeMapper<IAnimal, object>)mapper)
                        .Create(new Unknown()),
                    "base"))
            {
                throw new InvalidOperationException(
                    "The mapper setting did not override MSBuild.");
            }
        }
    }
}
