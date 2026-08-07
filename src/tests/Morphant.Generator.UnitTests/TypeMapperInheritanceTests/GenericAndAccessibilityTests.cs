using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class GenericAndAccessibilityTests
{
    [Test]
    public void Provides_the_open_configuration_surface_for_a_closed_base()
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
    public sealed class Source<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class Destination<T>
    {
        public T Value { get; set; } = default!;
    }

    public abstract class GenericBaseMapper<T> : TypeMapper
    {
        protected static TValue Identity<TValue>(TValue value) => value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Members((source, _) => new()
                {
                    Value = Identity<T>((T)source.Value)
                });
    }

    [MorphantMapper]
    public partial class ClosedMapper : GenericBaseMapper<int>
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source<int>, Destination<int>>)
                new ClosedMapper();
            var result = mapper.Create(
                new Source<int> { Value = 17 },
                default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "The closed base mapping was not instantiated.");
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
    public void Substitutes_closed_type_arguments_in_an_included_nested_map()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;
using System.Collections.Generic;

namespace TestCase
{
    public sealed class ChildSource<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class ChildDestination<T>
    {
        public T Value { get; set; } = default!;
    }

    public class OuterSource<T>
    {
        public ChildSource<T> Child { get; init; } = new();
    }

    public sealed class DogSource : OuterSource<int>
    {
    }

    public class OuterDestination<T>
    {
        public ChildDestination<T> Child { get; set; } = new();
    }

    public sealed class DogDestination : OuterDestination<int>
    {
    }

