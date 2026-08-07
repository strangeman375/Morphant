using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class InvalidCompositionTests
{
    [Test]
    public void Preserves_invalid_IncludeBase_forms_as_unsupported_states()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
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
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Rejects_duplicate_base_Configure_calls_for_the_mapper()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
    }

    public sealed class LocalSource
    {
    }

    public sealed class LocalDestination
    {
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            base.Configure(builder);
            builder.Map<LocalSource, LocalDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            try
            {
                ((ITypeMapper<LocalSource, LocalDestination>)
                    new DerivedMapper())
                    .Create(new LocalSource(), default);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Duplicate base Configure calls were accepted.");
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Keeps_included_declarative_settings_inactive_for_local_Convert()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public class Animal
    {
        public int Value { get; init; }
    }

    public sealed class Dog : Animal
    {
    }

    public class AnimalDto
    {
        public AnimalDto(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class DogDto : AnimalDto
    {
        public DogDto(int value) : base(value)
        {
        }
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .ConstructorSelection(ConstructorSelection.Explicit)
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Value));
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Convert((source, _, _) =>
                    new DogDto(source?.Value ?? -1));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog { Value = 17 },
                    default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "Included no-effect settings invalidated local Convert.");
            }
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
