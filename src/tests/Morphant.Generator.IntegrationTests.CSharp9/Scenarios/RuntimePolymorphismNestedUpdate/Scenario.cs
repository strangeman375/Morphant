#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismNestedUpdate
{
    public class Animal { }
    public sealed class Dog : Animal
    {
        public string Name { get; init; } = string.Empty;
    }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto
    {
        public string Name { get; set; } = string.Empty;
    }
    public sealed class Holder
    {
        public Animal? Animal { get; init; }
    }
    public sealed class ReplacementHolder
    {
        public Animal? Animal { get; init; }
        public bool Empty { get; init; }
    }
    public sealed class HolderDto
    {
        public AnimalDto? Animal { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal?, AnimalDto?>()
                .NullSourceHandling(NullSourceHandling.ReturnDestination)
                .ForDerived<Dog, DogDto>();
            builder.Map<Dog, DogDto>()
                .NullDestinationHandling(NullDestinationHandling.Throw)
                .Members(source => new() { Name = source.Name });
            builder.Map<Holder, HolderDto>()
                .Members(source => new() { Animal = Map<AnimalDto?>(source.Animal) });
            builder.Map<ReplacementHolder, HolderDto>()
                .ResolveUsing(source => new HolderDto
                {
                    Animal = source!.Empty ? null : new DogDto { Name = "replacement" }
                })
                .Members(source => new() { Animal = Map<AnimalDto?>(source.Animal) });
        }
    }

    public static class Scenario
    {
        public static void CreateSelectsDerivedBranch()
        {
            var mapper = (ITypeMapper<Holder, HolderDto>)new TestMapper();
            var created = mapper.Create(new Holder { Animal = new Dog { Name = "created" } });
            if (created.Animal is not DogDto { Name: "created" })
                throw new InvalidOperationException("Nested Create did not select the derived branch.");
        }

        public static void NullOuterDestinationUsesNestedCreate()
        {
            var mapper = (ITypeMapper<Holder, HolderDto>)new TestMapper();
            var updated = mapper.Update(new Holder { Animal = new Dog { Name = "created" } }, null);
            if (updated.Animal is not DogDto { Name: "created" })
                throw new InvalidOperationException("Update without an outer destination did not use nested Create.");
        }

        public static void UpdatePreservesBothDestinations()
        {
            var mapper = (ITypeMapper<Holder, HolderDto>)new TestMapper();
            var dog = new DogDto { Name = "previous" };
            var previous = new HolderDto { Animal = dog };
            var updated = mapper.Update(new Holder { Animal = new Dog { Name = "updated" } }, previous);
            if (!ReferenceEquals(updated, previous) || !ReferenceEquals(updated.Animal, dog) || dog.Name != "updated")
                throw new InvalidOperationException("Nested derived Update replaced an existing destination.");
        }

        public static void MissingNestedDestinationAppliesDerivedPolicy()
        {
            var mapper = (ITypeMapper<Holder, HolderDto>)new TestMapper();
            var previous = new HolderDto();
            ExpectNullDestination(() => mapper.Update(new Holder { Animal = new Dog() }, previous));
            if (previous.Animal != null)
                throw new InvalidOperationException("A failed nested Update assigned a destination.");
        }

        public static void WrongNestedDestinationReportsSelectedBranch()
        {
            var mapper = (ITypeMapper<Holder, HolderDto>)new TestMapper();
            var animal = new AnimalDto();
            var previous = new HolderDto { Animal = animal };
            try
            {
                mapper.Update(new Holder { Animal = new Dog() }, previous);
            }
            catch (PolymorphicDestinationTypeMismatchException exception)
            {
                if (exception.Operation != MappingOperation.Update ||
                    exception.SourceType != typeof(Animal) || exception.DestinationType != typeof(AnimalDto) ||
                    exception.BranchSourceType != typeof(Dog) || exception.ExpectedDestinationType != typeof(DogDto) ||
                    exception.ActualDestinationType != typeof(AnimalDto) || !ReferenceEquals(previous.Animal, animal))
                    throw new InvalidOperationException("Nested mismatch reported the wrong pair or changed the member.", exception);
                return;
            }
            throw new InvalidOperationException("An incompatible nested destination was accepted.");
        }

        public static void NullSourcePreservesNestedDestination(bool hasDestination)
        {
            var mapper = (ITypeMapper<Holder, HolderDto>)new TestMapper();
            var dog = hasDestination ? new DogDto { Name = "previous" } : null;
            var previous = new HolderDto { Animal = dog };
            var updated = mapper.Update(new Holder(), previous);
            if (!ReferenceEquals(updated, previous) || !ReferenceEquals(updated.Animal, dog) ||
                (dog != null && dog.Name != "previous"))
                throw new InvalidOperationException("Null-source handling did not preserve the nested destination.");
        }

        public static void ReplacementUsesItsOwnNestedDestination()
        {
            var mapper = (ITypeMapper<ReplacementHolder, HolderDto>)new TestMapper();
            var animal = new AnimalDto();
            var previous = new HolderDto { Animal = animal };
            var updated = mapper.Update(new ReplacementHolder { Animal = new Dog { Name = "updated" } }, previous);
            if (ReferenceEquals(updated, previous) || updated.Animal is not DogDto { Name: "updated" } ||
                !ReferenceEquals(previous.Animal, animal))
                throw new InvalidOperationException("Nested Update did not use the replacement holder's destination.");
        }

        public static void EmptyReplacementDoesNotFallBackToPreviousMember()
        {
            var mapper = (ITypeMapper<ReplacementHolder, HolderDto>)new TestMapper();
            var dog = new DogDto { Name = "previous" };
            var previous = new HolderDto { Animal = dog };
            ExpectNullDestination(() => mapper.Update(new ReplacementHolder
            {
                Animal = new Dog { Name = "updated" },
                Empty = true
            }, previous));
            if (!ReferenceEquals(previous.Animal, dog) || dog.Name != "previous")
                throw new InvalidOperationException("A failed replacement mutated the previous nested destination.");
        }

        private static void ExpectNullDestination(Action action)
        {
            try
            {
                action();
            }
            catch (NullDestinationException exception)
            {
                if (exception.Operation != MappingOperation.Update ||
                    exception.SourceType != typeof(Dog) || exception.DestinationType != typeof(DogDto))
                    throw new InvalidOperationException("Nested null failure belongs to the wrong operation or pair.", exception);
                return;
            }
            throw new InvalidOperationException("The derived null-destination policy was bypassed.");
        }
    }
}
