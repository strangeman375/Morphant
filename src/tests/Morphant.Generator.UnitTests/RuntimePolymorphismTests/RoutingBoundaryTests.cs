using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class RoutingBoundaryTests
{
    [Test]
    public void Uses_application_lookup_across_mappers_but_keeps_standalone_exact()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Exceptions;

namespace TestCase
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public sealed class Dog : IDog { }

    [MorphantMapper]
    public partial class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .Convert(_ => "base");
    }

    [MorphantMapper]
    public partial class DerivedMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IDog, string>()
                .Convert(_ => "dog");
    }

    public sealed class Provider : IServiceProvider
    {
        private readonly BaseMapper _baseMapper = new();
        private readonly DerivedMapper _derivedMapper = new();

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IAnimal, object>>))
            {
                return new ITypeMapper<IAnimal, object>[]
                {
                    _baseMapper
                };
            }

            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IDog, string>>))
            {
                return new ITypeMapper<IDog, string>[]
                {
                    _derivedMapper
                };
            }

            return null;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var dog = new Dog();
            var application = new Mapper(new Provider());

            if (!Equals(
                    application.Map<IAnimal, object>(dog),
                    "dog"))
            {
                throw new InvalidOperationException(
                    "Application lookup did not reach the derived mapper.");
            }

            var standalone =
                (ITypeMapper<IAnimal, object>)new BaseMapper();

            try
            {
                standalone.Create(dog);
                throw new InvalidOperationException(
                    "Standalone lookup invented a derived registration.");
            }
            catch (MappingNotFoundException exception)
            {
                if (exception.SourceType != typeof(IDog) ||
                    exception.DestinationType != typeof(string))
                {
                    throw new InvalidOperationException(
                        "Standalone lookup reported the wrong exact pair.");
                }
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismMapperBoundary");
    }

    [Test]
    public void Enforces_base_mode_before_dispatch_and_derived_mode_after_match()
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
    public interface IFirst { }
    public interface IFirstDerived : IFirst { }
    public sealed class FirstDerived : IFirstDerived { }
    public interface ISecond { }
    public interface ISecondDerived : ISecond { }
    public sealed class SecondDerived : ISecondDerived { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IFirst, object>(MappingMode.Create)
                .ForDerived<IFirstDerived, string>()
                .Convert(_ => "first-base");
            builder.Map<IFirstDerived, string>()
                .Convert(_ => "first-derived");

            builder.Map<ISecond, object>()
                .ForDerived<ISecondDerived, string>()
                .Convert(_ => "second-base");
            builder.Map<ISecondDerived, string>(MappingMode.Update)
                .Convert(_ => "second-derived");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var first = (ITypeMapper<IFirst, object>)mapper;
            var second = (ITypeMapper<ISecond, object>)mapper;

            try
            {
                first.Update(new FirstDerived(), "previous");
                throw new InvalidOperationException(
                    "Derived dispatch bypassed the base MappingMode.");
            }
            catch (MappingOperationNotSupportedException exception)
            {
                if (exception.SourceType != typeof(IFirst) ||
                    exception.DestinationType != typeof(object) ||
                    exception.EffectiveMappingMode != MappingMode.Create)
                {
                    throw new InvalidOperationException(
                        "The base mode failure is incorrect.");
                }
            }

            try
            {
                second.Create(new SecondDerived());
                throw new InvalidOperationException(
                    "A matched disabled derived Create was accepted.");
            }
            catch (MappingOperationNotSupportedException exception)
            {
                if (exception.SourceType != typeof(ISecondDerived) ||
                    exception.DestinationType != typeof(string) ||
                    exception.EffectiveMappingMode != MappingMode.Update)
                {
                    throw new InvalidOperationException(
                        "The derived mode failure is incorrect.");
                }
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismModes");
    }

    [Test]
    public void Preserves_zero_one_or_multiple_lookup_for_a_matched_pair()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Exceptions;

namespace TestCase
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public sealed class Dog : IDog { }

    [MorphantMapper]
    public partial class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .Convert(_ => "base");
    }

    [MorphantMapper]
    public partial class DerivedMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IDog, string>()
                .Convert(_ => "dog");
    }

    public sealed class Provider : IServiceProvider
    {
        private readonly int _derivedCount;
        private readonly BaseMapper _baseMapper = new();
        private readonly DerivedMapper _derivedMapper = new();

        public Provider(int derivedCount) =>
            _derivedCount = derivedCount;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IAnimal, object>>))
            {
                return new ITypeMapper<IAnimal, object>[]
                {
                    _baseMapper
                };
            }

            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IDog, string>>))
            {
                return _derivedCount switch
                {
                    0 => Array.Empty<ITypeMapper<IDog, string>>(),
                    1 => new ITypeMapper<IDog, string>[]
                    {
                        _derivedMapper
                    },
                    _ => new ITypeMapper<IDog, string>[]
                    {
                        _derivedMapper,
                        _derivedMapper
                    }
                };
            }

            return null;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = (IAnimal)new Dog();

            try
            {
                new Mapper(new Provider(0))
                    .Map<IAnimal, object>(source);
                throw new InvalidOperationException(
                    "A missing derived pair was accepted.");
            }
            catch (MappingNotFoundException exception)
            {
                if (exception.SourceType != typeof(IDog) ||
                    exception.DestinationType != typeof(string))
                {
                    throw new InvalidOperationException(
                        "Missing lookup reported the base pair.");
                }
            }

            if (!Equals(
                    new Mapper(new Provider(1))
                        .Map<IAnimal, object>(source),
                    "dog"))
            {
                throw new InvalidOperationException(
                    "The single derived pair was not invoked.");
            }

            try
            {
                new Mapper(new Provider(2))
                    .Map<IAnimal, object>(source);
                throw new InvalidOperationException(
                    "Ambiguous derived registrations were accepted.");
            }
            catch (AmbiguousMappingException exception)
            {
                if (exception.SourceType != typeof(IDog) ||
                    exception.DestinationType != typeof(string))
                {
                    throw new InvalidOperationException(
                        "Ambiguous lookup reported the base pair.");
                }
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismLookupLaw");
    }

    [Test]
    public void Resolves_the_exact_base_pair_before_running_its_dispatcher()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Exceptions;

