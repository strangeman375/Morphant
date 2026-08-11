// Compiled integration scenario: InheritanceDiagnosticsTests::Invalid_IncludeBase_edges_reject_only_dependent_pairs
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0025, MORPH0026, MORPH0027

using Morphant;
using Morphant.Exceptions;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InheritancePairRecovery_7c0f0025
{
    public class Animal
    {
    }

    public sealed class Dog : Animal
    {
    }

    public sealed class Cat : Animal
    {
    }

    public sealed class UnrelatedSource
    {
    }

    public class AnimalDto
    {
    }

    public sealed class DogDto : AnimalDto
    {
    }

    public sealed class CatDto : AnimalDto
    {
    }

    public sealed class UnrelatedDestination
    {
    }

    public sealed class MissingSource
    {
    }

    public sealed class MissingDestination
    {
    }

    public sealed class ValidSource
    {
        public int Value { get; init; }
    }

    public sealed class ValidDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class EdgeMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>();

            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .IncludeBase<Animal, AnimalDto>();

            builder.Map<Cat, CatDto>(MappingMode.Create)
                .IncludeBase<MissingSource, MissingDestination>();

            builder.Map<UnrelatedSource, UnrelatedDestination>(
                    MappingMode.Update)
                .IncludeBase<Animal, AnimalDto>();

            builder.Map<ValidSource, ValidDestination>();
        }
    }

    public abstract class InvalidBaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .IncludeBase<MissingSource, MissingDestination>();
    }

    [MorphantMapper]
    public partial class TransitiveMapper : InvalidBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new EdgeMapper();

            VerifyPair<Dog, DogDto>(mapper, new Dog(), new DogDto());
            VerifyPair<Cat, CatDto>(mapper, new Cat(), new CatDto());
            VerifyPair<UnrelatedSource, UnrelatedDestination>(
                mapper,
                new UnrelatedSource(),
                new UnrelatedDestination());
            VerifyPair<Dog, DogDto>(
                new TransitiveMapper(),
                new Dog(),
                new DogDto());

            var valid =
                ((ITypeMapper<ValidSource, ValidDestination>)mapper)
                    .Create(new ValidSource { Value = 17 }, default);

            if (valid.Value != 17)
            {
                throw new InvalidOperationException(
                    "An independent mapping pair did not execute.");
            }
        }

        private static void VerifyPair<TSource, TDestination>(
            object mapper,
            TSource source,
            TDestination destination)
        {
            var typed = (ITypeMapper<TSource, TDestination>)mapper;

            ExpectConfigurationFailure(() =>
                typed.Create(source, default));
            ExpectConfigurationFailure(() =>
                typed.Update(source, destination, default));
        }

        private static void ExpectConfigurationFailure(Action action)
        {
            try
            {
                action();
            }
            catch (MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An invalid IncludeBase edge was executed.");
        }
    }
}
