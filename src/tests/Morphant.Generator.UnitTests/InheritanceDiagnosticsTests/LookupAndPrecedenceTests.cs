namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

[TestFixture]
internal sealed class LookupAndPrecedenceTests
{
    [Test]
    public void Accepts_current_nearest_and_exact_same_pair_candidates()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class Animal
    {
    }

    public sealed class Dog : Animal
    {
    }

    public class AnimalDto
    {
    }

    public sealed class DogDto : AnimalDto
    {
    }

    public abstract class FarMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : FarMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Animal, AnimalDto>();
    }

    public abstract class NearMapper<TMapper> : FarMapper<TMapper>
        where TMapper : NearMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Animal, AnimalDto>();
        }
    }

    [MorphantMapper]
    public partial class SameLevelMapper : TypeMapper<SameLevelMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
            builder.Map<Animal, AnimalDto>();
        }
    }

    [MorphantMapper]
    public partial class NearestMapper : NearMapper<NearestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Dog, DogDto>()
                .IncludeBase<Animal, AnimalDto>();
        }
    }

    [MorphantMapper]
    public partial class SamePairMapper : NearMapper<SamePairMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Animal, AnimalDto>()
                .IncludeBase<Animal, AnimalDto>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Treats_same_pair_without_another_node_as_missing()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .IncludeBase<Source, Destination>();
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        var diagnostic = result.EffectiveDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0026"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Included mapping 'TestCase.Source -> " +
                    "TestCase.Destination' was not found for mapping " +
                    "'TestCase.Source -> TestCase.Destination' in mapper " +
                    "'TestCase.TestMapper'."));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Uses_only_identity_reference_and_boxing_relations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public interface ISource
    {
    }

    public interface IDestination
    {
    }

    public sealed class Source : ISource
    {
    }

    public sealed class Destination : IDestination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ISource, IDestination>();
            builder.Map<Source, Destination>()
                .IncludeBase<ISource, IDestination>();

            builder.Map<object, object>();
            builder.Map<int, int>()
                .IncludeBase<object, object>();

            builder.Map<long, long>();
            builder.Map<short, short>()
                .IncludeBase<long, long>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics
            .Where(static diagnostic => diagnostic.Id is
                "MORPH0024" or
                "MORPH0025" or
                "MORPH0026" or
                "MORPH0027" or
                "MORPH0028")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0027", "MORPH0027" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[] { "long", "long" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Rejects_user_defined_conversions_for_both_type_relations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class IncludedSource
    {
    }

    public sealed class CurrentSource
    {
        public static implicit operator IncludedSource(CurrentSource _) =>
            new();
    }

    public sealed class IncludedDestination
    {
    }

    public sealed class CurrentDestination
    {
        public static implicit operator IncludedDestination(
            CurrentDestination _) => new();
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IncludedSource, IncludedDestination>();
            builder.Map<CurrentSource, CurrentDestination>()
                .IncludeBase<IncludedSource, IncludedDestination>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0027", "MORPH0027" }));
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "IncludedSource",
                    "IncludedDestination"
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Keeps_constructed_generic_missing_edges_context_dependent()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<T>
    {
    }

    public sealed class Destination<T>
    {
    }

    public abstract class GenericMapper<TMapper, T> : TypeMapper<TMapper>
        where TMapper : GenericMapper<TMapper, T>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>()
                .IncludeBase<T, T>();
    }

    [MorphantMapper]
    public partial class IntMapper : GenericMapper<IntMapper, int>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<int>, Destination<int>>()
                .IncludeBase<Source<int>, Destination<int>>();
        }
    }

    [MorphantMapper]
    public partial class StringMapper : GenericMapper<StringMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<string>, Destination<string>>()
                .IncludeBase<Source<string>, Destination<string>>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0026", "MORPH0026" }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Some.Contains(
                    "int -> int"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Some.Contains(
                    "string -> string"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
