using Microsoft.CodeAnalysis.CSharp;
using Morphant.Context;
using Morphant.Exceptions;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class TypeMapperObservableFailureTests
{
    [Test]
    public void Exposes_stable_failure_types_and_messages()
    {
        var failures = new (MorphantException Failure, string Message)[]
        {
            (
                new MappingConfigurationException(
                    MappingOperation.Create,
                    typeof(string),
                    typeof(int),
                    "The plan is invalid."),
                "Mapping 'System.String' -> 'System.Int32' could not be " +
                "generated. The plan is invalid."
            ),
            (
                new MappingOperationNotSupportedException(
                    MappingOperation.Update,
                    typeof(string),
                    typeof(int),
                    MappingMode.Create),
                "MappingMode.Create does not support Update for mapping " +
                "'System.String' -> 'System.Int32'."
            ),
            (
                new NullSourceException(
                    MappingOperation.Create,
                    typeof(string),
                    typeof(int)),
                "NullSourceHandling.Throw does not allow a null source for " +
                "mapping 'System.String' -> 'System.Int32'."
            ),
            (
                new NullDestinationException(
                    MappingOperation.Update,
                    typeof(string),
                    typeof(int)),
                "NullDestinationHandling.Throw does not allow a null " +
                "destination for mapping 'System.String' -> 'System.Int32'."
            ),
            (
                new MappingNotFoundException(
                    MappingOperation.Create,
                    typeof(string),
                    typeof(int)),
                "No mapping is registered for 'System.String' -> " +
                "'System.Int32'."
            ),
            (
                new AmbiguousMappingException(
                    MappingOperation.Create,
                    typeof(string),
                    typeof(int)),
                "Multiple mappings are registered for 'System.String' -> " +
                "'System.Int32'. Exactly one is required."
            ),
            (
                new InvalidMappingRegistrationException(
                    MappingOperation.Create,
                    typeof(string),
                    typeof(int)),
                "The mapping registered for 'System.String' -> " +
                "'System.Int32' resolved to null."
            ),
            (
                new MappingScopeCompletedException(
                    MappingOperation.Create,
                    typeof(string),
                    typeof(int)),
                "MappingContext.Mapper cannot be used after the outer " +
                "mapping call has completed."
            ),
            (
                new NestedDestinationTypeMismatchException(
                    MappingOperation.Update,
                    typeof(object),
                    typeof(string),
                    typeof(string),
                    typeof(int)),
                "Current destination type 'System.Int32' cannot be used " +
                "as 'System.String'."
            ),
            (
                new NestedDestinationTypeMismatchException(
                    MappingOperation.Update,
                    typeof(object),
                    typeof(string),
                    typeof(string),
                    null),
                "The current destination is null and cannot be used as " +
                "'System.String'."
            ),
            (
                new OptionValueMissingException(),
                "Option contains no value."
            ),
            (
                new RuntimeInvocationNotSupportedException(),
                "This Morphant configuration API is compile-time only. Use " +
                "it inside Configure."
            ),
            (
                new InvalidMappingContextException(),
                "MappingContext is not initialized. Use IMapper, or call " +
                "Create/Update through the ITypeMapper extensions."
            ),
            (
                new UnmatchedMappingSwitchException(
                    MappingOperation.Create,
                    typeof(string),
                    typeof(int)),
                "No declarative switch branch matched during Create for " +
                "mapping 'System.String' -> 'System.Int32'."
            ),
            (
                new AmbiguousPolymorphicMappingException(
                    MappingOperation.Create,
                    typeof(object),
                    typeof(object),
                    typeof(string),
                    [typeof(IComparable), typeof(IFormattable)],
                    [typeof(string), typeof(int)]),
                "Runtime source type 'System.String' matches multiple " +
                "equally specific branches for mapping 'System.Object' -> " +
                "'System.Object': 'System.IComparable' -> 'System.String', " +
                "'System.IFormattable' -> 'System.Int32'."
            ),
            (
                new UnmatchedPolymorphicMappingException(
                    MappingOperation.Create,
                    typeof(object),
                    typeof(object),
                    typeof(string)),
                "No polymorphic branch matches runtime source type " +
                "'System.String' for mapping 'System.Object' -> " +
                "'System.Object', and UnknownDerivedTypeHandling.Throw " +
                "rejects base fallback."
            ),
            (
                new PolymorphicDestinationTypeMismatchException(
                    MappingOperation.Update,
                    typeof(object),
                    typeof(object),
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(int)),
                "Runtime source type 'System.String' selected polymorphic " +
                "branch 'System.String' -> 'System.String', but destination " +
                "type 'System.Int32' cannot be used as 'System.String'."
            ),
            (
                new PolymorphicDestinationTypeMismatchException(
                    MappingOperation.Update,
                    typeof(object),
                    typeof(object),
                    typeof(string),
                    typeof(string),
                    typeof(int),
                    null),
                "Runtime source type 'System.String' selected polymorphic " +
                "branch 'System.String' -> 'System.Int32', but a null " +
                "destination cannot be used as 'System.Int32'."
            )
        };

        Assert.Multiple(() =>
        {
            foreach (var (failure, message) in failures)
            {
                Assert.That(failure.Message, Is.EqualTo(message));
            }

            Assert.That(
                failures.Select(static failure => failure.Failure)
                    .OfType<MappingException>()
                    .ToArray(),
                Has.Length.EqualTo(15));

            var configuration =
                (MappingConfigurationException)failures[0].Failure;
            Assert.That(
                configuration.Operation,
                Is.EqualTo(MappingOperation.Create));
            Assert.That(configuration.SourceType, Is.EqualTo(typeof(string)));
            Assert.That(
                configuration.DestinationType,
                Is.EqualTo(typeof(int)));
            Assert.That(configuration.Reason, Is.EqualTo("The plan is invalid."));

            var unsupported =
                (MappingOperationNotSupportedException)failures[1].Failure;
            Assert.That(
                unsupported.EffectiveMappingMode,
                Is.EqualTo(MappingMode.Create));

            var mismatch =
                (NestedDestinationTypeMismatchException)failures[8].Failure;
            Assert.That(
                mismatch.ExpectedDestinationType,
                Is.EqualTo(typeof(string)));
            Assert.That(
                mismatch.ActualDestinationType,
                Is.EqualTo(typeof(int)));

            var ambiguity = failures.Select(static failure => failure.Failure)
                .OfType<AmbiguousPolymorphicMappingException>()
                .Single();
            Assert.That(ambiguity.ActualSourceType, Is.EqualTo(typeof(string)));
            Assert.That(
                ambiguity.MatchingSourceTypes,
                Is.EqualTo(new[]
                {
                    typeof(IComparable),
                    typeof(IFormattable)
                }));
            Assert.That(
                ambiguity.MatchingDestinationTypes,
                Is.EqualTo(new[] { typeof(string), typeof(int) }));

            var unmatched = failures.Select(static failure => failure.Failure)
                .OfType<UnmatchedPolymorphicMappingException>()
                .Single();
            Assert.That(unmatched.ActualSourceType, Is.EqualTo(typeof(string)));

            var polymorphicMismatch = failures
                .Select(static failure => failure.Failure)
                .OfType<PolymorphicDestinationTypeMismatchException>()
                .First();
            Assert.That(
                polymorphicMismatch.BranchSourceType,
                Is.EqualTo(typeof(string)));
            Assert.That(
                polymorphicMismatch.ExpectedDestinationType,
                Is.EqualTo(typeof(string)));
            Assert.That(
                polymorphicMismatch.ActualDestinationType,
                Is.EqualTo(typeof(int)));
        });
    }

    [Test]
    public void Polymorphic_exception_factories_preserve_generated_matches()
    {
        var source = "source";
        var ambiguity =
            AmbiguousPolymorphicMappingException.Create<object, object>(
                MappingOperation.Update,
                source,
                (true, typeof(IComparable), typeof(string)),
                (false, typeof(IDisposable), typeof(object)),
                (true, typeof(IFormattable), typeof(int)));
        var unmatched =
            UnmatchedPolymorphicMappingException.Create<object, object>(
                MappingOperation.Create,
                source);

        Assert.Multiple(() =>
        {
            Assert.That(
                ambiguity.Operation,
                Is.EqualTo(MappingOperation.Update));
            Assert.That(ambiguity.SourceType, Is.EqualTo(typeof(object)));
            Assert.That(
                ambiguity.DestinationType,
                Is.EqualTo(typeof(object)));
            Assert.That(
                ambiguity.ActualSourceType,
                Is.EqualTo(typeof(string)));
            Assert.That(
                ambiguity.MatchingSourceTypes,
                Is.EqualTo(new[]
                {
                    typeof(IComparable),
                    typeof(IFormattable)
                }));
            Assert.That(
                ambiguity.MatchingDestinationTypes,
                Is.EqualTo(new[] { typeof(string), typeof(int) }));
            Assert.That(
                unmatched.ActualSourceType,
                Is.EqualTo(typeof(string)));
            Assert.That(unmatched.SourceType, Is.EqualTo(typeof(object)));
            Assert.That(
                unmatched.DestinationType,
                Is.EqualTo(typeof(object)));
        });
    }

    [Test]
    public void Polymorphic_exception_factories_validate_inputs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => AmbiguousPolymorphicMappingException
                    .Create<object, object>(
                        MappingOperation.Create,
                        null!,
                        (true, typeof(string), typeof(string)),
                        (true, typeof(int), typeof(int))),
                Throws.ArgumentNullException.With.Property("ParamName")
                    .EqualTo("source"));
            Assert.That(
                () => AmbiguousPolymorphicMappingException
                    .Create<object, object>(
                        MappingOperation.Create,
                        new object(),
                        null!),
                Throws.ArgumentNullException.With.Property("ParamName")
                    .EqualTo("branches"));
            Assert.That(
                () => UnmatchedPolymorphicMappingException
                    .Create<object, object>(
                        MappingOperation.Create,
                        null!),
                Throws.ArgumentNullException.With.Property("ParamName")
                    .EqualTo("source"));
        });
    }

    [Test]
    public async Task Emits_complete_stubs_for_an_invalid_effective_setting()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        // lang=c#
        const string expected =
