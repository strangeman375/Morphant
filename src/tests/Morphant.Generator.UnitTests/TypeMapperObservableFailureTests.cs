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
                "context.Mapper cannot be used after the outer mapping " +
                "call has completed."
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
                "This Morphant configuration method cannot be called at " +
                "runtime. Use it only inside Configure."
            ),
            (
                new InvalidMappingContextException(),
                "MappingContext is not initialized. Use IMapper or the " +
                "ITypeMapper Create/Update extension methods."
            ),
            (
                new UnmatchedMappingSwitchException(
                    MappingOperation.Create,
                    typeof(string),
                    typeof(int)),
                "No switch branch matched the current value."
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
                Has.Length.EqualTo(11));

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
