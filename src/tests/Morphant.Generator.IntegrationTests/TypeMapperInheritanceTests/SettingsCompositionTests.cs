using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class SettingsCompositionTests
{
    [Test]
    public void Resolves_all_included_pair_settings_before_mapper_roots()
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
    public class Entity
    {
        public int Id { get; init; }

        public int Value { get; init; }
    }

    public class Animal : Entity
    {
    }

    public sealed class Dog : Animal
    {
    }

    public class EntityDto
    {
        public int Value { get; set; }
    }

    public class AnimalDto : EntityDto
    {
    }

    public sealed class DogDto : AnimalDto
    {
        public DogDto()
        {
            Kind = "parameterless";
        }

        public DogDto(int id, string label = "largest")
        {
            Kind = label + ":" + id;
        }

        public string Kind { get; }
    }

    public sealed class RootSource
    {
        public int Value { get; init; }
    }

    public sealed class RootDestination
    {
        public int Value { get; set; }
    }

    public abstract class FarMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MappingMode(MappingMode.Create);
            builder.NullSourceHandling(NullSourceHandling.Throw);
            builder.NullDestinationHandling(NullDestinationHandling.Create);
            builder.ConstructorSelection(ConstructorSelection.Largest);
            builder.MemberSelection(MemberSelection.Explicit);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Strict);

            builder.Map<Entity, EntityDto>(MappingMode.CreateAndUpdate)
                .NullSourceHandling(NullSourceHandling.ReturnNull)
                .NullDestinationHandling(NullDestinationHandling.Create)
                .ConstructorSelection(ConstructorSelection.Largest)
                .MemberSelection(MemberSelection.Auto)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination);
        }
    }

    public abstract class NearMapper : FarMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);

            builder.MappingMode(MappingMode.Update);
            builder.NullSourceHandling(
                NullSourceHandling.ReturnDestination);
            builder.NullDestinationHandling(
                NullDestinationHandling.Throw);
            builder.ConstructorSelection(
                ConstructorSelection.Parameterless);
            builder.MemberSelection(MemberSelection.Auto);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Source);

            builder.Map<Animal, AnimalDto>()
                .IncludeBase<Entity, EntityDto>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .NullSourceHandling(NullSourceHandling.Default)
                .ConstructorSelection(ConstructorSelection.Explicit)
                .ConstructorSelection(ConstructorSelection.Default)
                .MemberSelection(MemberSelection.Explicit)
                .MemberSelection(MemberSelection.Default)
                .UnmappedMemberValidation(UnmappedMemberValidation.None)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Default);
        }
    }

    [MorphantMapper]
    public partial class DogMapper : NearMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);

            builder.MappingMode(MappingMode.Create);
            builder.MappingMode(MappingMode.Default);
            builder.NullSourceHandling(NullSourceHandling.Throw);
            builder.NullSourceHandling(NullSourceHandling.Default);
            builder.NullDestinationHandling(
                NullDestinationHandling.Create);
            builder.NullDestinationHandling(
                NullDestinationHandling.Default);
            builder.ConstructorSelection(ConstructorSelection.Explicit);
            builder.ConstructorSelection(ConstructorSelection.Default);
            builder.MemberSelection(MemberSelection.Explicit);
            builder.MemberSelection(MemberSelection.Default);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.None);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Default);

            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
            builder.Map<RootSource, RootDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DogMapper();
            var included = (ITypeMapper<Dog, DogDto>)mapper;
            var source = new Dog { Id = 17, Value = 31 };
            var created = included.Create(source, default);
            var recreated = included.Update(source, null, default);

            if (created.Kind != "largest:17" ||
                created.Value != 31 ||
                recreated.Kind != "largest:17" ||
                included.Create(null, default) is not null)
            {
                throw new InvalidOperationException(
                    "Included settings did not outrank mapper roots.");
            }

            var root =
                (ITypeMapper<RootSource, RootDestination>)mapper;
            var previous = new RootDestination { Value = 7 };

            if (!ReferenceEquals(
                    previous,
                    root.Update(null, previous, default)))
            {
                throw new InvalidOperationException(
                    "Default did not continue to the nearest base root.");
            }

            ExpectNotSupported(() =>
                root.Create(new RootSource { Value = 17 }, default));
            ExpectArgumentNull(() =>
                root.Update(
                    new RootSource { Value = 17 },
                    null,
                    default));
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
                "Connected root MappingMode was not applied.");
        }

        private static void ExpectArgumentNull(Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentNullException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Connected root NullDestinationHandling was not applied.");
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
