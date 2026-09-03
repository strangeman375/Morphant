// Compiled integration scenario: TypeMapperConstructorSelectionTests/ConfigurationTests::Uses_the_MSBuild_assembly_default_and_pair_override
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0036

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.ConstructorSelection
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; } = -1;
    }

    public sealed class OverrideDestination
    {
        public int Value { get; set; } = -1;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>(
                    global::Morphant.MappingMode.Create)
                .ConstructorSelection(
                    global::Morphant.ConstructorSelection.Default);
            builder.Map<Source, OverrideDestination>(
                    global::Morphant.MappingMode.Create)
                .ConstructorSelection(
                    global::Morphant.ConstructorSelection.Parameterless)
                .MemberSelection(
                    global::Morphant.MemberSelection.Auto);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 53 };
            var configured =
                (ITypeMapper<Source, Destination>)mapper;
            var overridden =
                (ITypeMapper<Source, OverrideDestination>)mapper;

            ExpectAssemblyDefaultToRejectAutomaticConstruction(
                configured,
                source);

            var result = overridden.Create(source);

            if (result.Value != 53)
            {
                throw new InvalidOperationException(
                    "The pair ConstructorSelection did not override the " +
                    "assembly default.");
            }
        }

        private static void ExpectAssemblyDefaultToRejectAutomaticConstruction(
            ITypeMapper<Source, Destination> mapper,
            Source source)
        {
            try
            {
                mapper.Create(source);
            }
            catch (MappingConfigurationException exception)
                when (exception.Operation == MappingOperation.Create &&
                      exception.Reason.Contains(
                          "select a constructor",
                          StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "The assembly ConstructorSelection did not reject automatic " +
                "construction.");
        }
    }
}