"""
// <auto-generated />
#nullable enable

namespace TestCase
{
    public partial class TestMapper :
        global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(global::TestCase.Source) &&
                    destinationType == typeof(global::TestCase.Destination)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Create(
            global::TestCase.Source? source,
            global::Morphant.Context.MappingContext context)
            => throw new global::Morphant.Exceptions.MappingConfigurationException(
                global::Morphant.Context.MappingOperation.Create,
                typeof(global::TestCase.Source),
                typeof(global::TestCase.Destination),
                "MappingMode has an invalid value.");

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Update(
            global::TestCase.Source? source,
            global::TestCase.Destination? destination,
            global::Morphant.Context.MappingContext context)
            => throw new global::Morphant.Exceptions.MappingConfigurationException(
                global::Morphant.Context.MappingOperation.Update,
                typeof(global::TestCase.Source),
                typeof(global::TestCase.Destination),
                "MappingMode has an invalid value.");
    }
}
""";

        await ConventionTypeMapperGeneratorTest.RunAndAssertWithAnalyzerConfig(
            LanguageVersion.CSharp9,
            source,
            """
is_global = true

build_property.MorphantMappingMode = Unexpected
""",
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs",
                expected
            ));
    }

    [Test]
    public async Task Keeps_independent_pairs_when_generic_contracts_unify()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Envelope<T> { }
    public sealed class Result<T> { }
    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Envelope<T>, Result<string>>();
            builder.Map<Envelope<string>, Result<T>>();
            builder.Map<Source, Destination>();
        }
    }
}
""";

        // lang=c#
        const string expected =
"""
// <auto-generated />
#nullable enable

namespace TestCase
{
    public partial class TestMapper<T> :
        global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(global::TestCase.Source) &&
                    destinationType == typeof(global::TestCase.Destination)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Create(
            global::TestCase.Source? source,
            global::Morphant.Context.MappingContext context)
        {
            if (source is null)
            {
                return default!;
            }

            return __Create(source, context);
        }

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Update(
            global::TestCase.Source? source,
            global::TestCase.Destination? destination,
            global::Morphant.Context.MappingContext context)
        {
            if (source is null)
            {
                return default!;
            }

            if (destination is null)
            {
                return __Create(source, context);
            }

            return __Update(source, destination, context);
        }

        private global::TestCase.Destination __Create(
            global::TestCase.Source source,
            global::Morphant.Context.MappingContext context)
        {
            return new global::TestCase.Destination();
        }

        private global::TestCase.Destination __Update(
            global::TestCase.Source source,
            global::TestCase.Destination destination,
            global::Morphant.Context.MappingContext context)
        {
            return destination;
        }
    }
}
""";

        await ConventionTypeMapperGeneratorTest.RunAndAssertWithAnalyzerConfig(
            LanguageVersion.CSharp9,
            source,
            "is_global = true",
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper_1.g.cs",
                expected
            ));
    }
}
