#nullable enable
using System;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SettingsInheritance
{
    public sealed class Details { public int Code => 42; }
    public class Entity { public int Value => 10; public Details Details { get; } = new(); }
    public class Animal : Entity { }
    public class Dog : Animal { }
    public sealed class Puppy : Dog { }
    public class EntityDto { public int Value { get; set; } = 5; public int DetailsCode { get; set; } }
    public class AnimalDto : EntityDto { }
    public sealed class DogDto : AnimalDto { }

    public abstract class Far<TMapper> : TypeMapper<TMapper> where TMapper : Far<TMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Entity, EntityDto>()
            .Flattening(Flattening.Auto)
            .UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Throw)
            .MemberSelection(MemberSelection.Auto)
            .Members(source => new() { Value = source.Value + 1 });
    }
    public abstract class Near<TMapper> : Far<TMapper> where TMapper : Near<TMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Animal, AnimalDto>().IncludeBase<Entity, EntityDto>()
                .Flattening(Flattening.None).Flattening(Flattening.Default)
                .UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.UseBaseMapping)
                .UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Default)
                .MemberSelection(MemberSelection.Explicit).MemberSelection(MemberSelection.Default);
        }
    }
    [MorphantMapper]
    public partial class InheritedMapper : Near<InheritedMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>().IncludeBase<Animal, AnimalDto>()
                .Members(source => new() { Value = source.Value + 3 });
            builder.Flattening(Flattening.None);
            builder.UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.UseBaseMapping);
            builder.MemberSelection(MemberSelection.Explicit);
        }
    }
    [MorphantMapper]
    public partial class LocalMapper : Near<LocalMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>().IncludeBase<Animal, AnimalDto>()
                .Flattening(Flattening.None)
                .UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.UseBaseMapping)
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new() { Value = Auto() });
        }
    }
    [MorphantMapper]
    public partial class IgnoredMapper : Near<IgnoredMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.MemberSelection(MemberSelection.Explicit);
            builder.Map<Dog, DogDto>().IncludeBase<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new() { Value = Ignore(), DetailsCode = Auto() });
        }
    }
    [MorphantMapper]
    public partial class BeforeMapper : TypeMapper<BeforeMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MemberSelection(MemberSelection.Explicit);
            builder.Map<Dog, DogDto>().MemberSelection(MemberSelection.Auto)
                .MemberSelection(MemberSelection.Default);
        }
    }
    [MorphantMapper]
    public partial class AfterMapper : TypeMapper<AfterMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Dog, DogDto>().MemberSelection(MemberSelection.Auto)
                .MemberSelection(MemberSelection.Default);
            builder.MemberSelection(MemberSelection.Explicit);
        }
    }
    public static class Scenario
    {
        public static void VerifyIncludedSettings(bool update)
        {
            ITypeMapper<Dog, DogDto> mapper = new InheritedMapper();
            var previous = new DogDto { Value = 70 };
            var result = update ? mapper.Update(new Dog(), previous) : mapper.Create(new Dog());
            if (result.Value != 13 || result.DetailsCode != 42)
                throw new InvalidOperationException("Local Members must override the inherited expression; included settings must outrank mapper defaults.");
            if (update && !ReferenceEquals(previous, result))
                throw new InvalidOperationException("Update must preserve the supplied destination.");
        }

        public static void VerifyIncludedUnknownDerivedHandling()
        {
            ITypeMapper<Dog, DogDto> mapper = new InheritedMapper();
            try { mapper.Create(new Puppy()); }
            catch (UnmatchedPolymorphicMappingException) { return; }
            throw new InvalidOperationException("The included Throw setting must outrank the mapper's UseBaseMapping setting.");
        }

        public static void VerifyLocalSettings()
        {
            ITypeMapper<Dog, DogDto> mapper = new LocalMapper();
            var result = mapper.Create(new Puppy());
            if (result.Value != 10 || result.DetailsCode != 0)
                throw new InvalidOperationException("Local settings must permit base fallback and disable unrequested flattening; Auto must replace the inherited expression.");
        }

        public static void VerifyIgnoreAndAuto(bool update)
        {
            ITypeMapper<Dog, DogDto> mapper = new IgnoredMapper();
            var result = update ? mapper.Update(new Dog(), new DogDto { Value = 70 }) : mapper.Create(new Dog());
            if (result.Value != (update ? 70 : 5) || result.DetailsCode != 42)
                throw new InvalidOperationException("Ignore must preserve the current value; Auto must use inherited flattening even with explicit member selection.");
        }

        public static void VerifyRootOrder(bool settingsAfterRegistration)
        {
            ITypeMapper<Dog, DogDto> mapper = settingsAfterRegistration ? new AfterMapper() : new BeforeMapper();
            if (mapper.Create(new Dog()).Value != 5)
                throw new InvalidOperationException("Final pair Default must discard earlier Auto and inherit Explicit regardless of root call order.");
        }
    }
}
