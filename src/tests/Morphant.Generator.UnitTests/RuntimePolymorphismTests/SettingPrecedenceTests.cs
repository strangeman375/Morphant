using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class SettingPrecedenceTests
{
    [Test]
    public void Resolves_assembly_mapper_and_pair_precedence()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace TestCase
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public sealed class Unknown : IAnimal { }

    [MorphantMapper]
    public partial class AssemblyMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .Convert(_ => "base");
    }

    [MorphantMapper]
    public partial class RootMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.UnknownDerivedTypeHandling(
                UnknownDerivedTypeHandling.UseBaseMapping);
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .Convert(_ => "base");
        }
    }

    [MorphantMapper]
    public partial class PairMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.UnknownDerivedTypeHandling(
                UnknownDerivedTypeHandling.UseBaseMapping);
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw)
                .Convert(_ => "base");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            AssertThrows(new AssemblyMapper());
            AssertUsesBase(new RootMapper());
            AssertThrows(new PairMapper());
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
""";

        var result = GeneratorTestDriver.Run(
            "RuntimePolymorphismSettingPrecedence",
            source,
            LanguageVersion.CSharp9,
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.MorphantUnknownDerivedTypeHandling"] =
                    " throw "
            });

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });

        GeneratedCodeExecution.AssertScenario(
            "runtime polymorphism setting precedence",
            result.OutputCompilation,
            "TestCase.Scenario");
    }
}
