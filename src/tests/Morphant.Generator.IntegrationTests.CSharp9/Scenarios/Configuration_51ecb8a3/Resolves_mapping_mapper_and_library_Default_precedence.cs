// Compiled integration scenario: TypeMapperConstructorSelectionTests/ConfigurationTests::Resolves_mapping_mapper_and_library_Default_precedence
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Configuration_51ecb8a3
{
    public sealed class Source
    {
        public int Id { get; init; }

        public int Code { get; init; }
    }

    public sealed class RootDestination
    {
        public RootDestination()
        {
            Kind = "parameterless";
        }

        public RootDestination(int id)
        {
            Kind = id.ToString();
        }

        public string Kind { get; }
    }

    public sealed class MappingDestination
    {
        public MappingDestination(int id)
        {
            Kind = "small:" + id;
        }

        public MappingDestination(
            int code,
            string label = "default")
        {
            Kind = "largest:" + code + ":" + label;
        }

        public string Kind { get; }
    }

    public sealed class LibraryDestination
    {
        public LibraryDestination()
        {
            Kind = "parameterless";
        }

        public LibraryDestination(int id)
        {
            Kind = "unambiguous:" + id;
        }

        public string Kind { get; }
    }

    [MorphantMapper]
    public partial class ConfiguredMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.ConstructorSelection(
                ConstructorSelection.Parameterless);

            builder.Map<Source, RootDestination>()
                .ConstructorSelection(ConstructorSelection.Default);
            builder.Map<Source, MappingDestination>()
                .ConstructorSelection(ConstructorSelection.Largest);
        }
    }

    [MorphantMapper]
    public partial class LibraryMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, LibraryDestination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = new Source { Id = 17, Code = 31 };
            var context = default(MappingContext);
            var configured = new ConfiguredMapper();
            var root =
                ((ITypeMapper<Source, RootDestination>)configured)
                    .Create(source, context);
            var mapping =
                ((ITypeMapper<Source, MappingDestination>)configured)
                    .Create(source, context);
            var library =
                ((ITypeMapper<Source, LibraryDestination>)
                    new LibraryMapper()).Create(source, context);

            if (root.Kind != "parameterless" ||
                mapping.Kind != "largest:31:default" ||
                library.Kind != "unambiguous:17")
            {
                throw new InvalidOperationException(
                    "ConstructorSelection precedence was not preserved.");
            }
        }
    }
}