namespace TestCase
{
    public interface IAnimal { }
    public interface IDog : IAnimal { }
    public sealed class Dog : IDog { }

    [MorphantMapper]
    public partial class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<IAnimal, object>()
                .ForDerived<IDog, string>()
                .Convert(_ => "base");
    }

    public sealed class Provider : IServiceProvider
    {
        private readonly int _baseCount;
        private readonly BaseMapper _baseMapper = new();

        public Provider(int baseCount) => _baseCount = baseCount;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IAnimal, object>>))
            {
                return _baseCount switch
                {
                    0 => Array.Empty<ITypeMapper<IAnimal, object>>(),
                    1 => new ITypeMapper<IAnimal, object>[]
                    {
                        _baseMapper
                    },
                    _ => new ITypeMapper<IAnimal, object>[]
                    {
                        _baseMapper,
                        _baseMapper
                    }
                };
            }

            if (serviceType == typeof(IEnumerable<
                    ITypeMapper<IDog, string>>))
            {
                throw new InvalidOperationException(
                    "The derived pair was queried before base selection.");
            }

            return null;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = (IAnimal)new Dog();

            try
            {
                new Mapper(new Provider(0))
                    .Map<IAnimal, object>(source);
                throw new InvalidOperationException(
                    "A missing base pair was accepted.");
            }
            catch (MappingNotFoundException exception)
            {
                AssertBasePair(exception);
            }

            try
            {
                new Mapper(new Provider(2))
                    .Map<IAnimal, object>(source);
                throw new InvalidOperationException(
                    "Ambiguous base pairs were accepted.");
            }
            catch (AmbiguousMappingException exception)
            {
                AssertBasePair(exception);
            }
        }

        private static void AssertBasePair(MappingException exception)
        {
            if (exception.SourceType != typeof(IAnimal) ||
                exception.DestinationType != typeof(object))
            {
                throw new InvalidOperationException(
                    "Lookup reported a derived pair before base selection.");
            }
        }
    }
}
""";

        RunScenario(source, "RuntimePolymorphismBaseLookupLaw");
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
