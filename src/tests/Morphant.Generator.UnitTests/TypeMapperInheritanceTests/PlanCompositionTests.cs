using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class PlanCompositionTests
{
    [Test]
    public void Composes_same_level_pairs_transitively_regardless_of_order()
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
        public string Id { get; init; } = string.Empty;
    }

    public class Animal : Entity
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
        public string Breed { get; init; } = string.Empty;
    }

    public class EntityDto
    {
        public string Id { get; set; } = string.Empty;
    }

    public class AnimalDto : EntityDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
        public string Breed { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Members((source, _) => new()
                {
                    Breed = "dog:" + source.Breed
                });
            builder.Map<Animal, AnimalDto>()
                .IncludeBase<Entity, EntityDto>()
                .Members((source, _) => new()
                {
                    Name = "animal:" + source.Name
                });
            builder.Map<Entity, EntityDto>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Id = "entity:" + source.Id
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Dog, DogDto>)new TestMapper();
            var result = mapper.Create(
                new Dog
                {
                    Id = "17",
                    Name = "name",
                    Breed = "breed"
                },
                default);

            if (result.Id != "entity:17" ||
                result.Name != "animal:name" ||
                result.Breed != "dog:breed")
            {
                throw new InvalidOperationException(
                    "Same-level IncludeBase composition was incorrect.");
            }

            try
            {
                mapper.Create(null, default);
            }
            catch (ArgumentNullException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Same-level pair settings were not inherited.");
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Prefers_a_same_level_pair_to_a_connected_base_pair()
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
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
    }

    public class AnimalDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = "base:" + source.Name
                });
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = "current:" + source.Name
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog { Name = "name" },
                    default);

            if (result.Name != "current:name")
            {
                throw new InvalidOperationException(
                    "The connected base pair outranked the same-level pair.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Merges_included_Members_by_destination_member_and_rebuilds_dependencies()
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
        public string Name { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public string Kept { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
        public string Breed { get; init; } = string.Empty;
    }

    public class AnimalDto
    {
        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Kept { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
        public string Breed { get; set; } = string.Empty;

        public string Extra { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected static string ObsoleteName(Animal source) =>
            throw new InvalidOperationException(
                "An overridden dependency was evaluated.");

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = ObsoleteName(source),
                    Code = "base:" + source.Code,
                    Kept = "base:" + source.Kept
                });
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .Members((source, _, result) => new()
                {
                    Name = "dog:" + source.Name,
                    Code = Ignore(),
                    Breed = source.Breed,
                    Extra = result.Name + ":extra"
                })
                .IncludeBase<Animal, AnimalDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Dog, DogDto>)new DogMapper();
            var result = mapper.Create(
                new Dog
                {
                    Name = "name",
                    Code = "code",
                    Kept = "kept",
                    Breed = "breed"
                },
                default);

            if (result.Name != "dog:name" ||
                result.Code != string.Empty ||
                result.Kept != "base:kept" ||
                result.Breed != "breed" ||
                result.Extra != "dog:name:extra")
            {
                throw new InvalidOperationException(
                    "The effective Members plan was composed incorrectly.");
            }

            try
            {
                mapper.Create(null, default);
            }
            catch (ArgumentNullException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Included pair settings were not inherited.");
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Accepts_interface_base_pair_assignability()
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
    public interface IAnimal
    {
        string Name { get; }
    }

    public sealed class Dog : IAnimal
    {
        public string Name { get; init; } = string.Empty;
    }

    public interface IAnimalDto
    {
        string Name { get; set; }
    }

    public sealed class DogDto : IAnimalDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IAnimal, IAnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = "base:" + source.Name
                });
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<IAnimal, IAnimalDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog { Name = "name" },
                    default);

            if (result.Name != "base:name")
            {
                throw new InvalidOperationException(
                    "Interface base-pair composition was not applied.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Does_not_include_Construct_and_recomputes_derived_construction()
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
        public int Seed { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
        public string Breed { get; init; } = string.Empty;
    }

    public class AnimalDto
    {
        public AnimalDto(int seed) => Seed = seed;

        public int Seed { get; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
        public DogDto(int seed) : base(seed)
        {
        }

        public string Breed { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper
    {
        private static AnimalDto CreateBase(int seed) => new(seed + 1000);

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .Construct(source => new(ByFactory(() =>
                    CreateBase(source.Seed))))
                .Members((source, _) => new()
                {
                    Name = "base:" + source.Name
                });
    }

    [MorphantMapper]
    public partial class DogMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Members((source, _) => new()
                {
                    Breed = "dog:" + source.Breed
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog
                    {
                        Seed = 17,
                        Name = "name",
                        Breed = "breed"
                    },
                    default);

            if (result.Seed != 17 ||
                result.Name != "base:name" ||
                result.Breed != "dog:breed")
            {
                throw new InvalidOperationException(
                    "Construct was included or derived construction was not recomputed.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Uses_the_nearest_explicit_base_pair_and_composes_transitively()
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
        public string Id { get; init; } = string.Empty;
    }

    public class Animal : Entity
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
        public string Breed { get; init; } = string.Empty;
    }

    public class EntityDto
    {
        public string Id { get; set; } = string.Empty;
    }

    public class AnimalDto : EntityDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
        public string Breed { get; set; } = string.Empty;
    }

    public abstract class FarMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Entity, EntityDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Id = "entity:" + source.Id
                });
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = "far:" + source.Name
                });
        }
    }

    public abstract class NearMapper : FarMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Animal, AnimalDto>()
                .IncludeBase<Entity, EntityDto>()
                .Members((source, _) => new()
                {
                    Name = "near:" + source.Name
                });
        }
    }

    [MorphantMapper]
    public partial class DogMapper : NearMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Members((source, _) => new()
                {
                    Breed = "dog:" + source.Breed
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog
                    {
                        Id = "17",
                        Name = "name",
                        Breed = "breed"
                    },
                    default);

            if (result.Id != "entity:17" ||
                result.Name != "near:name" ||
                result.Breed != "dog:breed")
            {
                throw new InvalidOperationException(
                    "The nearest or transitive base pair was not composed.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Does_not_include_Convert_and_local_Convert_replaces_included_Members()
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
        public string Kind { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
    }

    public class Vehicle
    {
        public int Value { get; init; }
    }

    public sealed class Car : Vehicle
    {
    }

    public class VehicleDto
    {
        public string Kind { get; set; } = string.Empty;
    }

    public sealed class CarDto : VehicleDto
    {
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Convert((source, _, _) => new AnimalDto
                {
                    Kind = "animal:" + source!.Value
                });
            builder.Map<Vehicle, VehicleDto>()
                .Members((source, _) => new()
                {
                    Kind = "vehicle:" + source.Value
                });
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Members((source, _) => new()
                {
                    Kind = "dog:" + source.Value
                });
            builder.Map<Car, CarDto>()
                .IncludeBase<Vehicle, VehicleDto>()
                .Convert((source, _, _) => new CarDto
                {
                    Kind = "car:" + source!.Value
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DerivedMapper();
            var dog = ((ITypeMapper<Dog, DogDto>)mapper).Create(
                new Dog { Value = 17 },
                default);
            var car = ((ITypeMapper<Car, CarDto>)mapper).Create(
                new Car { Value = 31 },
                default);

            if (dog.Kind != "dog:17" || car.Kind != "car:31")
            {
                throw new InvalidOperationException(
                    "Convert crossed the IncludeBase boundary.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