    public abstract class GenericBaseMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource<T>, ChildDestination<T>>();
            builder.Map<OuterSource<T>, OuterDestination<T>>()
                .Members((source, _) => new()
                {
                    Child = Create<ChildDestination<T>>(source.Child)
                });
        }
    }

    [MorphantMapper]
    public partial class ClosedMapper : GenericBaseMapper<int>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<DogSource, DogDestination>()
                .IncludeBase<OuterSource<int>, OuterDestination<int>>();
        }
    }

    public sealed class ManualServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service)
                ? service
                : null;

        public void Add<TService>(TService service)
            where TService : class =>
            _services[typeof(IEnumerable<TService>)] =
                new TService[] { service };
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var typeMapper = new ClosedMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<
                ChildSource<int>,
                ChildDestination<int>>>(typeMapper);
            provider.Add<ITypeMapper<
                DogSource,
                DogDestination>>(typeMapper);
            var mapper = new Mapper(provider);
            var result = mapper.Map<
                DogSource,
                DogDestination>(
                new DogSource
                {
                    Child = new ChildSource<int> { Value = 17 }
                });

            if (result.Child.Value != 17)
            {
                throw new InvalidOperationException(
                    "Included nested-map types were not substituted.");
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
    public void Substitutes_closed_type_arguments_in_an_inherited_Convert()
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
    public sealed class Source<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class Destination<T>
    {
        public Destination(T value) => Value = value;

        public T Value { get; }
    }

    public abstract class GenericBaseMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Convert((source, _, _) =>
                    new Destination<T>((T)source!.Value));
    }

    [MorphantMapper]
    public partial class ClosedMapper : GenericBaseMapper<int>
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Source<int>, Destination<int>>)
                    new ClosedMapper()).Create(
                        new Source<int> { Value = 17 },
                        default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "Inherited Convert type arguments were not substituted.");
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Substitutes_closed_type_arguments_in_an_inherited_factory_body()
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
    public sealed class Source<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class Destination<T>
    {
        public Destination(T value) => Value = value;

        public T Value { get; }
    }

    public abstract class GenericBaseMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Construct(source => new(ByFactory(() =>
                {
                    T value = (T)source.Value;
                    return new Destination<T>(value);
                })));
    }

    [MorphantMapper]
    public partial class ClosedMapper : GenericBaseMapper<int>
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Source<int>, Destination<int>>)
                    new ClosedMapper()).Create(
                        new Source<int> { Value = 17 },
                        default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "Inherited factory type arguments were not substituted.");
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Instantiates_generic_IncludeBase_types_for_a_nested_mapper()
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
    public class Source<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class DerivedSource<T> : Source<T>
    {
        public string Extra { get; init; } = string.Empty;
    }

    public class Destination<T>
    {
        public T Value { get; set; } = default!;

        public string Label { get; set; } = string.Empty;
    }

    public sealed class DerivedDestination<T> : Destination<T>
    {
        public string Extra { get; set; } = string.Empty;
    }

    public abstract class GenericBaseMapper<T> : TypeMapper
    {
        protected static string FormatValue(object? value) =>
            "base:" + value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .Members((source, _) => new()
                {
                    Value = source.Value,
                    Label = FormatValue(source.Value)
                });
    }

    public partial class Container<T>
    {
        [MorphantMapper]
        public partial class Mapper : GenericBaseMapper<T>
        {
            protected override void Configure(MapperBuilder builder)
            {
                base.Configure(builder);
                builder.Map<DerivedSource<T>, DerivedDestination<T>>()
                    .IncludeBase<Source<T>, Destination<T>>()
                    .Members((source, _) => new()
                    {
                        Extra = source.Extra
                    });
            }
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<
                    DerivedSource<int>,
                    DerivedDestination<int>>)
                new Container<int>.Mapper();
            var result = mapper.Create(
                new DerivedSource<int>
                {
                    Value = 17,
                    Extra = "extra"
                },
                default);

            if (result.Value != 17 ||
                result.Label != "base:17" ||
                result.Extra != "extra")
            {
                throw new InvalidOperationException(
                    "The constructed generic IncludeBase pair was lost.");
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
    public void Treats_inaccessible_inherited_expressions_as_unsupported_plans()
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
        public string Value { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
    }

    public class PrivateAnimalDto
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class PrivateDogDto : PrivateAnimalDto
    {
    }

    public class BaseExpressionAnimalDto
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class BaseExpressionDogDto : BaseExpressionAnimalDto
    {
    }

    public abstract class MapperSupport : TypeMapper
    {
        protected string Decorate(string value) => "support:" + value;

        protected override void Configure(MapperBuilder builder)
        {
        }
    }

    public abstract class BaseMapper : MapperSupport
    {
        private static string Secret(string value) => "secret:" + value;

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, PrivateAnimalDto>()
                .Members((source, _) => new()
                {
                    Value = Secret(source.Value)
                });
            builder.Map<Animal, BaseExpressionAnimalDto>()
                .Members((source, _) => new()
                {
                    Value = base.Decorate(source.Value)
                });
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, PrivateDogDto>()
                .IncludeBase<Animal, PrivateAnimalDto>();
            builder.Map<Dog, BaseExpressionDogDto>()
                .IncludeBase<Animal, BaseExpressionAnimalDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DerivedMapper();
            var source = new Dog { Value = "value" };

            ExpectNotSupported(() =>
                ((ITypeMapper<Dog, PrivateDogDto>)mapper)
                    .Create(source, default));
            ExpectNotSupported(() =>
                ((ITypeMapper<Dog, BaseExpressionDogDto>)mapper)
                    .Create(source, default));
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
                "An inaccessible inherited plan was transferred.");
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
    public void Removes_an_overridden_inaccessible_member_rule_before_emission()
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
    }

    public sealed class Dog : Animal
    {
    }

    public class AnimalDto
    {
        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;
    }

    public sealed class DogDto : AnimalDto
    {
    }

    public abstract class BaseMapper : TypeMapper
    {
        private static string Secret(string value) => "secret:" + value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = Secret(source.Name),
                    Code = "base:" + source.Code
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
                    Name = "dog:" + source.Name
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DogMapper()).Create(
                    new Dog { Name = "name", Code = "code" },
                    default);

            if (result.Name != "dog:name" || result.Code != "base:code")
            {
                throw new InvalidOperationException(
                    "The overridden inaccessible rule remained effective.");
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
    public void Does_not_transfer_an_inaccessible_Construct_plan()
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
        public AnimalDto(string value) => Value = value;

        public string Value { get; }
    }

    public sealed class DogDto : AnimalDto
    {
        public DogDto(string value) : base(value)
        {
        }
    }

    public abstract class BaseMapper : TypeMapper
    {
        private static string Secret(int value) => "secret:" + value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>()
                .Construct(source => new(Secret(source.Value)));
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>()
                .Construct(source => new("current:" + source.Value));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Dog, DogDto>)new DerivedMapper())
                    .Create(new Dog { Value = 17 }, default);

            if (result.Value != "current:17")
            {
                throw new InvalidOperationException(
                    "The inaccessible base Construct remained effective.");
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
