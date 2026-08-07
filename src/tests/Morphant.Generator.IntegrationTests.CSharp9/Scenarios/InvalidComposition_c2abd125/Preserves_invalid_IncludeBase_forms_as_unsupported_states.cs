// Compiled integration scenario: TypeMapperInheritanceTests/InvalidCompositionTests::Preserves_invalid_IncludeBase_forms_as_unsupported_states
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidComposition_c2abd125
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

    public class MissingBaseDto
    {
    }

    public sealed class CatDto : MissingBaseDto
    {
    }

    public sealed class UnrelatedDestination
    {
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>();
    }

    [MorphantMapper]
    public partial class NoChainMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
    }

    [MorphantMapper]
    public partial class MissingPairMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Cat, CatDto>()
                .IncludeBase<Animal, MissingBaseDto>();
        }
    }

    [MorphantMapper]
    public partial class IncompatibleSourceMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<UnrelatedSource, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
        }
    }

    [MorphantMapper]
    public partial class IncompatibleDestinationMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, UnrelatedDestination>()
                .IncludeBase<Animal, AnimalDto>();
        }
    }

    [MorphantMapper]
    public partial class DuplicateIncludeMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .IncludeBase<Animal, AnimalDto>();
        }
    }

    [MorphantMapper]
    public partial class SelfReferenceMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Dog, DogDto>()
                .IncludeBase<Dog, DogDto>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            ExpectNotSupported(() =>
                ((ITypeMapper<Dog, DogDto>)new NoChainMapper())
                    .Create(new Dog(), default));
            ExpectNotSupported(() =>
                ((ITypeMapper<Cat, CatDto>)new MissingPairMapper())
                    .Create(new Cat(), default));
            ExpectNotSupported(() =>
                ((ITypeMapper<UnrelatedSource, DogDto>)
                    new IncompatibleSourceMapper())
                    .Create(new UnrelatedSource(), default));
            ExpectNotSupported(() =>
                ((ITypeMapper<Dog, UnrelatedDestination>)
                    new IncompatibleDestinationMapper())
                    .Create(new Dog(), default));
            ExpectNotSupported(() =>
                ((ITypeMapper<Dog, DogDto>)new DuplicateIncludeMapper())
                    .Create(new Dog(), default));
            ExpectNotSupported(() =>
                ((ITypeMapper<Dog, DogDto>)new SelfReferenceMapper())
                    .Create(new Dog(), default));
        }

        private static void ExpectNotSupported(Action action)
        {
            try
            {
                action();
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An invalid IncludeBase form was silently accepted.");
        }
    }
}
