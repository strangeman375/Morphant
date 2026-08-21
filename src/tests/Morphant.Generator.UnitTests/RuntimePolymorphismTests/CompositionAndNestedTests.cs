using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class CompositionAndNestedTests
{
    [Test]
    public void Dispatches_transitively_at_root_and_explicit_nested_calls()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public class Animal { }
    public class Dog : Animal { }
    public sealed class ServiceDog : Dog { }
    public class AnimalDto { }
    public class DogDto : AnimalDto { }
    public sealed class ServiceDogDto : DogDto { }
    public sealed class Holder
    {
        public Animal Animal { get; init; } = null!;
    }
    public sealed class HolderDto
    {
        public AnimalDto Animal { get; set; } = null!;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .Convert(_ => new AnimalDto());
            builder.Map<Dog, DogDto>()
                .ForDerived<ServiceDog, ServiceDogDto>()
                .Convert(_ => new DogDto());
            builder.Map<ServiceDog, ServiceDogDto>()
                .Convert(_ => new ServiceDogDto());
            builder.Map<Holder, HolderDto>()
                .Members(source => new()
                {
                    Animal = Map<AnimalDto>(source.Animal)
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var animalMapper =
                (ITypeMapper<Animal, AnimalDto>)mapper;
            var holderMapper =
                (ITypeMapper<Holder, HolderDto>)mapper;

            if (animalMapper.Create(new ServiceDog()) is not
                    ServiceDogDto ||
                holderMapper.Create(new Holder
                {
                    Animal = new ServiceDog()
                }).Animal is not ServiceDogDto)
            {
                throw new InvalidOperationException(
                    "Transitive or nested dispatch stopped early.");
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismNested");
    }

    [Test]
    public void Runs_include_members_and_flattening_in_the_selected_plan()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public class Animal { }
    public sealed class Dog : Animal
    {
        public DogDetails Details { get; init; } = new();
    }
    public sealed class DogDetails
    {
        public string Name { get; init; } = string.Empty;
        public Customer Customer { get; init; } = new();
    }
    public sealed class Customer
    {
        public string Name { get; init; } = string.Empty;
    }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto
    {
        public string Name { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .Convert(_ => new AnimalDto());
            builder.Map<Dog, DogDto>()
                .IncludeMembers(source => source.Details);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Animal, AnimalDto>)new TestMapper();
            var result = mapper.Create(new Dog
            {
                Details = new DogDetails
                {
                    Name = "Ada",
                    Customer = new Customer { Name = "Lovelace" }
                }
            });

            if (result is not DogDto
                {
                    Name: "Ada",
                    CustomerName: "Lovelace"
                })
            {
                throw new InvalidOperationException(
                    "The selected derived plan skipped configuration rules.");
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismDerivedRules");
    }

    [Test]
    public void IncludeBase_reuses_rules_without_importing_dispatch_links()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public class Animal
    {
        public string Name { get; init; } = string.Empty;
    }
    public class Dog : Animal { }
    public sealed class ServiceDog : Dog { }
    public class AnimalDto
    {
        public string Name { get; set; } = string.Empty;
    }
    public class DogDto : AnimalDto { }
    public sealed class ServiceDogDto : DogDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<ServiceDog, ServiceDogDto>()
                .Members(source => new() { Name = source.Name });
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
            builder.Map<ServiceDog, ServiceDogDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var serviceDog = new ServiceDog { Name = "Ada" };
            var throughBase =
                ((ITypeMapper<Animal, AnimalDto>)mapper)
                    .Create(serviceDog);
            var throughDog =
                ((ITypeMapper<Dog, DogDto>)mapper)
                    .Create(serviceDog);

            if (throughBase is not ServiceDogDto { Name: "Ada" } ||
                throughDog.GetType() != typeof(DogDto) ||
                throughDog.Name != "Ada")
            {
                throw new InvalidOperationException(
                    "IncludeBase and ForDerived were coupled.");
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismIncludeBase");
    }

    [Test]
    public void Exact_IncludeBase_does_not_import_dispatch_links()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public class Animal { }
    public sealed class Dog : Animal { }
    public class AnimalDto { }
    public sealed class DogDto : AnimalDto { }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, AnimalDto>()
                .ForDerived<Dog, DogDto>()
                .Convert(_ => new AnimalDto());
            builder.Map<Dog, DogDto>()
                .Convert(_ => new DogDto());
        }
    }

    [MorphantMapper]
    public partial class TestMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Animal, AnimalDto>()
                .IncludeBase<Animal, AnimalDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Animal, AnimalDto>)new TestMapper())
                    .Create(new Dog());

            if (result.GetType() != typeof(AnimalDto))
            {
                throw new InvalidOperationException(
                    "Exact IncludeBase imported a ForDerived link.");
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismExactIncludeBase");
    }

    [Test]
    public void Supports_generic_mapper_substitution()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public class Animal<T> { }
    public sealed class Dog<T> : Animal<T>
    {
        public T Value { get; init; } = default!;
    }
    public class AnimalDto<T> { }
    public sealed class DogDto<T> : AnimalDto<T>
    {
        public T Value { get; init; } = default!;
    }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal<T>, AnimalDto<T>>()
                .ForDerived<Dog<T>, DogDto<T>>()
                .Convert(_ => new AnimalDto<T>());
            builder.Map<Dog<T>, DogDto<T>>()
                .Convert(source => new DogDto<T>
                {
                    Value = source!.Value
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Animal<string>, AnimalDto<string>>)
                new TestMapper<string>();

            if (mapper.Create(new Dog<string>
                {
                    Value = "generic"
                }) is not DogDto<string> { Value: "generic" })
            {
                throw new InvalidOperationException(
                    "Generic ForDerived substitution failed.");
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismGeneric");
    }

    private static void RunScenario(string source, string assemblyName)
    {
        var result = GeneratorTestDriver.Run(
            assemblyName,
            source,
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });

        GeneratedCodeExecution.AssertScenario(
            assemblyName,
            result.OutputCompilation,
            "TestCase.Scenario");
    }
}
