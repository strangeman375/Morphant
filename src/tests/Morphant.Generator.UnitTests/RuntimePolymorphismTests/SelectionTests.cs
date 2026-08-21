using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class SelectionTests
{
    [Test]
    public void Selects_the_unique_most_specific_branch_and_proxy_ancestor()
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
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public interface IServiceDog : IDog { }
    public sealed class Dog : IDog { }
    public sealed class ProxyDog : IDog { }
    public sealed class ServiceDog : IServiceDog { }
    public sealed class Cat : IAnimal { }
    public abstract class AbstractAnimal { }
    public sealed class AbstractDog : AbstractAnimal { }
    public sealed class AbstractCat : AbstractAnimal { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .ForDerived<IServiceDog, string>()
                .Convert(_ => "base");
            builder.Map<IDog, string>()
                .Convert(_ => "dog");
            builder.Map<IServiceDog, string>()
                .Convert(_ => "service");
            builder.Map<AbstractAnimal, object>()
                .ForDerived<AbstractDog, string>()
                .Convert(_ => "abstract-base");
            builder.Map<AbstractDog, string>()
                .Convert(_ => "abstract-dog");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<IAnimal, object>)new TestMapper();
            var abstractMapper =
                (ITypeMapper<AbstractAnimal, object>)new TestMapper();

            if (!Equals(mapper.Create(new Dog()), "dog") ||
                !Equals(mapper.Create(new ProxyDog()), "dog") ||
                !Equals(mapper.Create(new ServiceDog()), "service") ||
                !Equals(mapper.Create(new Cat()), "base") ||
                !Equals(
                    abstractMapper.Create(new AbstractDog()),
                    "abstract-dog") ||
                !Equals(
                    abstractMapper.Create(new AbstractCat()),
                    "abstract-base"))
            {
                throw new InvalidOperationException(
                    "Most-specific polymorphic selection is incorrect.");
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismMostSpecific");
    }

    [Test]
    public void Reports_all_maximal_incomparable_interface_branches()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Linq;
using Morphant;
using Morphant.Exceptions;

namespace TestCase
{
    public interface IRoot { }
    public interface IKnown : IRoot { }
    public interface IWorking : IKnown { }
    public interface IPet : IKnown { }
    public interface IWorkingPet : IWorking, IPet { }
    public sealed class WorkingPet : IWorking, IPet { }
    public sealed class SpecificWorkingPet : IWorkingPet { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IRoot, object>()
                .ForDerived<IKnown, object>()
                .ForDerived<IWorking, string>()
                .ForDerived<IPet, string>()
                .ForDerived<IWorkingPet, string>()
                .Convert(_ => "base");
            builder.Map<IKnown, object>()
                .Convert(_ => "known");
            builder.Map<IWorking, string>()
                .Convert(_ => "working");
            builder.Map<IPet, string>()
                .Convert(_ => "pet");
            builder.Map<IWorkingPet, string>()
                .Convert(_ => "specific");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<IRoot, object>)new TestMapper();

            if (!Equals(
                    mapper.Create(new SpecificWorkingPet()),
                    "specific"))
            {
                throw new InvalidOperationException(
                    "A unique more-specific interface was not selected.");
            }

            try
            {
                mapper.Create(new WorkingPet());
                throw new InvalidOperationException(
                    "An ambiguous runtime source was accepted.");
            }
            catch (AmbiguousPolymorphicMappingException exception)
            {
                if (exception.SourceType != typeof(IRoot) ||
                    exception.DestinationType != typeof(object) ||
                    exception.ActualSourceType != typeof(WorkingPet) ||
                    !exception.MatchingSourceTypes.SequenceEqual(
                        new[] { typeof(IWorking), typeof(IPet) }) ||
                    !exception.MatchingDestinationTypes.SequenceEqual(
                        new[] { typeof(string), typeof(string) }))
                {
                    throw new InvalidOperationException(
                        "The ambiguity exception lost maximal branches.");
                }
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismAmbiguity");
    }

    [Test]
    public void Applies_throw_only_to_non_null_unknown_derived_sources()
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
    public interface IRoot { }
    public interface IKnown : IRoot { }
    public sealed class Known : IKnown { }
    public sealed class Unknown : IRoot { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IRoot, object>()
                .ForDerived<IKnown, string>()
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw)
                .Convert(source => source is null ? "null" : "base");
            builder.Map<IKnown, string>()
                .Convert(_ => "known");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<IRoot, object>)new TestMapper();

            if (!Equals(mapper.Create(new Known()), "known") ||
                !Equals(mapper.Create(null), "null"))
            {
                throw new InvalidOperationException(
                    "Known or null dispatch is incorrect.");
            }

            try
            {
                mapper.Create(new Unknown());
                throw new InvalidOperationException(
                    "An unknown derived source was accepted.");
            }
            catch (UnmatchedPolymorphicMappingException exception)
            {
                if (exception.SourceType != typeof(IRoot) ||
                    exception.DestinationType != typeof(object) ||
                    exception.ActualSourceType != typeof(Unknown))
                {
                    throw new InvalidOperationException(
                        "The unmatched exception lost runtime details.");
                }
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismUnknown");
    }

    [Test]
    public void Throw_rejects_unknown_runtime_types_even_without_links()
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
    public sealed class Dog : IAnimal { }
    public class ConcreteAnimal { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IAnimal, object>()
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw)
                .Convert(_ => "interface-base");
            builder.Map<ConcreteAnimal, object>()
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw)
                .Convert(_ => "concrete-base");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            if (!Equals(
                    ((ITypeMapper<ConcreteAnimal, object>)mapper)
                        .Create(new ConcreteAnimal()),
                    "concrete-base"))
            {
                throw new InvalidOperationException(
                    "An exact concrete source skipped the base plan.");
            }

            try
            {
                ((ITypeMapper<IAnimal, object>)mapper).Create(new Dog());
                throw new InvalidOperationException(
                    "An empty strict dispatch table accepted a subtype.");
            }
            catch (UnmatchedPolymorphicMappingException)
            {
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismEmptyStrictTable");
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
